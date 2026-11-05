import { diff } from "deep-object-diff";

const contactHistories = new Map();

export function recordContactUpdate(event) {
  const contactId = event.data.contactId;
  if (typeof contactId !== "string" || contactId.trim() === "") return null;
  const history = contactHistories.get(contactId) ?? { snapshot: {}, entries: [] };
  const fields = withoutContactId(event.data);
  const changes = changedFields(history.snapshot, fields);

  if (changes.length > 0) {
    history.entries.unshift({
      eventId: event.id,
      correlationId: event.correlationId,
      source: event.source,
      timestamp: event.timestamp,
      kind: history.entries.length === 0 ? "created" : "updated",
      changes,
    });
  }

  history.snapshot = structuredClone(fields);
  contactHistories.set(contactId, history);
  return changes.length > 0 ? history.entries[0] : null;
}

export function getContactHistory(contactId) {
  const entries = contactHistories.get(contactId)?.entries;
  return entries ? entries.map(maskSensitiveChanges) : null;
}

const SENSITIVE_FIELDS = new Set(["email", "phone"]);
const MASK_CHARACTER = "*";
const MIN_LENGTH_TO_KEEP_ENDS = 3;

function maskSensitiveChanges(entry) {
  return {
    ...entry,
    changes: entry.changes.map((change) => (SENSITIVE_FIELDS.has(change.field)
      ? { ...change, previous: maskValue(change.previous), current: maskValue(change.current) }
      : change)),
  };
}

function maskValue(value) {
  if (value === null || value === undefined) return value;
  const text = String(value);
  if (text.length < MIN_LENGTH_TO_KEEP_ENDS) return MASK_CHARACTER.repeat(text.length);
  return text[0] + MASK_CHARACTER.repeat(text.length - 2) + text[text.length - 1];
}

// contactId selects the history, so it is never reported as a changed field.
function withoutContactId(data) {
  return Object.fromEntries(Object.entries(data).filter(([key]) => key !== "contactId"));
}

function changedFields(previous, current) {
  return leafPaths(diff(previous, current)).map((path) => ({
    field: path.join("."),
    previous: valueAt(previous, path) ?? null,
    current: valueAt(current, path) ?? null,
  }));
}

// deep-object-diff reports removed keys as undefined, so values are read from the snapshots instead.
function leafPaths(node, prefix = []) {
  return Object.entries(node).flatMap(([key, value]) =>
    isPlainObject(value) ? leafPaths(value, [...prefix, key]) : [[...prefix, key]]);
}

function valueAt(source, path) {
  return path.reduce((value, key) => value?.[key], source);
}

function isPlainObject(value) {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
