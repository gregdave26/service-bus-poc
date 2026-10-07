import assert from "node:assert/strict";
import test from "node:test";
import { createDigitalSiteApi } from "../src/digitalSiteApi.js";

function recordingFetch(responses) {
  const calls = [];
  const fetchImpl = async (url, options) => {
    calls.push({ url, options });
    const { status = 200, body } = responses.shift() ?? {};
    return {
      status,
      ok: status >= 200 && status < 300,
      json: async () => (body === undefined ? Promise.reject(new SyntaxError("no body")) : body),
    };
  };
  return { calls, fetchImpl };
}

test("builds CommerceApi routes from the configured base URL", async () => {
  const { calls, fetchImpl } = recordingFetch(Array.from({ length: 11 }, () => ({ body: {} })));
  const api = createDigitalSiteApi("http://localhost:5200/", fetchImpl);

  await api.getConfig();
  await api.getCatalog();
  await api.lookupVehicle(" 1ABC 123 ");
  await api.getActiveCarts("CRM-00000001");
  await api.getHistory("CRM-00000001");
  await api.createCart({ coverId: "RSA-STANDARD" });
  await api.updateCart("cart/1", { version: 1 });
  await api.deleteCart({ id: "cart-1", version: 3 }, "CRM-00000001");
  await api.startCheckout({ cartId: "cart-1" });
  await api.getCheckoutStatus("payment-1");
  await api.simulateStubPayment("payment-1", "Authorised");

  const root = "http://localhost:5200/api/digital-site";
  assert.deepEqual(calls.map(({ url, options }) => `${options.method} ${url.replace(root, "")}`), [
    "GET /config",
    "GET /catalog",
    "GET /vehicles/1ABC%20123",
    "GET /members/CRM-00000001/carts",
    "GET /members/CRM-00000001/history",
    "POST /carts",
    "PUT /carts/cart%2F1",
    "DELETE /carts/cart-1?crmId=CRM-00000001&version=3",
    "POST /checkout/session",
    "GET /checkout/status/payment-1",
    "POST /checkout/stub/payment-1/notify",
  ]);
  assert.equal(calls[0].options.body, undefined);
  assert.deepEqual(calls[5].options.headers, { "Content-Type": "application/json" });
  assert.equal(calls[10].options.body, JSON.stringify({ outcome: "Authorised" }));
});

test("returns null for no-content responses", async () => {
  const { fetchImpl } = recordingFetch([{ status: 204 }]);
  assert.equal(await createDigitalSiteApi("http://api", fetchImpl).deleteCart({ id: "c", version: 1 }, "CRM-00000001"), null);
});

test("surfaces the API error message, or the status when there is none", async () => {
  const { fetchImpl } = recordingFetch([{ status: 409, body: { error: "This cart changed" } }, { status: 502 }]);
  const api = createDigitalSiteApi("http://api", fetchImpl);
  await assert.rejects(api.getCatalog(), /This cart changed/);
  await assert.rejects(api.getCatalog(), /Request failed \(502\)/);
});

test("explains when the API cannot be reached", async () => {
  const api = createDigitalSiteApi("http://localhost:5200", async () => { throw new TypeError("fetch failed"); });
  await assert.rejects(api.getCatalog(), /not reachable at http:\/\/localhost:5200/);
});
