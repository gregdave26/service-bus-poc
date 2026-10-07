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
export const rosteringInputs = {
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

// Public, UI-facing projection of the centralized input definitions, so the Rostering tab
// can render labels, required/optional columns, inferred types, and aliases without
// duplicating this configuration client-side.
export const rosteringInputDefinitions = Object.fromEntries(Object.entries(rosteringInputs).map(([inputName, definition]) => [inputName, {
  label: definition.label,
  destinationTable: definition.destinationTable,
  fields: definition.fields.map((field) => ({ name: field.name, type: field.type, required: field.required, aliases: field.aliases })),
}]));
