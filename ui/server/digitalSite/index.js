export const digitalSiteClientConfig = {
  digitalSiteApiBaseUrl: process.env.DIGITAL_SITE_API_BASE_URL ?? "http://localhost:5200",
};

export { createDigitalSiteRouter, digitalSiteRouter } from "./router.js";
export { getCatalog } from "./catalog.js";
export { createOrderRepository } from "./orderRepository.js";
export { createOrder, createOrderPlacer, priceFor, toProductHoldingChange, validateOrderRequest } from "./orderService.js";
export { isValidRego, lookupVehicle, normaliseRego } from "./vehicleLookup.js";
export { logDigitalSite } from "./log.js";
