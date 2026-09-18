import assert from "node:assert/strict";
import test from "node:test";
import {
  getEmulatorStatus,
  getMessages,
  getServiceStatuses,
  messageHistory,
  storeMessage,
  createPublishMessage,
  validateDashboardMessage,
  validatePublishRequest,
} from "../server.js";

test("validates all required publish fields", () => {
  assert.match(validatePublishRequest({}), /contactId, firstName, lastName, phone, email/);
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
