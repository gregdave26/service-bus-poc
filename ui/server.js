import express from "express";
import { DefaultAzureCredential } from "@azure/identity";
import { ServiceBusClient } from "@azure/service-bus";
import { mkdirSync, readFileSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { randomUUID } from "node:crypto";
import net from "node:net";
import { generateAccLineup, DEFAULT_UNIT_GROUPS, parseConfig } from "./rosteringLineup.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const app = express();
const port = Number.parseInt(process.env.PORT ?? "5080", 10);
const heartbeatTimeoutMs = 15_000;
const heartbeats = new Map();
const messageHistory = [];
const maxMessageHistory = 500;
const serviceBusConfigPath = path.resolve(__dirname, "..", "infra", "servicebus", "config.json");
const contractsPath = path.resolve(__dirname, "..", "contracts");
const serviceBusConfig = JSON.parse(readFileSync(serviceBusConfigPath, "utf8"));
const subscriberLabels = serviceBusConfig.Dashboard?.SubscriberLabels ?? {};
const producerLabels = serviceBusConfig.Dashboard?.ProducerLabels ?? {};
const configuredMessageTypes = serviceBusConfig.Dashboard?.MessageTypes ?? {};
const posEventsPath = process.env.POS_EVENTS_DB_PATH ?? path.join(__dirname, "data", "pos-events.db");
mkdirSync(path.dirname(posEventsPath), { recursive: true });
const posEventsDb = new DatabaseSync(posEventsPath);
posEventsDb.exec(`
  CREATE TABLE IF NOT EXISTS PosEvents (
    id TEXT PRIMARY KEY,
    event_type TEXT NOT NULL,
    receipt_number TEXT NOT NULL,
    occurred_at TEXT NOT NULL,
    status TEXT NOT NULL,
    payload TEXT NOT NULL
  )
`);
posEventsDb.prepare(`
  INSERT OR IGNORE INTO PosEvents (id, event_type, receipt_number, occurred_at, status, payload)
  VALUES (?, ?, ?, ?, ?, ?)
`).run(
  "pos-transaction-created-example",
  "POS Transaction Created",
  "POS-10042",
  "2026-09-25T07:00:00.000Z",
  "Persisted locally",
  JSON.stringify({ transactionId: "txn-10042", total: 42.5, currency: "AUD", items: 2 }),
);

const rosteringDbPath = process.env.ROSTERING_DB_PATH ?? path.join(__dirname, "data", "rostering.db");
mkdirSync(path.dirname(rosteringDbPath), { recursive: true });
const rosteringDb = new DatabaseSync(rosteringDbPath);
rosteringDb.exec(`
  CREATE TABLE IF NOT EXISTS RosteringInputMappings (
    input_name TEXT PRIMARY KEY,
    source_table TEXT NOT NULL,
    destination_table TEXT NOT NULL
  );
  CREATE TABLE IF NOT EXISTS RosteringBatches (
    batch_id TEXT PRIMARY KEY,
    created_at TEXT NOT NULL,
    status TEXT NOT NULL,
    files_json TEXT NOT NULL,
    validation_json TEXT NOT NULL,
    extracted_xml TEXT
  );
  CREATE TABLE IF NOT EXISTS RosteringRows (
    batch_id TEXT NOT NULL,
    source_file TEXT NOT NULL,
    row_number INTEGER NOT NULL,
    source_json TEXT NOT NULL,
    mapped_json TEXT NOT NULL
  );
`);
const rosteringMappingColumns = rosteringDb.prepare("PRAGMA table_info(RosteringInputMappings)").all().map((column) => column.name);
if (rosteringMappingColumns.includes("staging_table")) {
  rosteringDb.exec(`
    CREATE TABLE RosteringInputMappings_v2 (
      input_name TEXT PRIMARY KEY,
      source_table TEXT NOT NULL,
      destination_table TEXT NOT NULL
    );
    INSERT INTO RosteringInputMappings_v2 (input_name, source_table, destination_table)
      SELECT input_name, source_table, destination_table FROM RosteringInputMappings;
    DROP TABLE RosteringInputMappings;
    ALTER TABLE RosteringInputMappings_v2 RENAME TO RosteringInputMappings;
  `);
}

// Each named input's columns are declared once here as the single source of truth for
// validation (required vs. optional), inferred type, and recognised header aliases.
// Field types:
//   identity - uniquely identifies a person/agent (e.g. AgentID, LogonID)
//   group    - an organisational or categorical classifier used for joining/filtering (e.g. MUId, ActivityCode)
//   date     - a date or date-time value
//   numeric  - a numeric measure or rate
//   text     - free-form descriptive text that isn't required to drive matching, filtering, or the ACC lineup
// Per the Rostering MVP update, identity/date/group/numeric columns remain strictly required
// (the batch is rejected if their header is missing); "text" columns are optional and tolerated
// when entirely absent from a source file.
const rosteringInputs = {
  agentScheduleSummary: {
    label: "Agent schedule summary",
    sourceTable: "agentScheduleSummary",
    destinationTable: "dbo.AgentScheduleSummary",
    fields: [
      { name: "AgentID", type: "identity", required: true, aliases: ["AgentNumber", "StaffID"] },
      { name: "AgentName", type: "identity", required: true, aliases: ["FullName", "DisplayName"] },
      { name: "MUId", type: "group", required: true, aliases: ["BranchID", "SiteID"] },
      { name: "MUName", type: "group", required: true, aliases: ["BranchName", "SiteName"] },
      { name: "ScheduleDate", type: "date", required: true, aliases: ["RosterDate", "ShiftDate"] },
      { name: "StartDateTime", type: "date", required: true, aliases: [] },
      { name: "EndDateTime", type: "date", required: true, aliases: [] },
      { name: "ScheduledMinutes", type: "numeric", required: true, aliases: [] },
      { name: "PaidMinutes", type: "numeric", required: true, aliases: [] },
      { name: "ActivityCode", type: "group", required: true, aliases: ["ActivityType", "StatusCode"] },
    ],
  },
  agentScheduleDetail: {
    label: "Agent schedule detail",
    sourceTable: "agentScheduleDetail",
    destinationTable: "dbo.AgentScheduleDetail",
    fields: [
      { name: "AgentID", type: "identity", required: true, aliases: ["AgentNumber", "StaffID"] },
      { name: "AgentName", type: "identity", required: true, aliases: ["FullName", "DisplayName"] },
      { name: "MUId", type: "group", required: true, aliases: ["BranchID", "SiteID"] },
      { name: "MUName", type: "group", required: true, aliases: ["BranchName", "SiteName"] },
      { name: "ScheduleDate", type: "date", required: true, aliases: ["RosterDate", "ShiftDate"] },
      { name: "StartDateTime", type: "date", required: true, aliases: [] },
      { name: "EndDateTime", type: "date", required: true, aliases: [] },
      { name: "DurationMinutes", type: "numeric", required: true, aliases: [] },
      { name: "ActivityCode", type: "group", required: true, aliases: ["ActivityType", "StatusCode"] },
      { name: "ActivityDescription", type: "text", required: false, aliases: ["ActivityName", "StatusDescription"] },
    ],
  },
  ctActiveForecast: {
    label: "CT active forecast",
    sourceTable: "ctActiveForecast",
    destinationTable: "dbo.ActiveForecast",
    fields: [
      { name: "SAGroupID", type: "group", required: true, aliases: ["SkillGroupID", "QueueID"] },
      { name: "SAGroupName", type: "group", required: true, aliases: ["SkillGroupName", "QueueName"] },
      { name: "ForecastDate", type: "date", required: true, aliases: ["IntervalDate"] },
      { name: "IntervalStartDateTime", type: "date", required: true, aliases: [] },
      { name: "IntervalEndDateTime", type: "date", required: true, aliases: [] },
      { name: "ContactsOffered", type: "numeric", required: true, aliases: [] },
      { name: "AverageHandleTime", type: "numeric", required: true, aliases: [] },
      { name: "RequiredAgents", type: "numeric", required: true, aliases: [] },
      { name: "ServiceLevel", type: "numeric", required: true, aliases: [] },
    ],
  },
  agentInfo: {
    label: "Agent info",
    sourceTable: "agentInfo",
    destinationTable: "dbo.AgentInfo",
    fields: [
      { name: "AgentID", type: "identity", required: true, aliases: ["AgentNumber", "StaffID"] },
      { name: "AgentName", type: "identity", required: true, aliases: ["FullName", "DisplayName"] },
      { name: "LogonID", type: "identity", required: true, aliases: ["UserID", "Username"] },
      { name: "EmployeeID", type: "identity", required: true, aliases: ["StaffNumber", "PayrollID"] },
      { name: "MUId", type: "group", required: true, aliases: ["BranchID", "SiteID"] },
      { name: "MUName", type: "group", required: true, aliases: ["BranchName", "SiteName"] },
      { name: "EmailAddress", type: "text", required: false, aliases: ["Email"] },
      { name: "FirstName", type: "identity", required: true, aliases: ["GivenName"] },
      { name: "LastName", type: "identity", required: true, aliases: ["Surname", "FamilyName"] },
      { name: "StartDate", type: "date", required: true, aliases: [] },
      { name: "EndDate", type: "date", required: true, aliases: [] },
      { name: "TimeOffGroup", type: "text", required: false, aliases: ["LeaveGroup"] },
    ],
  },
};

for (const definition of Object.values(rosteringInputs)) {
  definition.columns = definition.fields.map((field) => field.name);
  definition.requiredColumns = definition.fields.filter((field) => field.required).map((field) => field.name);
  definition.optionalColumns = definition.fields.filter((field) => !field.required).map((field) => field.name);
}

const rosteringMappings = Object.fromEntries(Object.entries(rosteringInputs).map(([inputName, definition]) => {
  rosteringDb.prepare(`
    INSERT OR REPLACE INTO RosteringInputMappings
      (input_name, source_table, destination_table)
    VALUES (?, ?, ?)
  `).run(inputName, definition.sourceTable, definition.destinationTable);
  return [inputName, {
    sourceTable: definition.sourceTable,
    destinationTable: definition.destinationTable,
  }];
}));

// Public, UI-facing projection of the centralized input definitions, so the Rostering tab
// can render labels, required/optional columns, inferred types, and aliases without
// duplicating this configuration client-side.
const rosteringInputDefinitions = Object.fromEntries(Object.entries(rosteringInputs).map(([inputName, definition]) => [inputName, {
  label: definition.label,
  destinationTable: definition.destinationTable,
  fields: definition.fields.map((field) => ({ name: field.name, type: field.type, required: field.required, aliases: field.aliases })),
}]));

const identityAliases = {
  employeeId: ["employeeid", "employee_id", "staffid", "staff_id", "personid", "person_id", "id"],
  firstName: ["firstname", "first_name", "givenname", "given_name"],
  lastName: ["lastname", "last_name", "surname", "familyname", "family_name"],
  email: ["email", "emailaddress", "email_address"],
  role: ["role", "jobtitle", "job_title", "position"],
  date: ["date", "rosterdate", "roster_date", "shiftdate", "shift_date"],
};

function normalizeHeader(value) {
  return String(value).toLowerCase().replace(/[^a-z0-9]/g, "");
}

function normalizeInputName(name) {
  return String(name).replace(/\.txt$/i, "").toLowerCase().replace(/[^a-z0-9]/g, "");
}

function findInputDefinition(name) {
  const normalizedName = normalizeInputName(name);
  return Object.entries(rosteringInputs).find(([inputName]) => normalizeInputName(inputName) === normalizedName)?.[0] ?? null;
}

// A field's header may be supplied using its canonical name or any configured alias.
function fieldHeaderNames(field) {
  return [field.name, ...(field.aliases ?? [])].map(normalizeHeader);
}

// Tolerant numeric parsing: a value that fails to parse is kept as supplied rather than rejected,
// since only the column's presence is strictly required, not the value's format.
function coerceFieldValue(field, value) {
  if (field.type !== "numeric" || value === "") return value;
  const parsed = Number(value);
  return Number.isNaN(parsed) ? value : parsed;
}

function parseDelimited(content) {
  const lines = String(content ?? "").replace(/^\uFEFF/, "").split(/\r?\n/).filter((line) => line.trim() !== "");
  if (!lines.length) return { headers: [], rows: [] };
  const delimiter = lines[0].includes("\t") ? "\t" : ",";
  const parseLine = (line) => {
    const cells = [];
    let value = "";
    let quoted = false;
    for (let index = 0; index < line.length; index += 1) {
      const character = line[index];
      if (character === '"') {
        if (quoted && line[index + 1] === '"') { value += '"'; index += 1; }
        else quoted = !quoted;
      } else if (character === delimiter && !quoted) { cells.push(value.trim()); value = ""; }
      else value += character;
    }
    cells.push(value.trim());
    return cells;
  };
  const headers = parseLine(lines[0]).map((header, index) => header || `column${index + 1}`);
  return { headers, rows: lines.slice(1).map((line) => Object.fromEntries(parseLine(line).map((value, index) => [headers[index], value ?? ""]))) };
}

function mapRosteringRow(row) {
  const normalized = new Map(Object.entries(row).map(([key, value]) => [normalizeHeader(key), value]));
  const mapped = {};
  for (const [field, aliases] of Object.entries(identityAliases)) {
    const match = aliases.find((alias) => normalized.has(alias));
    if (match && normalized.get(match) !== "") mapped[field] = normalized.get(match);
  }
  return mapped;
}

function mapOdsRow(inputName, row) {
  const mapped = mapRosteringRow(row);
  const definition = rosteringInputs[inputName];
  const normalized = new Map(Object.entries(row).map(([key, value]) => [normalizeHeader(key), value]));
  for (const field of definition.fields) {
    const headerName = fieldHeaderNames(field).find((name) => normalized.has(name));
    if (headerName === undefined) continue; // optional column absent from this file - tolerated
    const value = normalized.get(headerName);
    if (value !== undefined) mapped[field.name] = coerceFieldValue(field, value);
  }
  return mapped;
}

function escapeXml(value) {
  return String(value ?? "").replace(/[<>&'"]/g, (character) => ({ "<": "&lt;", ">": "&gt;", "&": "&amp;", "'": "&apos;", '"': "&quot;" }[character]));
}

function createLineupXml(rows) {
  const grouped = [...rows].sort((left, right) =>
    (left.source_file < right.source_file ? -1 : left.source_file > right.source_file ? 1 : 0) ||
    left.row_number - right.row_number);
  const groups = new Map();
  for (const row of grouped) {
    if (!groups.has(row.source_file)) groups.set(row.source_file, []);
    groups.get(row.source_file).push(row);
  }
  const body = [...groups.entries()].map(([source, sourceRows]) =>
    `  <group source="${escapeXml(source)}">\n${sourceRows.map((row) =>
      `    <row number="${row.row_number}">
      <mapped>${Object.entries(JSON.parse(row.mapped_json ?? "{}")).sort(([a], [b]) => a.localeCompare(b)).map(([key, value]) => `<${key}>${escapeXml(value)}</${key}>`).join("")}</mapped>
      <source>${Object.entries(JSON.parse(row.source_json ?? "{}")).sort(([a], [b]) => a.localeCompare(b)).map(([key, value]) => `<field name="${escapeXml(key)}">${escapeXml(value)}</field>`).join("")}</source>
    </row>`).join("\n")}\n  </group>`).join("\n");
  return `<?xml version="1.0" encoding="UTF-8"?>\n<lineup>\n${body}\n</lineup>\n`;
}

function processRosteringBatch(files) {
  if (!Array.isArray(files) || files.length !== 4) return { error: "Upload exactly four TXT files." };
  const batchId = randomUUID();
  const validation = [];
  const rows = [];
  for (const file of files) {
    const name = String(file?.name ?? "");
    if (!/\.txt$/i.test(name)) { validation.push({ file: name || "unnamed", valid: false, error: "File must use .txt." }); continue; }
    const inputName = findInputDefinition(name);
    if (!inputName) {
      validation.push({ file: name, valid: false, error: "Filename must be agentScheduleSummary, agentScheduleDetail, ctActiveForecast, or agentInfo (.txt)." });
      continue;
    }
    const parsed = parseDelimited(file.content);
    if (!parsed.headers.length) { validation.push({ file: name, valid: false, error: "File has no header row." }); continue; }
    const normalizedHeaders = new Set(parsed.headers.map(normalizeHeader));
    const missingColumns = rosteringInputs[inputName].fields
      .filter((field) => field.required && !fieldHeaderNames(field).some((name) => normalizedHeaders.has(name)))
      .map((field) => field.name);
    if (missingColumns.length) {
      validation.push({
        file: name,
        input: inputName,
        label: rosteringInputs[inputName].label,
        mapping: rosteringMappings[inputName],
        valid: false,
        headers: parsed.headers,
        error: `Missing required columns: ${missingColumns.join(", ")}.`,
      });
      continue;
    }
    parsed.rows.forEach((row, index) => rows.push({ source_file: name, row_number: index + 2, source_json: JSON.stringify(row), mapped_json: JSON.stringify(mapOdsRow(inputName, row)) }));
    const optionalColumnsFound = rosteringInputs[inputName].fields
      .filter((field) => !field.required && fieldHeaderNames(field).some((headerName) => normalizedHeaders.has(headerName)))
      .map((field) => field.name);
    validation.push({
      file: name,
      input: inputName,
      label: rosteringInputs[inputName].label,
      mapping: rosteringMappings[inputName],
      valid: true,
      headers: parsed.headers,
      rowCount: parsed.rows.length,
      optionalColumnsFound,
    });
  }
  const suppliedInputs = validation.filter((result) => result.input).map((result) => result.input);
  const missingInputs = Object.keys(rosteringInputs).filter((inputName) => !suppliedInputs.includes(inputName));
  if (missingInputs.length) validation.push({ file: "batch", valid: false, error: `Missing required files: ${missingInputs.join(", ")}.` });
  const valid = validation.length === 4 && new Set(suppliedInputs).size === 4 && validation.every((result) => result.valid);
  rosteringDb.prepare("INSERT INTO RosteringBatches VALUES (?, ?, ?, ?, ?, ?)").run(batchId, new Date().toISOString(), valid ? "loaded" : "invalid", JSON.stringify(files.map(({ name }) => name)), JSON.stringify(validation), null);
  if (valid) {
    const insert = rosteringDb.prepare("INSERT INTO RosteringRows VALUES (?, ?, ?, ?, ?)");
    for (const row of rows) insert.run(batchId, row.source_file, row.row_number, row.source_json, row.mapped_json);
  }
  return { batchId, valid, validation, rowCount: valid ? rows.length : 0 };
}

function getMessageTypes() {
  return Object.fromEntries(Object.entries(configuredMessageTypes).map(([type, configuration]) => {
    const schema = JSON.parse(readFileSync(path.join(contractsPath, configuration.Schema), "utf8"));
    const configurationErrors = [];
    const formFields = configuration.FormFields ?? {};
    for (const [name, field] of Object.entries(formFields)) {
      if (!schema.properties?.[name] && !field.Type) {
        configurationErrors.push(`Configured field "${name}" was not found in the contract schema.`);
      }
      if (!field.Label || field.Label.trim() === "") {
        configurationErrors.push(`No label was configured for field "${name}".`);
      }
    }
    const schemaFields = Object.entries(schema.properties ?? {}).map(([name, property]) => ({
      Name: name,
      Type: property.type === "boolean" ? "boolean" : property.format === "email" ? "email" : property.type === "object" ? "object" : "text",
      Required: (schema.required ?? []).includes(name),
      Options: property.enum,
    }));
    const additionalFields = Object.entries(formFields)
      .filter(([name]) => !schema.properties?.[name])
      .map(([name, field]) => ({ Name: name, ...field }));
    const fieldsByName = new Map([...schemaFields, ...additionalFields].map((field) => [field.Name, field]));
    const order = [...fieldsByName.keys()].sort((left, right) =>
      (formFields[left]?.Order ?? Number.MAX_SAFE_INTEGER) -
      (formFields[right]?.Order ?? Number.MAX_SAFE_INTEGER));
    const fields = order
      .map((name) => fieldsByName.get(name))
      .filter(Boolean)
      .map((field) => ({ ...field, Label: formFields[field.Name]?.Label ?? field.Name }));
    return [type, { DisplayName: configuration.DisplayName, Fields: fields, ConfigurationErrors: configurationErrors }];
  }));
}

const messageTypes = getMessageTypes();

app.use(express.json({ limit: "4mb" }));
app.use(express.static(path.join(__dirname, "public", "dist"), {
  setHeaders: (response) => response.setHeader("Cache-Control", "no-store"),
}));
app.use(express.static(path.join(__dirname, "public"), {
  setHeaders: (response) => response.setHeader("Cache-Control", "no-store"),
}));

function getServiceStatuses() {
  const now = Date.now();

  return [...heartbeats.values()].map((heartbeat) => ({
    ...heartbeat,
    state:
      now - Date.parse(heartbeat.sentAt) < heartbeatTimeoutMs
        ? heartbeat.state
        : "Offline",
  }));
}

function getMessages(service, direction) {
  return messageHistory.filter((message) =>
    (!service || message.serviceName === service) &&
    (!direction || message.direction === direction));
}

function getEmulatorStatus() {
  const host = process.env.ServiceBus__EmulatorHost ?? "127.0.0.1";
  const port = Number.parseInt(process.env.ServiceBus__EmulatorPort ?? "5672", 10);

  return new Promise((resolve) => {
    const socket = net.createConnection({ host, port });
    const finish = (running, error) => {
      socket.destroy();
      resolve({
        running,
        host,
        port,
        checkedAt: new Date().toISOString(),
        error: error ?? null,
      });
    };

    socket.setTimeout(1500, () => finish(false, "Connection timed out"));
    socket.once("connect", () => finish(true));
    socket.once("error", (error) => finish(false, error.code ?? error.message));
  });
}

function validatePublishRequest(body) {
  const type = body?.type ?? "ContactUpdated";
  const configuredType = messageTypes[type];
  if (!configuredType) return `Unsupported message type: ${type}`;
  const requiredFields = (configuredType.Fields ?? [])
    .filter((field) => field.Required)
    .map((field) => field.Name);
  const missingFields = requiredFields.filter(
    (field) => {
      const value = body?.[field];
      return typeof value !== "string" || value.trim() === "";
    },
  );

  if (missingFields.length > 0) {
    return `Missing required fields: ${missingFields.join(", ")}`;
  }

  return null;
}

function createPublishMessage(request) {
  const type = request.type ?? "ContactUpdated";
  const configuredType = messageTypes[type];
  const data = {};
  for (const field of configuredType?.Fields ?? []) {
    if (field.Type === "boolean") data[field.Name] = request[field.Name] === true;
    else if (request[field.Name] !== undefined) data[field.Name] = String(request[field.Name]).trim();
  }
  const applicationProperties = Object.fromEntries(
    (configuredType?.Fields ?? [])
      .filter((field) => field.Type === "boolean")
      .map((field) => [field.Name, data[field.Name] === true]),
  );
  if (type === "ContactUpdated") data.attributes = { ...applicationProperties };
  const event = {
    id: randomUUID(),
    type,
    source: "dashboard",
    timestamp: new Date().toISOString(),
    dataVersion: "1.0",
    correlationId: randomUUID(),
    data: {
      ...data,
    },
  };

  return {
    body: event,
    contentType: "application/json",
    messageId: event.id,
    correlationId: event.correlationId,
    subject: event.type,
    applicationProperties,
  };
}

function validateDashboardMessage(body) {
  const requiredStrings = ["messageId", "eventId", "serviceName", "direction", "payload"];
  const missingFields = requiredStrings.filter(
    (field) => typeof body?.[field] !== "string" || body[field].trim() === "",
  );
  if (missingFields.length > 0) return `Missing required fields: ${missingFields.join(", ")}`;
  if (!["sent", "received"].includes(body.direction)) return "direction must be sent or received";
  if (typeof body.timestamp !== "string" || Number.isNaN(Date.parse(body.timestamp))) {
    return "timestamp must be a valid ISO date";
  }
  try {
    JSON.parse(body.payload);
  } catch {
    return "payload must be a JSON string";
  }
  if (body.subscriptionName !== undefined && body.subscriptionName !== null &&
      (typeof body.subscriptionName !== "string" || body.subscriptionName.trim() === "")) {
    return "subscriptionName must be a non-empty string or null";
  }
  return null;
}

function storeMessage(message) {
  messageHistory.unshift({
    messageId: message.messageId,
    eventId: message.eventId,
    serviceName: message.serviceName,
    direction: message.direction,
    timestamp: message.timestamp,
    subscriptionName: message.subscriptionName ?? null,
    payload: message.payload,
  });
  if (messageHistory.length > maxMessageHistory) messageHistory.length = maxMessageHistory;
  return messageHistory[0];
}

async function publishContactEvent(request) {
  const connectionString = process.env.ServiceBus__ConnectionString;
  const namespace = process.env.ServiceBus__Namespace;
  const topicName = process.env.ServiceBus__TopicName ?? "contact.events";

  if (!connectionString && !namespace) {
    throw new Error(
      "Service Bus is not configured. Set ServiceBus__ConnectionString or ServiceBus__Namespace.",
    );
  }

  const client = connectionString
    ? new ServiceBusClient(connectionString)
    : new ServiceBusClient(namespace, new DefaultAzureCredential());

  const sender = client.createSender(topicName);
  const message = createPublishMessage(request);
  const event = message.body;

  try {
    await sender.sendMessages(message);
    return event;
  } finally {
    await sender.close();
    await client.close();
  }
}

app.post("/api/heartbeat", (request, response) => {
  const heartbeat = request.body;

  if (!heartbeat?.serviceName || !heartbeat?.state || !heartbeat?.sentAt) {
    return response.status(400).json({ error: "serviceName, state, and sentAt are required" });
  }

  heartbeats.set(heartbeat.serviceName, {
    serviceName: heartbeat.serviceName,
    subscriptionName: heartbeat.subscriptionName ?? null,
    state: heartbeat.state,
    sentAt: heartbeat.sentAt,
    messagesHandled: heartbeat.messagesHandled ?? 0,
    lastMessageAt: heartbeat.lastMessageAt ?? null,
    lastEventId: heartbeat.lastEventId ?? null,
  });

  return response.status(204).send();
});

app.get("/api/status", (_request, response) => response.json(getServiceStatuses()));

app.get("/api/config", (_request, response) => response.json({ subscriberLabels, producerLabels, messageTypes, rosteringMappings, rosteringInputDefinitions }));

app.get("/api/pos-events", (_request, response) => {
  const events = posEventsDb.prepare(`
    SELECT id, event_type AS eventType, receipt_number AS receiptNumber,
      occurred_at AS occurredAt, status, payload
    FROM PosEvents ORDER BY occurred_at DESC
  `).all().map((event) => ({ ...event, payload: JSON.parse(event.payload) }));
  return response.json(events);
});

app.post("/api/rostering/upload", (request, response) => {
  const result = processRosteringBatch(request.body?.files);
  return result.error ? response.status(400).json({ error: result.error }) : response.status(result.valid ? 201 : 422).json(result);
});

app.post("/api/rostering/:batchId/extract", (request, response) => {
  const batch = rosteringDb.prepare("SELECT status FROM RosteringBatches WHERE batch_id = ?").get(request.params.batchId);
  if (!batch) return response.status(404).json({ error: "Rostering batch not found." });
  if (batch.status !== "loaded") return response.status(422).json({ error: "Only a valid loaded batch can be extracted." });
  const rows = rosteringDb.prepare("SELECT source_file, row_number, source_json, mapped_json FROM RosteringRows WHERE batch_id = ?").all(request.params.batchId)
    .map((row) => ({ ...row, input_name: findInputDefinition(row.source_file) }));
  const mappedRows = rows.map((row) => ({ ...JSON.parse(row.source_json ?? "{}"), ...JSON.parse(row.mapped_json ?? "{}"), input_name: row.input_name }));
  const unitGroups = parseConfig(process.env.ROSTERING_UNIT_GROUPS, DEFAULT_UNIT_GROUPS);
  const lineup = generateAccLineup({
    rows: mappedRows,
    generatedAt: request.body?.generatedAt,
    start: request.body?.start,
    end: request.body?.end,
    sequence: request.body?.sequence ?? 1,
    timezone: process.env.ROSTERING_TIMEZONE ?? "UTC",
    filters: request.body?.filters ?? {},
    coordinateFields: parseConfig(process.env.ROSTERING_COORDINATE_FIELDS, {}),
    unitGroups,
  });
  rosteringDb.prepare("UPDATE RosteringBatches SET status = ?, extracted_xml = ? WHERE batch_id = ?").run("extracted", lineup.xml, request.params.batchId);
  return response.json({ batchId: request.params.batchId, xml: lineup.xml, rowCount: rows.length, filename: lineup.filename, doneFilename: lineup.doneFilename, header: lineup.header, units: lineup.units.map((unit) => ({ unitId: unit.unitId, agentCount: unit.agents.length })) });
});

app.get("/api/emulator-status", async (_request, response) => {
  response.json(await getEmulatorStatus());
});

app.post("/api/publish", async (request, response) => {
  const validationError = validatePublishRequest(request.body);
  if (validationError) {
    return response.status(400).json({ success: false, error: validationError });
  }

  try {
    const event = await publishContactEvent(request.body);
    storeMessage({
      messageId: event.id,
      eventId: event.id,
      serviceName: "producer",
      direction: "sent",
      timestamp: event.timestamp,
      payload: JSON.stringify(event),
    });
    return response.json({ success: true, eventId: event.id });
  } catch (error) {
    console.error("Failed to publish event from dashboard", error);
    return response.status(502).json({
      success: false,
      error: error instanceof Error ? error.message : "Failed to publish event",
    });
  }
});

app.post("/api/messages", (request, response) => {
  const validationError = validateDashboardMessage(request.body);
  if (validationError) {
    return response.status(400).json({ error: validationError });
  }
  return response.status(201).json(storeMessage(request.body));
});

app.get("/api/messages", (request, response) => {
  return response.json(getMessages(request.query.service, request.query.direction));
});

app.get("/api/messages/:id", (request, response) => {
  const message = messageHistory.find((candidate) => candidate.messageId === request.params.id);
  return message ? response.json(message) : response.status(404).json({ error: "Message not found" });
});

export {
  app,
  getEmulatorStatus,
  getServiceStatuses,
  validatePublishRequest,
  createPublishMessage,
  validateDashboardMessage,
  storeMessage,
  getMessages,
  messageHistory,
  posEventsDb,
  parseDelimited,
  mapRosteringRow,
  mapOdsRow,
  createLineupXml,
  processRosteringBatch,
  rosteringInputs,
  rosteringMappings,
  rosteringInputDefinitions,
};

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  app.listen(port, () => {
    console.log(`Service Bus POC dashboard listening on http://localhost:${port}`);
  });
}
