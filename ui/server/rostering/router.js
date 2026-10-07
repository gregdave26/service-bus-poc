import express from "express";
import { mkdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { findInputDefinition, processRosteringBatch, rosteringDb, rosteringMappings } from "./batchProcessor.js";
import { rosteringInputDefinitions } from "./inputDefinitions.js";
import { DEFAULT_UNIT_GROUPS, generateAccLineup, parseConfig } from "./lineup.js";
import { logRostering } from "./log.js";

export const rosteringClientConfig = { rosteringMappings, rosteringInputDefinitions };

export const rosteringRouter = express.Router();
const router = rosteringRouter;

const rosteringTempDirectory = process.env.ROSTERING_TEMP_DIR || path.join(tmpdir(), "service-bus-poc-rostering");
mkdirSync(rosteringTempDirectory, { recursive: true });

router.post("/api/rostering/upload", (request, response) => {
  const result = processRosteringBatch(request.body?.files);
  logRostering("batch.upload_completed", { batchId: result.batchId ?? null, statusCode: result.error ? 400 : result.valid ? 201 : 422 });
  return result.error ? response.status(400).json({ error: result.error }) : response.status(result.valid ? 201 : 422).json(result);
});

router.post("/api/rostering/temp-files", (request, response) => {
  const name = String(request.body?.name ?? "");
  const content = request.body?.content;
  if (!/^[A-Za-z0-9][A-Za-z0-9._-]*\.txt$/i.test(name)) return response.status(400).json({ error: "Generated filename must be a safe .txt filename." });
  if (typeof content !== "string") return response.status(400).json({ error: "Generated file content is required." });
  try {
    writeFileSync(path.join(rosteringTempDirectory, name), content, "utf8");
    logRostering("file.generated_saved", { name, bytes: Buffer.byteLength(content, "utf8"), directory: rosteringTempDirectory });
    return response.json({ saved: true, name, directory: rosteringTempDirectory });
  } catch (error) {
    logRostering("file.generated_save_failed", { name, error: error.message });
    console.error("Failed to save generated rostering file", error);
    return response.status(500).json({ error: "Unable to save generated file to the temporary directory." });
  }
});

router.post("/api/rostering/:batchId/extract", (request, response) => {
  logRostering("extraction.started", { batchId: request.params.batchId });
  const batch = rosteringDb.prepare("SELECT status FROM RosteringBatches WHERE batch_id = ?").get(request.params.batchId);
  if (!batch) {
    logRostering("extraction.rejected", { batchId: request.params.batchId, reason: "batch_not_found" });
    return response.status(404).json({ error: "Rostering batch not found." });
  }
  if (batch.status !== "loaded") {
    logRostering("extraction.rejected", { batchId: request.params.batchId, reason: "batch_not_loaded", status: batch.status });
    return response.status(422).json({ error: "Only a valid loaded batch can be extracted." });
  }
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
  logRostering("extraction.completed", { batchId: request.params.batchId, rowCount: rows.length, filename: lineup.filename, units: lineup.units.map((unit) => unit.unitId) });
  return response.json({ batchId: request.params.batchId, xml: lineup.xml, rowCount: rows.length, filename: lineup.filename, doneFilename: lineup.doneFilename, header: lineup.header, units: lineup.units.map((unit) => ({ unitId: unit.unitId, agentCount: unit.agents.length })) });
});
