import { DefaultAzureCredential } from "@azure/identity";
import { ServiceBusClient } from "@azure/service-bus";
import { randomUUID } from "node:crypto";
import { messageTypes } from "./messageTypes.js";
import { storeMessage } from "./messageStore.js";

export function validatePublishRequest(body) {
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

function isPlainObject(value) {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function createPublishMessage(request) {
  const type = request.type ?? "ContactUpdated";
  const configuredType = messageTypes[type];
  const data = {};
  for (const field of configuredType?.Fields ?? []) {
    const value = request[field.Name];
    if (field.Type === "boolean") data[field.Name] = value === true;
    else if (field.Type === "object") { if (isPlainObject(value)) data[field.Name] = value; }
    else if (value !== undefined) data[field.Name] = String(value).trim();
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

export async function publishContactEvent(request) {
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

export async function publishAndRecordContactEvent(request) {
  const event = await publishContactEvent(request);
  storeMessage({
    messageId: event.id,
    eventId: event.id,
    serviceName: "producer",
    direction: "sent",
    timestamp: event.timestamp,
    payload: JSON.stringify(event),
  });
  return event;
}
