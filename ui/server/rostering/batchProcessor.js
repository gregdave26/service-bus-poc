import { mkdirSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";
import path from "node:path";
import { randomUUID } from "node:crypto";
import { uiRoot } from "../shared/paths.js";
import { rosteringInputs } from "./inputDefinitions.js";
import { logRostering } from "./log.js";

const rosteringDbPath = process.env.ROSTERING_DB_PATH ?? path.join(uiRoot, "data", "rostering.db");
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
export { rosteringDb };

export const rosteringMappings = Object.fromEntries(Object.entries(rosteringInputs).map(([inputName, definition]) => {
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

export function findInputDefinition(name) {
  const normalizedName = normalizeInputName(name);
  return Object.entries(rosteringInputs).find(([inputName]) => {
    const normalizedInputName = normalizeInputName(inputName);
    if (normalizedName === normalizedInputName) return true;
    const suffix = normalizedName.slice(normalizedInputName.length);
    return normalizedName.startsWith(normalizedInputName) && /^\d+$/.test(suffix);
  })?.[0] ?? null;
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

export function parseDelimited(content) {
  const lines = String(content ?? "").replace(/^\uFEFF/, "").split(/\r?\n/).filter((line) => line.trim() !== "");
  if (!lines.length) return { headers: [], rows: [] };
  const delimiter = lines[0].includes("|") ? "|" : lines[0].includes("\t") ? "\t" : ",";
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

export function mapRosteringRow(row) {
  const normalized = new Map(Object.entries(row).map(([key, value]) => [normalizeHeader(key), value]));
  const mapped = {};
  for (const [field, aliases] of Object.entries(identityAliases)) {
    const match = aliases.find((alias) => normalized.has(alias));
    if (match && normalized.get(match) !== "") mapped[field] = normalized.get(match);
  }
  return mapped;
}

export function mapOdsRow(inputName, row) {
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

export function createLineupXml(rows) {
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

export function processRosteringBatch(files) {
  logRostering("batch.processing_started", {
    fileCount: Array.isArray(files) ? files.length : 0,
    filenames: Array.isArray(files) ? files.map((file) => String(file?.name ?? "unnamed")) : [],
  });
  if (!Array.isArray(files) || files.length !== 4) {
    logRostering("batch.rejected", { reason: "incorrect_file_count" });
    return { error: "Upload exactly four TXT files." };
  }
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
  logRostering(valid ? "batch.loaded" : "batch.rejected", {
    batchId,
    valid,
    rowCount: valid ? rows.length : 0,
    validation: validation.map(({ file, input, valid: itemValid, rowCount, error }) => ({ file, input, valid: itemValid, rowCount: rowCount ?? 0, error })),
  });
  return { batchId, valid, validation, rowCount: valid ? rows.length : 0 };
}
