function createSampleAgents() {
  const sampleSeed = 1000 + Math.floor(Math.random() * 8999);
  return [
    ["Alex", "Morgan", "4000", "Metro North"],
    ["Jordan", "Lee", "4001", "Metro South"],
    ["Taylor", "Singh", "4002", "Regional West"],
  ].map(([first, last, muId, muName], index) => {
    const number = sampleSeed + index;
    const logon = `${first[0].toLowerCase()}${last.toLowerCase()}`;
    return { id: `AG${number}`, name: `${first} ${last} #${index + 1}`, logon, employee: `EMP${number}`, first, last, muId, muName };
  });
}

const pad = (value) => String(value).padStart(2, "0");
const dateParts = (now) => ({
  date: `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`,
  stamp: `${String(now.getFullYear()).slice(-2)}${pad(now.getMonth() + 1)}${pad(now.getDate())}_${pad(now.getHours())}${pad(now.getMinutes())}`,
});

export function rosterFilename(inputName, now = new Date()) {
  return `${inputName}_${dateParts(now).stamp}.txt`;
}

function valueFor(field, agent, index, now) {
  const { date } = dateParts(now);
  const start = index % 2 === 0 ? "08:00" : "09:00";
  const end = index % 2 === 0 ? "16:30" : "17:30";
  const values = {
    AgentID: agent.id, AgentName: agent.name, LogonID: agent.logon, EmployeeID: agent.employee,
    MUId: agent.muId, MUName: agent.muName, FirstName: agent.first, LastName: agent.last,
    ScheduleDate: date, ForecastDate: date, StartDateTime: `${date} ${start}`,
    EndDateTime: `${date} ${end}`, IntervalStartDateTime: `${date} ${pad(8 + index)}:00`,
    IntervalEndDateTime: `${date} ${pad(8 + index)}:30`, StartDate: "2021-01-15", EndDate: "",
    ScheduledMinutes: 510, PaidMinutes: 480,
    DurationMinutes: 30, ActivityCode: index % 2 === 0 ? "AVAILABLE" : "BUSY",
    ActivityDescription: index % 2 === 0 ? "Available for contacts" : "Customer contacts",
    SAGroupID: agent.muId, SAGroupName: `${agent.muName} queue`,
    ContactsOffered: 18 + index * 7, AverageHandleTime: 295 + index * 18,
    RequiredAgents: 3 + index, ServiceLevel: [0.8, 0.85, 0.9][index],
    EmailAddress: `${agent.logon}@example.test`, TimeOffGroup: "Standard",
  };
  if (field.name === "EndDate") values.EndDate = "2099-12-31";
  const value = values[field.name] ?? (field.type === "numeric" ? 0 : field.type === "date" ? date : `${field.name}-${index + 1}`);
  return String(value);
}

function escapeCell(value) {
  const text = String(value ?? "");
  return /[|"\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}

export function generateRosterFile(inputName, definition, now = new Date()) {
  const fields = definition?.fields ?? [];
  const headers = fields.map((field) => field.name);
  const content = [headers, ...createSampleAgents().map((agent, index) => fields.map((field) => escapeCell(valueFor(field, agent, index, now))))]
    .map((row) => row.join("|"))
    .join("\n") + "\n";
  return { name: rosterFilename(inputName, now), content, type: "text/plain" };
}
