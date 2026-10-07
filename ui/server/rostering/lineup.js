const DEFAULT_UNIT_GROUPS = [
  { unitId: "4000", muIds: ["4000"], saGroupIds: ["4000"] },
  { unitId: "4001", muIds: ["4001"], saGroupIds: ["4001"] },
  { unitId: "4002", muIds: ["4002"], saGroupIds: ["4002"] },
];

function text(value) {
  return value === undefined || value === null ? "" : String(value);
}

function xml(value) {
  return text(value).replace(/[<>&'"]/g, (character) => ({ "<": "&lt;", ">": "&gt;", "&": "&amp;", "'": "&apos;", '"': "&quot;" }[character]));
}

function timestamp(value, timezone) {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) throw new Error(`Invalid lineup timestamp: ${value}`);
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: timezone,
    year: "numeric", month: "2-digit", day: "2-digit",
    hour: "2-digit", minute: "2-digit", second: "2-digit", hourCycle: "h23",
  }).formatToParts(date).reduce((result, part) => ({ ...result, [part.type]: part.value }), {});
  return `${parts.year}${parts.month}${parts.day}${parts.hour}${parts.minute}${parts.second}`;
}

function parseConfig(value, fallback) {
  if (!value) return fallback;
  try { return JSON.parse(value); } catch { return fallback; }
}

function rowValue(row, ...names) {
  const entries = Object.entries(row ?? {});
  const found = entries.find(([key]) => names.some((name) => key.toLowerCase().replace(/[^a-z0-9]/g, "") === name.toLowerCase().replace(/[^a-z0-9]/g, "")));
  return found?.[1] ?? "";
}

function dateValue(row, ...names) {
  const value = rowValue(row, ...names);
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function availability(activityCode) {
  const code = text(activityCode).trim().toUpperCase();
  if (!code) return "UNKNOWN";
  if (/WORK|AVAILABLE|READY|CONTACT/.test(code)) return "AVAILABLE";
  if (/BREAK|MEAL|TRAIN|MEETING|COACH/.test(code)) return "BUSY";
  if (/LEAVE|AWAY|SICK|UNAVAILABLE|OFF/.test(code)) return "UNAVAILABLE";
  return "BUSY";
}

function unitFor(row, groups) {
  const mu = text(rowValue(row, "MUId", "MU"));
  const sa = text(rowValue(row, "SAGroupID", "SAGroup"));
  return groups.find((group) => group.unitId === mu || group.unitId === sa)?.unitId
    ?? groups.find((group) => (!group.muIds?.length || group.muIds.includes(mu)) && (!group.saGroupIds?.length || group.saGroupIds.includes(sa)))?.unitId
    ?? null;
}

function buildFilename(start, end, sequence, timezone = "UTC") {
  return `Lineup_RAC_${timestamp(start, timezone)}_ROADSIDE_${timestamp(end, timezone)}_${String(sequence).padStart(3, "0")}.xml`;
}

function createAccLineupXml(model) {
  const header = model.header;
  const units = model.units.map((unit) => `    <Unit UnitID="${xml(unit.unitId)}">${unit.agents.map((agent) => `      <Agent AgentID="${xml(agent.agentId)}" AgentName="${xml(agent.agentName)}"${agent.employeeId ? ` EmployeeID="${xml(agent.employeeId)}"` : ""}${agent.vehicleId ? ` VehicleID="${xml(agent.vehicleId)}"` : ""}${agent.coordinates ? ` XCoord="${xml(agent.coordinates.x)}" YCoord="${xml(agent.coordinates.y)}"` : ""}>${agent.shifts.map((shift) => `        <Shift Start="${xml(shift.start)}" End="${xml(shift.end)}" Availability="${xml(shift.availability)}">${shift.activities.map((activity) => `<Activity Start="${xml(activity.start)}" End="${xml(activity.end)}" ActivityCode="${xml(activity.code)}" Availability="${xml(activity.availability)}">${xml(activity.description)}</Activity>`).join("")}</Shift>`).join("")}</Agent>`).join("\n")}</Unit>`).join("\n");
  return `<?xml version="1.0" encoding="UTF-8"?>\n<Lineup Club="${xml(header.club)}" LineupType="${xml(header.lineupType)}" Generated="${xml(header.generated)}" Start="${xml(header.start)}" End="${xml(header.end)}" Sequence="${xml(header.sequence)}">\n${units}\n</Lineup>\n`;
}

export function generateAccLineup({ rows, generatedAt, start, end, sequence = 1, timezone = "UTC", filters = {}, coordinateFields = {}, unitGroups = DEFAULT_UNIT_GROUPS }) {
  const byInput = Object.groupBy(rows ?? [], (row) => row.input_name ?? row.inputName ?? "");
  const infos = byInput.agentInfo ?? [];
  const summaries = byInput.agentScheduleSummary ?? [];
  const details = byInput.agentScheduleDetail ?? [];
  const filterMu = new Set(filters.muIds ?? []);
  const filterSa = new Set(filters.saGroupIds ?? []);
  const eligible = (row) => (!filterMu.size || filterMu.has(text(rowValue(row, "MUId"))) || filterMu.has(text(rowValue(row, "MUName"))))
    && (!filterSa.size || filterSa.has(text(rowValue(row, "SAGroupID"))) || filterSa.has(text(rowValue(row, "SAGroupName"))));
  const infoByAgent = new Map(infos.filter(eligible).map((row) => [text(rowValue(row, "AgentID")), row]));
  const summaryByAgent = new Map();
  summaries.filter(eligible).forEach((row) => {
    const id = text(rowValue(row, "AgentID")); if (!summaryByAgent.has(id)) summaryByAgent.set(id, []); summaryByAgent.get(id).push(row);
  });
  const detailByAgent = new Map();
  details.filter(eligible).forEach((row) => {
    const id = text(rowValue(row, "AgentID")); if (!detailByAgent.has(id)) detailByAgent.set(id, []); detailByAgent.get(id).push(row);
  });
  const agents = [...new Set([...summaryByAgent.keys(), ...detailByAgent.keys(), ...infoByAgent.keys()])].sort();
  const units = unitGroups.map((group) => ({ unitId: group.unitId, agents: [] }));
  for (const agentId of agents) {
    const info = infoByAgent.get(agentId) ?? {};
    const summary = (summaryByAgent.get(agentId) ?? []).slice().sort((a, b) => text(rowValue(a, "StartDateTime")).localeCompare(text(rowValue(b, "StartDateTime"))));
    const detail = (detailByAgent.get(agentId) ?? []).slice().sort((a, b) => text(rowValue(a, "StartDateTime")).localeCompare(text(rowValue(b, "StartDateTime"))));
    const source = summary[0] ?? detail[0] ?? info;
    const group = unitFor(source, unitGroups);
    const unit = units.find((item) => item.unitId === group);
    if (!unit) continue;
    const coords = coordinateFields.x && coordinateFields.y && rowValue(info, coordinateFields.x) !== "" && rowValue(info, coordinateFields.y) !== ""
      ? { x: rowValue(info, coordinateFields.x), y: rowValue(info, coordinateFields.y) } : null;
    const activities = detail.map((item) => ({ start: rowValue(item, "StartDateTime"), end: rowValue(item, "EndDateTime"), code: rowValue(item, "ActivityCode"), description: rowValue(item, "ActivityDescription"), availability: availability(rowValue(item, "ActivityCode")) }));
    const suppliedName = text(rowValue(info, "AgentName") || rowValue(source, "AgentName")).trim();
    if (!suppliedName.includes("#")) continue;
    unit.agents.push({
      agentId, agentName: suppliedName,
      employeeId: rowValue(info, "EmployeeID", "EmployeeId") || rowValue(source, "EmployeeID"),
      vehicleId: rowValue(info, "VehicleID", "VehicleId") || rowValue(source, "VehicleID"),
      coordinates: coords,
      shifts: summary.map((item) => ({ start: rowValue(item, "StartDateTime"), end: rowValue(item, "EndDateTime"), availability: availability(rowValue(item, "ActivityCode")), activities: activities.filter((activity) => activity.start >= rowValue(item, "StartDateTime") && activity.end <= rowValue(item, "EndDateTime")) })),
    });
  }
  const generated = generatedAt ?? new Date().toISOString();
  const inferredStart = summaries.map((row) => dateValue(row, "StartDateTime")).filter(Boolean).sort((a, b) => a - b)[0];
  const inferredEnd = summaries.map((row) => dateValue(row, "EndDateTime")).filter(Boolean).sort((a, b) => b - a)[0];
  const startDate = start ?? inferredStart?.toISOString() ?? generated;
  const endDate = end ?? inferredEnd?.toISOString() ?? generated;
  const header = { club: "RAC", lineupType: "ROADSIDE", generated: generated, start: startDate, end: endDate, sequence: String(sequence).padStart(3, "0") };
  return { filename: buildFilename(startDate, endDate, sequence, timezone), doneFilename: `${buildFilename(startDate, endDate, sequence, timezone)}.done`, header, units, xml: createAccLineupXml({ header, units }) };
}

export { availability, buildFilename, createAccLineupXml, DEFAULT_UNIT_GROUPS, parseConfig };
