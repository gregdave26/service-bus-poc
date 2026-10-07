import assert from "node:assert/strict";
import { DatabaseSync } from "node:sqlite";
import test from "node:test";
import express from "express";
import {
  createDigitalSiteRouter,
  createOrder,
  createOrderPlacer,
  createOrderRepository,
  getCatalog,
  isValidRego,
  lookupVehicle,
  normaliseRego,
  priceFor,
  toProductHoldingChange,
  validateOrderRequest,
} from "../server/digitalSite/index.js";
import { createPublishMessage, validatePublishRequest } from "../server/contactEvents/index.js";

const validRequest = Object.freeze({
  isBrokenDown: "No",
  vehicle: { searchChoice: "RegoLookup", rego: "1anurag" },
  coverId: "CLAS",
  paymentPlan: "Monthly",
  acceptedPaymentAuthTerms: true,
  acceptedRoadsideAssistTerms: true,
});

const noopLog = () => {};

function createRepository() {
  return createOrderRepository(new DatabaseSync(":memory:"));
}

async function withServer(router, run) {
  const app = express();
  app.use(express.json());
  app.use(router);
  const server = app.listen(0);
  try {
    await run(`http://127.0.0.1:${server.address().port}`);
  } finally {
    server.closeAllConnections();
    server.close();
  }
}

function postOrder(baseUrl, body) {
  return fetch(`${baseUrl}/api/digital-site/orders`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });
}

test("normalises and validates registrations", () => {
  assert.equal(normaliseRego(" 1abc 123 "), "1ABC123");
  assert.equal(normaliseRego(undefined), "");
  assert.equal(isValidRego("1abc123"), true);
  assert.equal(isValidRego(""), false);
  assert.equal(isValidRego("TOO-LONG-REGO"), false);
});

test("looks up demo vehicles and returns null for unknown registrations", () => {
  assert.equal(lookupVehicle("1 anurag").make, "Toyota");
  assert.equal(lookupVehicle("ZZZ999"), null);
});

test("catalog exposes covers and payment plans but not demo vehicles", () => {
  const catalog = getCatalog();
  assert.deepEqual(catalog.covers.map((cover) => cover.id), ["CLAS", "ULTI", "ULPL", "STD"]);
  assert.deepEqual(catalog.paymentPlans.map((plan) => plan.id), ["Monthly", "Annual"]);
  assert.equal(catalog.vehicles, undefined);
});

test("accepts a complete order request", () => {
  assert.equal(validateOrderRequest(validRequest), null);
  assert.equal(validateOrderRequest({ ...validRequest, vehicle: { searchChoice: "Skip" } }), null);
});

test("rejects invalid order requests with a specific reason", () => {
  const cases = [
    [undefined, /broken down/],
    [{ ...validRequest, isBrokenDown: "Yes" }, /broken down/],
    [{ ...validRequest, vehicle: undefined }, /searchChoice/],
    [{ ...validRequest, vehicle: { searchChoice: "Manual" } }, /searchChoice/],
    [{ ...validRequest, vehicle: { searchChoice: "RegoLookup", rego: "!!" } }, /not a valid registration/],
    [{ ...validRequest, vehicle: { searchChoice: "RegoLookup", rego: "ZZZ999" } }, /not found/],
    [{ ...validRequest, coverId: "GOLD" }, /coverId/],
    [{ ...validRequest, paymentPlan: "Weekly" }, /paymentPlan/],
    [{ ...validRequest, acceptedPaymentAuthTerms: "true" }, /authorisation terms/],
    [{ ...validRequest, acceptedRoadsideAssistTerms: false }, /Roadside Assistance terms/],
  ];
  for (const [request, expected] of cases) assert.match(validateOrderRequest(request), expected);
});

test("prices orders from the catalog for each payment plan", () => {
  const classic = getCatalog().covers.find((cover) => cover.id === "CLAS");
  assert.deepEqual(priceFor(classic, "Monthly"), { instalmentAmount: 28.55, frequency: "monthly", totalAnnualCost: 345.6 });
  assert.deepEqual(priceFor(classic, "Annual"), { instalmentAmount: 310, frequency: "annual", totalAnnualCost: 310 });
});

test("creates an order ignoring any client supplied price", () => {
  const order = createOrder({ ...validRequest, price: { instalmentAmount: 0.01 } }, {
    sequence: 42,
    now: new Date("2026-10-07T00:00:00.000Z"),
    createContactId: () => "contact-1",
  });
  assert.equal(order.orderId, "RSA-000042");
  assert.equal(order.contactId, "contact-1");
  assert.equal(order.createdAt, "2026-10-07T00:00:00.000Z");
  assert.equal(order.price.instalmentAmount, 28.55);
  assert.equal(order.vehicle.rego, "1ANURAG");
  assert.equal(order.publishStatus, "pending");
  assert.equal(createOrder({ ...validRequest, vehicle: { searchChoice: "Skip" } }, { sequence: 1 }).vehicle, null);
});

test("maps an order to a valid ProductHoldingChange publish request", () => {
  const order = createOrder(validRequest, { sequence: 7, createContactId: () => "contact-7" });
  const request = toProductHoldingChange(order);
  assert.equal(validatePublishRequest(request), null);
  const message = createPublishMessage(request);
  assert.equal(message.subject, "ProductHoldingChange");
  assert.deepEqual(message.body.data, {
    contactId: "contact-7",
    holdingId: "RSA-000007",
    productType: "roadside-assistance",
    action: "created",
    holdingData: {
      channel: "digital-site",
      coverId: "CLAS",
      coverName: "Classic",
      paymentPlan: "Monthly",
      instalmentAmount: 28.55,
      totalAnnualCost: 345.6,
      vehicleRego: "1ANURAG",
    },
  });
});

test("publish message drops non-object values for object fields", () => {
  const message = createPublishMessage({ ...toProductHoldingChange(createOrder(validRequest, { sequence: 1 })), holdingData: "text" });
  assert.equal("holdingData" in message.body.data, false);
});

test("repository stores, lists newest first and updates publish status", () => {
  const repository = createRepository();
  assert.equal(repository.nextSequenceNumber(), 1);
  const first = createOrder(validRequest, { sequence: 1 });
  repository.insertOrder(first, 1);
  const second = createOrder({ ...validRequest, vehicle: { searchChoice: "Skip" } }, { sequence: 2 });
  repository.insertOrder(second, 2);
  assert.equal(repository.nextSequenceNumber(), 3);

  repository.updatePublishStatus(first.orderId, { publishStatus: "published", eventId: "event-1" });
  assert.deepEqual(repository.listOrders().map((order) => order.orderId), ["RSA-000002", "RSA-000001"]);
  assert.equal(repository.getOrderById(first.orderId).publishStatus, "published");
  assert.equal(repository.getOrderById(first.orderId).eventId, "event-1");
  assert.equal(repository.getOrderById("missing"), null);
});

test("placing an order persists it and records a successful publish", async () => {
  const repository = createRepository();
  const published = [];
  const placeOrder = createOrderPlacer({ repository, publish: async (request) => { published.push(request); return { id: "event-1" }; }, log: noopLog });
  const order = await placeOrder(validRequest);
  assert.equal(order.publishStatus, "published");
  assert.equal(order.eventId, "event-1");
  assert.equal(published[0].holdingId, order.orderId);
});

test("placing an order keeps the order when publishing fails", async () => {
  const repository = createRepository();
  const placeOrder = createOrderPlacer({ repository, publish: async () => { throw new Error("Service Bus is not configured."); }, log: noopLog });
  const order = await placeOrder(validRequest);
  assert.equal(order.publishStatus, "publish_failed");
  assert.equal(order.publishError, "Service Bus is not configured.");
  assert.equal(repository.listOrders().length, 1);
});

test("digital site API serves catalog, vehicle lookup and orders", async () => {
  const router = createDigitalSiteRouter({ repository: createRepository(), publish: async () => ({ id: "event-api" }), log: noopLog });
  await withServer(router, async (baseUrl) => {
    assert.equal((await (await fetch(`${baseUrl}/api/digital-site/catalog`)).json()).covers.length, 4);
    assert.equal((await (await fetch(`${baseUrl}/api/digital-site/vehicles/1abc123`)).json()).model, "CX-5");
    assert.equal((await fetch(`${baseUrl}/api/digital-site/vehicles/ZZZ999`)).status, 404);
    assert.equal((await fetch(`${baseUrl}/api/digital-site/vehicles/${encodeURIComponent("@@")}`)).status, 400);

    const rejected = await postOrder(baseUrl, { ...validRequest, coverId: "nope" });
    assert.equal(rejected.status, 400);
    assert.match((await rejected.json()).error, /coverId/);

    const created = await postOrder(baseUrl, validRequest);
    assert.equal(created.status, 201);
    const order = await created.json();
    assert.equal(order.publishStatus, "published");

    assert.equal((await (await fetch(`${baseUrl}/api/digital-site/orders`)).json())[0].orderId, order.orderId);
    assert.equal((await (await fetch(`${baseUrl}/api/digital-site/orders/${order.orderId}`)).json()).eventId, "event-api");
    assert.equal((await fetch(`${baseUrl}/api/digital-site/orders/missing`)).status, 404);
  });
});

test("digital site API returns 500 when the order cannot be stored", async () => {
  const repository = { ...createRepository(), insertOrder() { throw new Error("disk full"); } };
  const router = createDigitalSiteRouter({ repository, publish: async () => ({ id: "unused" }), log: noopLog });
  const originalConsoleError = console.error;
  console.error = () => {};
  try {
    await withServer(router, async (baseUrl) => {
      const response = await postOrder(baseUrl, validRequest);
      assert.equal(response.status, 500);
      assert.equal((await response.json()).error, "Failed to place order");
    });
  } finally {
    console.error = originalConsoleError;
  }
});
