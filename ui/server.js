import express from "express";
import { DefaultAzureCredential } from "@azure/identity";
import { ServiceBusClient } from "@azure/service-bus";
import { mkdirSync, readFileSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { randomUUID } from "node:crypto";
import net from "node:net";

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

app.use(express.json({ limit: "32kb" }));
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

app.get("/api/config", (_request, response) => response.json({ subscriberLabels, producerLabels, messageTypes }));

app.get("/api/pos-events", (_request, response) => {
  const events = posEventsDb.prepare(`
    SELECT id, event_type AS eventType, receipt_number AS receiptNumber,
      occurred_at AS occurredAt, status, payload
    FROM PosEvents ORDER BY occurred_at DESC
  `).all().map((event) => ({ ...event, payload: JSON.parse(event.payload) }));
  return response.json(events);
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
};

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  app.listen(port, () => {
    console.log(`Service Bus POC dashboard listening on http://localhost:${port}`);
  });
}
