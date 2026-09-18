import assert from "node:assert/strict";
import test from "node:test";
import { getEmulatorStatus, getServiceStatuses, validatePublishRequest } from "../server.js";

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

test("returns an empty status list when no heartbeats have been received", () => {
  assert.deepEqual(getServiceStatuses(), []);
});

test("reports emulator status with its AMQP endpoint", async () => {
  const status = await getEmulatorStatus();
  assert.equal(typeof status.running, "boolean");
  assert.equal(status.port, 5672);
  assert.match(status.host, /127\.0\.0\.1/);
});
