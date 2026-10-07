import express from "express";
import { logContact } from "./log.js";
import { getEmulatorStatus } from "./emulatorStatus.js";
import { messageTypes, subscriberLabels, producerLabels } from "./messageTypes.js";
import { createPublishMessage, publishContactEvent, validatePublishRequest } from "./publisher.js";
import {
  getMessages,
  getServiceStatuses,
  messageHistory,
  recordHeartbeat,
  storeMessage,
  validateDashboardMessage,
} from "./messageStore.js";

export const contactEventsClientConfig = { subscriberLabels, producerLabels, messageTypes };

export const contactEventsRouter = express.Router();
const router = contactEventsRouter;

router.post("/api/heartbeat", (request, response) => {
  const heartbeat = request.body;

  if (!heartbeat?.serviceName || !heartbeat?.state || !heartbeat?.sentAt) {
    return response.status(400).json({ error: "serviceName, state, and sentAt are required" });
  }

  recordHeartbeat(heartbeat);
  return response.status(204).send();
});

router.get("/api/status", (_request, response) => response.json(getServiceStatuses()));

router.get("/api/emulator-status", async (_request, response) => {
  response.json(await getEmulatorStatus());
});

router.post("/api/publish", async (request, response) => {
  const validationError = validatePublishRequest(request.body);
  if (validationError) {
    logContact("publish.rejected", { reason: validationError, type: request.body?.type ?? null });
    return response.status(400).json({ success: false, error: validationError });
  }

  try {
    const event = await publishContactEvent(request.body);
    storeMessage({
      messageId: event.id,
      eventId: event.id,
      serviceName: "producer",
      direction: "sent",
      timestamp: event.timestamp,
      payload: JSON.stringify(event),
    });
    logContact("publish.completed", { eventId: event.id, type: event.type });
    return response.json({ success: true, eventId: event.id });
  } catch (error) {
    logContact("publish.failed", { error: error instanceof Error ? error.message : String(error) });
    console.error("Failed to publish event from dashboard", error);
    return response.status(502).json({
      success: false,
      error: error instanceof Error ? error.message : "Failed to publish event",
    });
  }
});

router.post("/api/messages", (request, response) => {
  const validationError = validateDashboardMessage(request.body);
  if (validationError) {
    logContact("message.rejected", { reason: validationError });
    return response.status(400).json({ error: validationError });
  }
  return response.status(201).json(storeMessage(request.body));
});

router.get("/api/messages", (request, response) => {
  return response.json(getMessages(request.query.service, request.query.direction));
});

router.get("/api/messages/:id", (request, response) => {
  const message = messageHistory.find((candidate) => candidate.messageId === request.params.id);
  return message ? response.json(message) : response.status(404).json({ error: "Message not found" });
});
