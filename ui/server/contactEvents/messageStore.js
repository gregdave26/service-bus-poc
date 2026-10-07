import { logContact } from "./log.js";

const heartbeatTimeoutMs = 15_000;
const heartbeats = new Map();
export const messageHistory = [];
const maxMessageHistory = 500;

export function recordHeartbeat(heartbeat) {
  heartbeats.set(heartbeat.serviceName, {
    serviceName: heartbeat.serviceName,
    subscriptionName: heartbeat.subscriptionName ?? null,
    state: heartbeat.state,
    sentAt: heartbeat.sentAt,
    messagesHandled: heartbeat.messagesHandled ?? 0,
    lastMessageAt: heartbeat.lastMessageAt ?? null,
    lastEventId: heartbeat.lastEventId ?? null,
  });
  logContact("heartbeat.received", {
    serviceName: heartbeat.serviceName,
    state: heartbeat.state,
    messagesHandled: heartbeat.messagesHandled ?? 0,
  });
}

export function getServiceStatuses() {
  const now = Date.now();

  return [...heartbeats.values()].map((heartbeat) => ({
    ...heartbeat,
    state:
      now - Date.parse(heartbeat.sentAt) < heartbeatTimeoutMs
        ? heartbeat.state
        : "Offline",
  }));
}

export function getMessages(service, direction) {
  return messageHistory.filter((message) =>
    (!service || message.serviceName === service) &&
    (!direction || message.direction === direction));
}

export function validateDashboardMessage(body) {
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

export function storeMessage(message) {
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
  logContact("message.stored", {
    messageId: message.messageId,
    serviceName: message.serviceName,
    direction: message.direction,
    subscriptionName: message.subscriptionName ?? null,
  });
  return messageHistory[0];
}
