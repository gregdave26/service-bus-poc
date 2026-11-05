import assert from "node:assert/strict";
import test from "node:test";
import { app } from "../server/app.js";
import { getContactHistory, recordContactUpdate } from "../server/contactEvents/index.js";

let eventSequence = 0;

function contactEvent(contactId, data = {}, overrides = {}) {
  eventSequence += 1;
  return {
    id: `event-${eventSequence}`,
    type: "ContactUpdated",
    source: "dashboard",
    timestamp: new Date(Date.UTC(2026, 10, 5, 2, eventSequence)).toISOString(),
    dataVersion: "1.0",
    correlationId: `correlation-${eventSequence}`,
    data: {
      contactId,
      firstName: "Ada",
      lastName: "Lovelace",
      email: "ada@example.com",
      phone: "0400000000",
      attributes: { hasInsurance: false, hasParksResorts: false, hasCarwashProduct: false },
      ...data,
    },
    ...overrides,
  };
}

function uniqueContactId() {
  return `history-contact-${Math.random().toString(16).slice(2)}`;
}

async function withServer(callback) {
  const server = app.listen(0);
  try {
    const address = server.address();
    return await callback(`http://127.0.0.1:${address.port}`);
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
}

test("records every populated field as created for the first update of a contact", () => {
  const contactId = uniqueContactId();

  const entry = recordContactUpdate(contactEvent(contactId));

  assert.equal(entry.kind, "created");
  assert.deepEqual(
    entry.changes.map((change) => change.field).sort(),
    ["attributes.hasCarwashProduct", "attributes.hasInsurance", "attributes.hasParksResorts", "email", "firstName", "lastName", "phone"],
  );
  assert.deepEqual(entry.changes.find((change) => change.field === "email"), {
    field: "email",
    previous: null,
    current: "ada@example.com",
  });
});

test("records only the fields that changed since the previous snapshot", () => {
  const contactId = uniqueContactId();
  recordContactUpdate(contactEvent(contactId));

  const entry = recordContactUpdate(contactEvent(contactId, {
    email: "ada.new@example.com",
    attributes: { hasInsurance: true, hasParksResorts: false, hasCarwashProduct: false },
  }));

  assert.equal(entry.kind, "updated");
  assert.deepEqual(entry.changes.map((change) => change.field).sort(), ["attributes.hasInsurance", "email"]);
  assert.deepEqual(entry.changes.find((change) => change.field === "email"), {
    field: "email",
    previous: "ada@example.com",
    current: "ada.new@example.com",
  });
  assert.deepEqual(entry.changes.find((change) => change.field === "attributes.hasInsurance"), {
    field: "attributes.hasInsurance",
    previous: false,
    current: true,
  });
});

test("records a removed value with a null current value", () => {
  const contactId = uniqueContactId();
  recordContactUpdate(contactEvent(contactId));
  const withoutPhone = contactEvent(contactId);
  delete withoutPhone.data.phone;

  const entry = recordContactUpdate(withoutPhone);

  assert.deepEqual(entry.changes, [{ field: "phone", previous: "0400000000", current: null }]);
});

test("does not record an entry when a republished snapshot is unchanged", () => {
  const contactId = uniqueContactId();
  recordContactUpdate(contactEvent(contactId));

  assert.equal(recordContactUpdate(contactEvent(contactId)), null);
  assert.equal(getContactHistory(contactId).length, 1);
});

test("returns history newest first with event and correlation identifiers", () => {
  const contactId = uniqueContactId();
  const created = contactEvent(contactId);
  const emailChange = contactEvent(contactId, { email: "first@example.com" });
  const phoneChange = contactEvent(contactId, { email: "first@example.com", phone: "0411111111" });
  recordContactUpdate(created);
  recordContactUpdate(emailChange);
  recordContactUpdate(phoneChange);

  const entries = getContactHistory(contactId);

  assert.deepEqual(entries.map((entry) => entry.kind), ["updated", "updated", "created"]);
  assert.equal(entries[0].eventId, phoneChange.id);
  assert.equal(entries[0].correlationId, phoneChange.correlationId);
  assert.equal(entries[0].source, "dashboard");
  assert.equal(entries[0].timestamp, phoneChange.timestamp);
});

test("returns null for a contact with no recorded updates", () => {
  assert.equal(getContactHistory(uniqueContactId()), null);
});

test("GET /api/contacts/:contactId/history returns the contact's history", async () => {
  const contactId = uniqueContactId();
  recordContactUpdate(contactEvent(contactId));
  recordContactUpdate(contactEvent(contactId, { lastName: "Byron" }));

  await withServer(async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/contacts/${encodeURIComponent(contactId)}/history`);
    const body = await response.json();

    assert.equal(response.status, 200);
    assert.equal(body.contactId, contactId);
    assert.equal(body.entries.length, 2);
    assert.deepEqual(body.entries[0].changes, [{ field: "lastName", previous: "Lovelace", current: "Byron" }]);
  });
});

test("GET /api/contacts/:contactId/history returns 404 for an unknown contact", async () => {
  await withServer(async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/contacts/${encodeURIComponent(uniqueContactId())}/history`);

    assert.equal(response.status, 404);
    assert.equal((await response.json()).error, "No history recorded for this contact");
  });
});

test("GET /api/contacts/:contactId/history rejects a blank contact identifier", async () => {
  await withServer(async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/contacts/%20%20/history`);

    assert.equal(response.status, 400);
    assert.equal((await response.json()).error, "contactId is required");
  });
});
