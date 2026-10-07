# Rostering workflow MVP

The dashboard's **Rostering** tab is a local workflow simulator for four named inputs. It accepts exactly one `.txt` file for each input and treats the first row of each file as a header row. The file extension is `.txt`, while the contents use pipe-delimited CSV format (`|` between columns), including quoted pipe fields.

## Centralized input definitions

Each of the four named inputs is defined once, server-side, in `ui/server.js` (`rosteringInputs`) as the single source of truth for validation and mapping. Every column has an inferred **type**, a **required**/optional flag, and a list of recognised header **aliases**. The same definitions are served to the UI via `GET /api/config` as `rosteringInputDefinitions`, so the Rostering tab renders required/optional columns without duplicating this configuration client-side.

Column types:

| Type | Meaning | Validation |
| --- | --- | --- |
| `identity` | Uniquely identifies a person/agent (e.g. `AgentID`, `LogonID`) | Required; header must be present |
| `group` | An organisational or categorical classifier used for joining/filtering (e.g. `MUId`, `SAGroupID`, `ActivityCode`) | Required; header must be present |
| `date` | A date or date-time value | Required; header must be present |
| `numeric` | A numeric measure or rate | Required; header must be present; tolerantly coerced to a number when parseable |
| `text` | Free-form descriptive text not needed to drive matching, filtering, or the ACC lineup | Optional; the column, and its value, may be omitted entirely from the source file |

Per the Rostering MVP update, `identity`/`date`/`group`/`numeric` columns remain strictly required — the batch is rejected if their header is missing. `text` columns are tolerated when absent.

## Required and optional inputs and columns

| Input filename (extension must be `.txt`) | Display label | Required columns | Optional columns |
| --- | --- | --- | --- |
| `agentScheduleSummary` | Agent schedule summary | `AgentID`, `AgentName`, `MUId`, `MUName`, `ScheduleDate`, `StartDateTime`, `EndDateTime`, `ScheduledMinutes`, `PaidMinutes`, `ActivityCode` | *(none)* |
| `agentScheduleDetail` | Agent schedule detail | `AgentID`, `AgentName`, `MUId`, `MUName`, `ScheduleDate`, `StartDateTime`, `EndDateTime`, `DurationMinutes`, `ActivityCode` | `ActivityDescription` |
| `ctActiveForecast` | CT active forecast | `SAGroupID`, `SAGroupName`, `ForecastDate`, `IntervalStartDateTime`, `IntervalEndDateTime`, `ContactsOffered`, `AverageHandleTime`, `RequiredAgents`, `ServiceLevel` | *(none)* |
| `agentInfo` | Agent info | `AgentID`, `AgentName`, `LogonID`, `EmployeeID`, `MUId`, `MUName`, `FirstName`, `LastName`, `StartDate`, `EndDate` | `EmailAddress`, `TimeOffGroup` |

Each column also recognises a small set of header aliases (matched case-insensitively, ignoring punctuation, in addition to the canonical name), for example `AgentID` also accepts `AgentNumber` or `StaffID`, and `MUId` also accepts `BranchID` or `SiteID`. The full alias list for every column is available from `GET /api/config` (`rosteringInputDefinitions.<input>.fields[].aliases`).

Inbound logical-target mappings are explicit in the SQLite `RosteringInputMappings` metadata table and are returned by `GET /api/config` (`rosteringMappings`):

| Input | Logical target |
| --- | --- |
| `agentScheduleSummary` | `dbo.AgentScheduleSummary` |
| `agentScheduleDetail` | `dbo.AgentScheduleDetail` |
| `ctActiveForecast` | `dbo.ActiveForecast` |
| `agentInfo` | `dbo.AgentInfo` |

Filename matching is case-insensitive and ignores separators in the stem, so `agent_schedule_detail.txt` is also accepted. Header matching is case-insensitive and ignores punctuation. Other filenames, duplicate input names, missing inputs, unsupported extensions, and missing required columns are rejected before loading; a file that omits an optional column is still valid.

## Assumptions and configuration

The server loads each validated source row directly into its persisted logical target representation in SQLite and maps the concrete schema columns to ODS-like fields. Source-file name and row-number metadata are retained for traceability. The mapped JSON retains the supplied column names and values:

- common identity aliases are also emitted where available (`employeeId`, `firstName`, `lastName`, `email`, and `date`)
- numeric columns are tolerantly parsed to a number when the supplied value is parseable; otherwise the original value is retained unchanged
- optional (`text`) columns are included in the mapped JSON only when their header is present in the source file

Unrecognised headers remain in the source JSON and are not discarded. A batch is valid only when all four named files have a supported extension, a non-empty header row, and all required columns. The SQLite path defaults to `ui/data/rostering.db` and can be changed with `ROSTERING_DB_PATH`.

## Workflow

Completed process-flow stages and connectors use the same green indication as the Contact
Events activity flash and the POS Processing flow. Pending stages remain neutral.

1. Select the four named files in the tab.
   The **Generate** button beside each file picker creates three realistic sample
   rows using the server-provided field definitions, including correctly typed
   dates, numbers, identities, and groups. Generated files use the documented
   primary filename pattern (for example `agentInfo_100825_1750.txt`), are
   automatically saved by the dashboard under the operating system's temporary
   directory in the `service-bus-poc-rostering` subdirectory, can be viewed in
   the file preview, and can also be downloaded as `.txt` files. Set
   `ROSTERING_TEMP_DIR` to override the temporary directory. Selecting an
   uploaded file also enables the same preview; upload and validation behavior is
   unchanged.
2. **Insert into ODS** sends the file contents to `POST /api/rostering/upload`, validates filenames and columns as one batch, and writes valid mapped rows to `RosteringBatches` and `RosteringRows`. Each loaded row retains both its mapped JSON and complete source JSON.
3. **Convert to Lineup XML** calls `POST /api/rostering/:batchId/extract`. Rows are grouped by source file and sorted by source filename and row number.
4. Convert to Lineup XML generates the provisional ACC/RAC roadside contract. The filename is deterministic in the configured timezone (`ROSTERING_TIMEZONE`, default `UTC`):
   `Lineup_RAC_{Start_yyyymmddHHMMSS}_ROADSIDE_{End_yyyymmddHHMMSS}_{nnn}.xml`.
   The API also returns the corresponding zero-byte completion marker name (`.done`); the simulator does not write files.

The ACC physical XML element names are provisional and isolated in `ui/server/rostering/lineup.js`; replace that generator when the canonical contract is supplied. The current mapping joins schedule summary/detail and agent info by `AgentID`, emits schedule/agent rows to RAC units 4000/4001/4002 according to `ROSTERING_UNIT_GROUPS` (JSON, with optional `muIds` and `saGroupIds`), and retains `AgentID`, `AgentName`, `EmployeeID`, and `VehicleID` when present. Only schedule/agent rows whose source `AgentName` contains the required `#` naming marker are emitted. Forecast rows load directly to `dbo.ActiveForecast` and retain their configured `SAGroupID`/`SAGroupName` values; they are not restricted to the RAC unit groups. Detail `ActivityCode` values become activity segments with derived `AVAILABLE`, `BUSY`, `UNAVAILABLE`, or `UNKNOWN` status. Empty filtered lineups are valid. Coordinates are emitted only when `ROSTERING_COORDINATE_FIELDS` is configured as JSON such as `{"x":"XCoord","y":"YCoord"}`; values are copied without latitude/longitude conversion.

The older generic `<lineup>` shape remains available through the existing helper for backwards-compatible tests, but is no longer used by the extraction endpoint.

## Diagnostics

Rostering activity is written as JSON Lines to a separate timestamped log for
each dashboard process. By default the files are created in the repository's
`logs/` directory with names such as
`rostering-2026-10-02T12-00-00-000Z-12345.log`. Entries cover server startup,
generated-file saves, batch validation/load results, and lineup extraction
results or rejection reasons. Full file contents are not written to the log.
Set `ROSTERING_LOG_DIR` to use a different directory.
