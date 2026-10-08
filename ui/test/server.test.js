import assert from "node:assert/strict";
import { existsSync, readFileSync, unlinkSync } from "node:fs";
import test from "node:test";
import path from "node:path";
import { app } from "../server/app.js";
import {
  getEmulatorStatus,
  getMessages,
  getServiceStatuses,
  messageHistory,
  storeMessage,
  createPublishMessage,
  validateDashboardMessage,
  validatePublishRequest,
} from "../server/contactEvents/index.js";
import {
  processRosteringBatch,
  parseDelimited,
  createLineupXml,
  rosteringMappings,
  rosteringInputs,
  rosteringInputDefinitions,
  mapOdsRow,
} from "../server/rostering/index.js";import { generateAccLineup, buildFilename } from "../server/rostering/lineup.js";
import { generateRosterFile, rosterFilename } from "../src/rosteringGenerator.js";

test("generates typed pipe-delimited roster files using the documented primary filename", () => {
  const now = new Date(2026, 9, 2, 11, 43);
  for (const [inputName, definition] of Object.entries(rosteringInputs)) {
    const generated = generateRosterFile(inputName, definition, now);
    assert.equal(generated.name, `${inputName}_261002_1143.txt`);
    const [header, firstRow] = generated.content.trim().split("\n");
    assert.equal(header, definition.fields.map((field) => field.name).join("|"));
    const values = firstRow.split("|");
    assert.equal(values.length, definition.fields.length);
    for (const field of definition.fields.filter((candidate) => candidate.required)) {
      const value = values[definition.fields.indexOf(field)];
      assert.notEqual(value, "", `${inputName}.${field.name} should have a generated value`);
      if (field.type === "numeric") assert.ok(Number.isFinite(Number(value)), `${inputName}.${field.name} should be numeric`);
      if (field.type === "date") assert.match(value, /^\d{4}-\d{2}-\d{2}( \d{2}:\d{2})?$/, `${inputName}.${field.name} should be date-shaped`);
    }
  }
  assert.equal(rosterFilename("agentInfo", now), "agentInfo_261002_1143.txt");
});

test("parses pipe-delimited TXT roster content", () => {
  const parsed = parseDelimited('AgentID|AgentName|ActivityDescription\nE1|Ada #1|"Available|primary"');
  assert.deepEqual(parsed.headers, ["AgentID", "AgentName", "ActivityDescription"]);
  assert.deepEqual(parsed.rows, [{ AgentID: "E1", AgentName: "Ada #1", ActivityDescription: "Available|primary" }]);
});

test("auto-saves generated roster content to the temporary directory", async () => {
  await withServer(async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/rostering/temp-files`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ name: "agentInfo_test.txt", content: "AgentID|AgentName\nAG1|Alex Morgan #1\n" }),
    });
    const body = await response.json();
    assert.equal(response.status, 200);
    assert.equal(body.saved, true);
    const savedPath = path.join(body.directory, body.name);
    assert.equal(existsSync(savedPath), true);
    assert.equal(readFileSync(savedPath, "utf8"), "AgentID|AgentName\nAG1|Alex Morgan #1\n");
    unlinkSync(savedPath);
  });
});

async function withServer(callback) {
  const server = app.listen(0);
  try {
    const address = server.address();
    return await callback(`http://127.0.0.1:${address.port}`);
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
}

test("validates all required publish fields", () => {
  assert.match(validatePublishRequest({}), /contactId, firstName, lastName/);
  assert.equal(
    validatePublishRequest({
      contactId: "c-1",
      firstName: "Ada",
      lastName: "Lovelace",
      phone: "0400000000",
      email: "ada@example.com",
    }),
    null,
  );
});

test("promotes capability flags to Service Bus application properties", () => {
  const message = createPublishMessage({
    contactId: "c-1",
    firstName: "Ada",
    lastName: "Lovelace",
    phone: "0400000000",
    email: "ada@example.com",
    hasInsurance: true,
    hasParksResorts: false,
    hasCarwashProduct: true,
  });

  assert.deepEqual(message.applicationProperties, {
    hasInsurance: true,
    hasParksResorts: false,
    hasCarwashProduct: true,
  });
  assert.deepEqual(message.body.data.attributes, message.applicationProperties);
});

test("creates configured ProductHoldingChange messages with dynamic fields", () => {
  assert.equal(validatePublishRequest({
    type: "ProductHoldingChange",
    contactId: "c-2",
    holdingId: "holding-2",
    productType: "insurance",
    action: "created",
  }), null);
  const message = createPublishMessage({
    type: "ProductHoldingChange",
    contactId: "c-2",
    holdingId: "holding-2",
    productType: "insurance",
    action: "created",
  });
  assert.equal(message.body.type, "ProductHoldingChange");
  assert.deepEqual(message.body.data, {
    contactId: "c-2",
    holdingId: "holding-2",
    productType: "insurance",
    action: "created",
  });
});

test("rejects unsupported configured message types", () => {
  assert.equal(validatePublishRequest({ type: "NotConfigured" }), "Unsupported message type: NotConfigured");
});

test("returns an empty status list when no heartbeats have been received", () => {
  assert.deepEqual(getServiceStatuses(), []);
});

test("reports emulator status with its AMQP endpoint", async () => {
  const status = await getEmulatorStatus();
  assert.equal(typeof status.running, "boolean");
  assert.equal(status.port, 5672);
  assert.match(status.host, /127\.0\.0\.1/);
});

test("validates and filters live dashboard messages", () => {
  messageHistory.length = 0;
  const message = {
    messageId: "m-live",
    eventId: "e-live",
    serviceName: "Insurance",
    direction: "received",
    timestamp: new Date().toISOString(),
    subscriptionName: "insurance",
    payload: JSON.stringify({ id: "e-live" }),
  };
  assert.equal(validateDashboardMessage(message), null);
  assert.equal(validateDashboardMessage({ ...message, payload: "not-json" }), "payload must be a JSON string");
  storeMessage(message);
  storeMessage({ ...message, messageId: "m-sent", direction: "sent", serviceName: "producer" });
  assert.equal(getMessages("Insurance", "received").length, 1);
  assert.equal(getMessages(null, "sent")[0].messageId, "m-sent");
});

test("serves dashboard API resources and publish validation", async () => {
  messageHistory.length = 0;
  await withServer(async (baseUrl) => {
    const heartbeat = await fetch(`${baseUrl}/api/heartbeat`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ serviceName: "Insurance", state: "Connected", sentAt: new Date().toISOString(), messagesHandled: 3 }),
    });

    assert.equal(heartbeat.status, 204);
    assert.equal(heartbeat.headers.get("cache-control"), "no-store");
    assert.equal((await (await fetch(`${baseUrl}/api/status`)).json())[0].serviceName, "Insurance");
    const config = await (await fetch(`${baseUrl}/api/config`)).json();
    assert.ok(config.subscriberLabels);
    assert.equal(config.digitalSiteApiBaseUrl, process.env.DIGITAL_SITE_API_BASE_URL ?? "http://localhost:5200");
    assert.deepEqual(config.rosteringMappings.ctActiveForecast, {
      sourceTable: "ctActiveForecast",
      destinationTable: "dbo.ActiveForecast",
    });
    assert.equal(config.rosteringInputDefinitions.agentScheduleSummary.label, "Agent schedule summary");
    assert.ok(config.rosteringInputDefinitions.agentInfo.fields.some((field) => field.name === "TimeOffGroup" && field.required === false));
    assert.equal((await fetch(`${baseUrl}/api/heartbeat`, { method: "POST", headers: { "content-type": "application/json" }, body: "{}" })).status, 400);

    const created = await fetch(`${baseUrl}/api/messages`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ messageId: "m-api", eventId: "e-api", serviceName: "Insurance", direction: "received", timestamp: new Date().toISOString(), payload: "{}" }),
    });
    assert.equal(created.status, 201);
    assert.equal((await (await fetch(`${baseUrl}/api/messages/m-api`)).json()).messageId, "m-api");
    assert.equal((await fetch(`${baseUrl}/api/messages/missing`)).status, 404);
    assert.equal((await fetch(`${baseUrl}/api/publish`, { method: "POST", headers: { "content-type": "application/json" }, body: "{}" })).status, 400);
    const publishFailure = await fetch(`${baseUrl}/api/publish`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ contactId: "c-1", firstName: "Ada", lastName: "Lovelace", phone: "0400000000", email: "ada@example.com" }),
    });
    assert.equal(publishFailure.status, 502);
  });
});

test("serves the seeded local POS event from SQLite", async () => {
  await withServer(async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/pos-events`);
    assert.equal(response.status, 200);
    const events = await response.json();
    assert.equal(events[0].eventType, "POS Transaction Created");
    assert.equal(events[0].receiptNumber, "POS-10042");
    assert.equal(events[0].payload.transactionId, "txn-10042");
  });
});

test("loads the four named roster inputs into logical targets and maps ODS-like fields", () => {
  const files = [
    { name: "agentScheduleSummary.txt", content: "AgentID,AgentName,MUId,MUName,ScheduleDate,StartDateTime,EndDateTime,ScheduledMinutes,PaidMinutes,ActivityCode\nE1,Ada #1,4000,Sales,2026-10-01,09:00,17:00,480,480,WORK" },
    { name: "agentScheduleDetail.txt", content: "AgentID\tAgentName\tMUId\tMUName\tScheduleDate\tStartDateTime\tEndDateTime\tDurationMinutes\tActivityCode\tActivityDescription\nE1\tAda #1\t4000\tSales\t2026-10-01\t09:00\t17:00\t480\tWORK\tWork" },
    { name: "ctActiveForecast.txt", content: "SAGroupID,SAGroupName,ForecastDate,IntervalStartDateTime,IntervalEndDateTime,ContactsOffered,AverageHandleTime,RequiredAgents,ServiceLevel\nS1,Sales,2026-10-01,09:00,09:30,12,300,4,0.8" },
    { name: "agentInfo.txt", content: "AgentID,AgentName,LogonID,EmployeeID,MUId,MUName,EmailAddress,FirstName,LastName,StartDate,EndDate,TimeOffGroup\nE1,Ada #1,a1,EMP1,4000,Sales,ada@example.com,Ada,Lovelace,2020-01-01,,Standard" },
  ];
  const result = processRosteringBatch(files);
  assert.equal(result.valid, true);
  assert.equal(result.rowCount, 4);
  assert.equal(result.validation.length, 4);
  assert.deepEqual(rosteringMappings, {
    agentScheduleSummary: {
      sourceTable: "agentScheduleSummary",
      destinationTable: "dbo.AgentScheduleSummary",
    },
    agentScheduleDetail: {
      sourceTable: "agentScheduleDetail",
      destinationTable: "dbo.AgentScheduleDetail",
    },
    ctActiveForecast: {
      sourceTable: "ctActiveForecast",
      destinationTable: "dbo.ActiveForecast",
    },
    agentInfo: {
      sourceTable: "agentInfo",
      destinationTable: "dbo.AgentInfo",
    },
  });
  assert.deepEqual(result.validation.map((item) => item.mapping.destinationTable), [
    "dbo.AgentScheduleSummary",
    "dbo.AgentScheduleDetail",
    "dbo.ActiveForecast",
    "dbo.AgentInfo",
  ]);
});

test("centralizes input field definitions with inferred types, required/optional flags, and aliases", () => {
  assert.deepEqual(
    rosteringInputs.agentScheduleDetail.fields.find((field) => field.name === "ActivityDescription"),
    { name: "ActivityDescription", type: "text", required: false, aliases: ["ActivityName", "StatusDescription"] },
  );
  assert.deepEqual(rosteringInputs.agentInfo.requiredColumns, [
    "AgentID", "AgentName", "LogonID", "EmployeeID", "MUId", "MUName", "FirstName", "LastName", "StartDate", "EndDate",
  ]);
  assert.deepEqual(rosteringInputs.agentInfo.optionalColumns, ["EmailAddress", "TimeOffGroup"]);
  assert.ok(rosteringInputDefinitions.agentScheduleSummary.fields.every((field) => field.type));
});

test("loads a batch that omits optional columns and recognises configured header aliases", () => {
  const files = [
    { name: "agentScheduleSummary.txt", content: "AgentID,AgentName,MUId,MUName,ScheduleDate,StartDateTime,EndDateTime,ScheduledMinutes,PaidMinutes,ActivityCode\nE1,Ada #1,4000,Sales,2026-10-01,09:00,17:00,480,480,WORK" },
    // Omits the optional ActivityDescription column entirely.
    { name: "agentScheduleDetail.txt", content: "AgentID\tAgentName\tMUId\tMUName\tScheduleDate\tStartDateTime\tEndDateTime\tDurationMinutes\tActivityCode\nE1\tAda #1\t4000\tSales\t2026-10-01\t09:00\t17:00\t480\tWORK" },
    { name: "ctActiveForecast.txt", content: "SAGroupID,SAGroupName,ForecastDate,IntervalStartDateTime,IntervalEndDateTime,ContactsOffered,AverageHandleTime,RequiredAgents,ServiceLevel\nS1,Sales,2026-10-01,09:00,09:30,12,300,4,0.8" },
    // Uses the AgentNumber/BranchID/BranchName aliases instead of the canonical AgentID/MUId/MUName headers, and omits optional columns.
    { name: "agentInfo.txt", content: "AgentNumber,AgentName,LogonID,EmployeeID,BranchID,BranchName,FirstName,LastName,StartDate,EndDate\nE1,Ada #1,a1,EMP1,4000,Sales,Ada,Lovelace,2020-01-01," },
  ];
  const result = processRosteringBatch(files);
  assert.equal(result.valid, true);
  assert.equal(result.rowCount, 4);
  const detailValidation = result.validation.find((item) => item.input === "agentScheduleDetail");
  assert.deepEqual(detailValidation.optionalColumnsFound, []);
  const infoValidation = result.validation.find((item) => item.input === "agentInfo");
  assert.deepEqual(infoValidation.optionalColumnsFound, []);
  // The aliased AgentID/MUId/MUName headers must still map to their canonical field names.
  assert.deepEqual(mapOdsRow("agentInfo", { AgentNumber: "E1", AgentName: "Ada #1", LogonID: "a1", EmployeeID: "EMP1", BranchID: "4000", BranchName: "Sales", FirstName: "Ada", LastName: "Lovelace", StartDate: "2020-01-01", EndDate: "" }).AgentID, "E1");
});

test("tolerantly coerces numeric columns and keeps unparseable optional values", () => {
  assert.equal(
    mapOdsRow("agentScheduleSummary", { AgentID: "E1", AgentName: "Ada #1", MUId: "4000", MUName: "Sales", ScheduleDate: "2026-10-01", StartDateTime: "09:00", EndDateTime: "17:00", ScheduledMinutes: "480", PaidMinutes: "n/a", ActivityCode: "WORK" }).ScheduledMinutes,
    480,
  );
  assert.equal(
    mapOdsRow("agentScheduleSummary", { AgentID: "E1", AgentName: "Ada #1", MUId: "4000", MUName: "Sales", ScheduleDate: "2026-10-01", StartDateTime: "09:00", EndDateTime: "17:00", ScheduledMinutes: "480", PaidMinutes: "n/a", ActivityCode: "WORK" }).PaidMinutes,
    "n/a",
  );
  assert.equal(mapOdsRow("agentScheduleDetail", { AgentID: "E1", AgentName: "Ada #1", MUId: "4000", MUName: "Sales", ScheduleDate: "2026-10-01", StartDateTime: "09:00", EndDateTime: "17:00", DurationMinutes: "480", ActivityCode: "WORK" }).ActivityDescription, undefined);
});

test("reports missing required columns without loading an invalid batch", () => {
  const result = processRosteringBatch([
    { name: "agentScheduleSummary.txt", content: "AgentID,AgentName,MUId,MUName,ScheduleDate,StartDateTime,EndDateTime,ScheduledMinutes,PaidMinutes\nE1,Ada,M1,Sales,2026-10-01,09:00,17:00,480,480" },
    { name: "agentScheduleDetail.txt", content: "AgentID,AgentName,MUId,MUName,ScheduleDate,StartDateTime,EndDateTime,DurationMinutes,ActivityCode,ActivityDescription\nE1,Ada,M1,Sales,2026-10-01,09:00,17:00,480,WORK,Work" },
    { name: "ctActiveForecast.txt", content: "SAGroupID,SAGroupName,ForecastDate,IntervalStartDateTime,IntervalEndDateTime,ContactsOffered,AverageHandleTime,RequiredAgents,ServiceLevel\nS1,Sales,2026-10-01,09:00,09:30,12,300,4,0.8" },
    { name: "agentInfo.txt", content: "AgentID,AgentName,LogonID,EmployeeID,MUId,MUName,EmailAddress,FirstName,LastName,StartDate,EndDate,TimeOffGroup\nE1,Ada,a1,EMP1,M1,Sales,ada@example.com,Ada,Lovelace,2020-01-01,,Standard" },
  ]);
  assert.equal(result.valid, false);
  assert.match(result.validation.find((item) => item.file === "agentScheduleSummary.txt").error, /ActivityCode/);
  assert.equal(result.rowCount, 0);
});

test("rejects wrong filenames even when four files are supplied", () => {
  const files = [
    "agentScheduleSummary.txt",
    "agentScheduleDetail.txt",
    "ctActiveForecast.txt",
    "people.txt",
  ].map((name) => ({ name, content: "AgentID,AgentName,MUId,MUName,ScheduleDate,StartDateTime,EndDateTime,ScheduledMinutes,PaidMinutes,ActivityCode\nE1,Ada,M1,Sales,2026-10-01,09:00,17:00,480,480,WORK" }));
  const result = processRosteringBatch(files);
  assert.equal(result.valid, false);
  assert.match(result.validation.find((item) => item.file === "people.txt").error, /Filename must be/);
});

test("accepts documented timestamped roster filenames", () => {
  const files = [
    { name: "agentScheduleSummary_261005_0920.txt", content: "AgentID,AgentName,MUId,MUName,ScheduleDate,StartDateTime,EndDateTime,ScheduledMinutes,PaidMinutes,ActivityCode\nE1,Ada #1,4000,Sales,2026-10-01,09:00,17:00,480,480,WORK" },
    { name: "agentScheduleDetail2_261005_0920.txt", content: "AgentID\tAgentName\tMUId\tMUName\tScheduleDate\tStartDateTime\tEndDateTime\tDurationMinutes\tActivityCode\tActivityDescription\nE1\tAda #1\t4000\tSales\t2026-10-01\t09:00\t17:00\t480\tWORK\tWork" },
    { name: "ctActiveForecast_261005_0920.txt", content: "SAGroupID,SAGroupName,ForecastDate,IntervalStartDateTime,IntervalEndDateTime,ContactsOffered,AverageHandleTime,RequiredAgents,ServiceLevel\nS1,Sales,2026-10-01,09:00,09:30,12,300,4,0.8" },
    { name: "agentInfo2_261005_0920.txt", content: "AgentID,AgentName,LogonID,EmployeeID,MUId,MUName,EmailAddress,FirstName,LastName,StartDate,EndDate,TimeOffGroup\nE1,Ada #1,a1,EMP1,4000,Sales,ada@example.com,Ada,Lovelace,2020-01-01,,Standard" },
  ];
  const result = processRosteringBatch(files);
  assert.equal(result.valid, true);
  assert.equal(result.rowCount, 4);
});

test("rejects invalid batch shape and creates deterministic escaped lineup XML", () => {
  assert.match(processRosteringBatch([{ name: "one.csv", content: "id\n1" }]).error, /exactly four/);
  const xml = createLineupXml([
    { source_file: "b.csv", row_number: 2, mapped_json: JSON.stringify({ lastName: "B & <" }) },
    { source_file: "a.csv", row_number: 2, mapped_json: JSON.stringify({ employeeId: "A" }) },
  ]);
  assert.match(xml, /<group source="a.csv">/);
  assert.match(xml, /B &amp; &lt;/);
  assert.ok(xml.indexOf('source="a.csv"') < xml.indexOf('source="b.csv"'));
});

test("maps joined schedule data to filtered RAC roadside units and deterministic artifact names", () => {
  const result = generateAccLineup({
    generatedAt: "2026-10-01T01:00:00.000Z",
    rows: [
      { input_name: "agentInfo", AgentID: "A1", AgentName: "Road #1", EmployeeID: "EMP-1", MUId: "M1" },
      { input_name: "agentScheduleSummary", AgentID: "A1", AgentName: "Road #1", MUId: "M1", StartDateTime: "2026-10-01T09:00:00+08:00", EndDateTime: "2026-10-01T17:00:00+08:00", ActivityCode: "WORK" },
      { input_name: "agentScheduleDetail", AgentID: "A1", AgentName: "Road #1", MUId: "M1", StartDateTime: "2026-10-01T09:00:00+08:00", EndDateTime: "2026-10-01T12:00:00+08:00", ActivityCode: "BREAK", ActivityDescription: "Break" },
    ],
    timezone: "Australia/Sydney",
    filters: { muIds: ["M1"] },
    unitGroups: [{ unitId: "4000", muIds: ["M1"], saGroupIds: [] }, { unitId: "4001", muIds: ["M2"], saGroupIds: [] }, { unitId: "4002", muIds: ["M3"], saGroupIds: [] }],
  });
  assert.equal(result.filename, "Lineup_RAC_20261001110000_ROADSIDE_20261001190000_001.xml");
  assert.equal(result.doneFilename, `${result.filename}.done`);
  assert.match(result.xml, /Club="RAC".*LineupType="ROADSIDE"/);
  assert.match(result.xml, /AgentID="A1" AgentName="Road #1" EmployeeID="EMP-1"/);
  assert.match(result.xml, /ActivityCode="BREAK" Availability="BUSY"/);
  assert.equal(result.units.find((unit) => unit.unitId === "4001").agents.length, 0);
  assert.equal(buildFilename("2026-10-01T00:00:00Z", "2026-10-01T01:00:00Z", 2), "Lineup_RAC_20261001000000_ROADSIDE_20261001010000_002.xml");
});

test("allows an empty filtered lineup and configured coordinates without conversion", () => {
  const result = generateAccLineup({
    generatedAt: "2026-10-01T00:00:00Z",
    rows: [],
    coordinateFields: { x: "Longitude", y: "Latitude" },
    filters: { muIds: ["missing"] },
  });
  assert.match(result.xml, /<Lineup /);
  assert.match(result.filename, /_001\.xml$/);
  assert.equal(result.units.reduce((count, unit) => count + unit.agents.length, 0), 0);
});

test("excludes agents whose source name lacks the required naming marker", () => {
  const result = generateAccLineup({
    generatedAt: "2026-10-01T00:00:00Z",
    rows: [
      { input_name: "agentInfo", AgentID: "A1", AgentName: "Ada", MUId: "M1" },
      { input_name: "agentScheduleSummary", AgentID: "A1", AgentName: "Ada", MUId: "M1", StartDateTime: "2026-10-01T09:00:00Z", EndDateTime: "2026-10-01T10:00:00Z", ActivityCode: "WORK" },
    ],
    unitGroups: [{ unitId: "4000", muIds: ["M1"], saGroupIds: [] }],
  });
  assert.doesNotMatch(result.xml, /AgentID="A1"/);
});

test("uploads a valid roster batch and extracts lineup XML through the API", async () => {
  const files = [
    { name: "agentScheduleSummary.txt", content: "AgentID,AgentName,MUId,MUName,ScheduleDate,StartDateTime,EndDateTime,ScheduledMinutes,PaidMinutes,ActivityCode\nE1,Ada #1,4000,Sales,2026-10-01,09:00,17:00,480,480,WORK" },
    { name: "agentScheduleDetail.txt", content: "AgentID\tAgentName\tMUId\tMUName\tScheduleDate\tStartDateTime\tEndDateTime\tDurationMinutes\tActivityCode\tActivityDescription\nE1\tAda #1\t4000\tSales\t2026-10-01\t09:00\t17:00\t480\tWORK\tWork" },
    { name: "ctActiveForecast.txt", content: "SAGroupID,SAGroupName,ForecastDate,IntervalStartDateTime,IntervalEndDateTime,ContactsOffered,AverageHandleTime,RequiredAgents,ServiceLevel\nS1,Sales,2026-10-01,09:00,09:30,12,300,4,0.8" },
    { name: "agentInfo.txt", content: "AgentID,AgentName,LogonID,EmployeeID,MUId,MUName,EmailAddress,FirstName,LastName,StartDate,EndDate,TimeOffGroup\nE1,Ada #1,a1,EMP1,4000,Sales,ada@example.com,Ada,Lovelace,2020-01-01,,Standard" },
  ];

  await withServer(async (baseUrl) => {
    const upload = await fetch(`${baseUrl}/api/rostering/upload`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ files }),
    });
    assert.equal(upload.status, 201);
    const batch = await upload.json();
    assert.equal(batch.valid, true);
    assert.equal(batch.rowCount, 4);

    const extraction = await fetch(`${baseUrl}/api/rostering/${batch.batchId}/extract`, { method: "POST" });
    assert.equal(extraction.status, 200);
    const extracted = await extraction.json();
    assert.equal(extracted.rowCount, 4);
    assert.match(extracted.xml, /^<\?xml version="1\.0"/);
    assert.match(extracted.xml, /<Lineup Club="RAC" LineupType="ROADSIDE"/);
    assert.match(extracted.xml, /<Agent AgentID="E1"/);
    assert.match(extracted.filename, /^Lineup_RAC_/);
  });
});
