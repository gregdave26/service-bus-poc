const JSON_HEADERS = { "Content-Type": "application/json" };

export function createDigitalSiteApi(baseUrl, fetchImpl = (...args) => fetch(...args)) {
  const root = `${baseUrl.replace(/\/+$/, "")}/api/digital-site`;

  async function request(path, { method = "GET", body } = {}) {
    let response;
    try {
      response = await fetchImpl(`${root}${path}`, body === undefined ? { method } : { method, headers: JSON_HEADERS, body: JSON.stringify(body) });
    } catch {
      throw new Error(`The Digital Site commerce API is not reachable at ${baseUrl}. Is it running?`);
    }
    if (response.status === 204) return null;
    const payload = await response.json().catch(() => null);
    if (!response.ok) throw new Error(payload?.error ?? `Request failed (${response.status})`);
    return payload;
  }

  const segment = encodeURIComponent;
  return {
    getConfig: () => request("/config"),
    getCatalog: () => request("/catalog"),
    lookupVehicle: (rego) => request(`/vehicles/${segment(rego.trim())}`),
    getActiveCarts: (crmId) => request(`/members/${segment(crmId)}/carts`),
    getHistory: (crmId) => request(`/members/${segment(crmId)}/history`),
    createCart: (cartRequest) => request("/carts", { method: "POST", body: cartRequest }),
    updateCart: (cartId, cartRequest) => request(`/carts/${segment(cartId)}`, { method: "PUT", body: cartRequest }),
    deleteCart: (cart, crmId) => request(`/carts/${segment(cart.id)}?crmId=${segment(crmId)}&version=${cart.version}`, { method: "DELETE" }),
    startCheckout: (checkoutRequest) => request("/checkout/session", { method: "POST", body: checkoutRequest }),
    getCheckoutStatus: (paymentId) => request(`/checkout/status/${segment(paymentId)}`),
    simulateStubPayment: (paymentId, outcome) => request(`/checkout/stub/${segment(paymentId)}/notify`, { method: "POST", body: { outcome } }),
  };
}
