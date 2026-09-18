import express from "express";
import { DefaultAzureCredential } from "@azure/identity";
import { ServiceBusClient } from "@azure/service-bus";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { randomUUID } from "node:crypto";
import net from "node:net";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const app = express();
const port = Number.parseInt(process.env.PORT ?? "5080", 10);
const heartbeatTimeoutMs = 15_000;
const heartbeats = new Map();

app.use(express.json({ limit: "32kb" }));
app.use(express.static(path.join(__dirname, "public")));

function getServiceStatuses() {
  const now = Date.now();

  return [...heartbeats.values()].map((heartbeat) => ({
    ...heartbeat,
    state:
      now - Date.parse(heartbeat.sentAt) < heartbeatTimeoutMs
        ? heartbeat.state
        : "Offline",
  }));
}

function getEmulatorStatus() {
  const host = process.env.ServiceBus__EmulatorHost ?? "127.0.0.1";
  const port = Number.parseInt(process.env.ServiceBus__EmulatorPort ?? "5672", 10);

  return new Promise((resolve) => {
    const socket = net.createConnection({ host, port });
    const finish = (running, error) => {
      socket.destroy();
      resolve({
        running,
        host,
        port,
        checkedAt: new Date().toISOString(),
        error: error ?? null,
      });
    };

    socket.setTimeout(1500, () => finish(false, "Connection timed out"));
    socket.once("connect", () => finish(true));
    socket.once("error", (error) => finish(false, error.code ?? error.message));
  });
}

function validatePublishRequest(body) {
  const requiredFields = ["contactId", "firstName", "lastName", "phone", "email"];
  const missingFields = requiredFields.filter(
    (field) => typeof body?.[field] !== "string" || body[field].trim() === "",
  );

  if (missingFields.length > 0) {
    return `Missing required fields: ${missingFields.join(", ")}`;
  }

  return null;
}

async function publishContactEvent(request) {
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
  const event = {
    id: randomUUID(),
    type: "ContactUpdated",
    source: "dashboard",
    timestamp: new Date().toISOString(),
    dataVersion: "1.0",
    correlationId: randomUUID(),
    data: {
      contactId: request.contactId.trim(),
      firstName: request.firstName.trim(),
      lastName: request.lastName.trim(),
      phone: request.phone.trim(),
      email: request.email.trim(),
      attributes: {
        hasInsurance: Boolean(request.hasInsurance),
        hasParksResorts: Boolean(request.hasParksResorts),
        hasCarwashProduct: Boolean(request.hasCarwashProduct),
      },
    },
  };

  try {
    await sender.sendMessages({ body: event, contentType: "application/json" });
    return event.id;
  } finally {
    await sender.close();
    await client.close();
  }
}

app.post("/api/heartbeat", (request, response) => {
  const heartbeat = request.body;

  if (!heartbeat?.serviceName || !heartbeat?.state || !heartbeat?.sentAt) {
    return response.status(400).json({ error: "serviceName, state, and sentAt are required" });
  }

  heartbeats.set(heartbeat.serviceName, {
    serviceName: heartbeat.serviceName,
    subscriptionName: heartbeat.subscriptionName ?? null,
    state: heartbeat.state,
    sentAt: heartbeat.sentAt,
    messagesHandled: heartbeat.messagesHandled ?? 0,
    lastMessageAt: heartbeat.lastMessageAt ?? null,
    lastEventId: heartbeat.lastEventId ?? null,
  });

  return response.status(204).send();
});

app.get("/api/status", (_request, response) => response.json(getServiceStatuses()));

app.get("/api/emulator-status", async (_request, response) => {
  response.json(await getEmulatorStatus());
});

app.post("/api/publish", async (request, response) => {
  const validationError = validatePublishRequest(request.body);
  if (validationError) {
    return response.status(400).json({ success: false, error: validationError });
  }

  try {
    const eventId = await publishContactEvent(request.body);
    return response.json({ success: true, eventId });
  } catch (error) {
    console.error("Failed to publish event from dashboard", error);
    return response.status(502).json({
      success: false,
      error: error instanceof Error ? error.message : "Failed to publish event",
    });
  }
});

export { app, getEmulatorStatus, getServiceStatuses, validatePublishRequest };

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  app.listen(port, () => {
    console.log(`Service Bus POC dashboard listening on http://localhost:${port}`);
  });
}
