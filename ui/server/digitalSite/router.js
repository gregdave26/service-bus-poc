import express from "express";
import { publishAndRecordContactEvent } from "../contactEvents/index.js";
import { getCatalog } from "./catalog.js";
import { rsaOrders } from "./database.js";
import { logDigitalSite } from "./log.js";
import { createOrderPlacer, validateOrderRequest } from "./orderService.js";
import { isValidRego, lookupVehicle } from "./vehicleLookup.js";

export function createDigitalSiteRouter({ repository, publish, log = logDigitalSite }) {
  const router = express.Router();
  const placeOrder = createOrderPlacer({ repository, publish, log });

  router.get("/api/digital-site/catalog", (_request, response) => response.json(getCatalog()));

  router.get("/api/digital-site/vehicles/:rego", (request, response) => {
    if (!isValidRego(request.params.rego)) return response.status(400).json({ error: "Enter a valid registration" });
    const vehicle = lookupVehicle(request.params.rego);
    return vehicle ? response.json(vehicle) : response.status(404).json({ error: "We couldn't find that vehicle" });
  });

  router.post("/api/digital-site/orders", async (request, response) => {
    const validationError = validateOrderRequest(request.body);
    if (validationError) {
      log("order.rejected", { reason: validationError });
      return response.status(400).json({ error: validationError });
    }

    try {
      return response.status(201).json(await placeOrder(request.body));
    } catch (error) {
      log("order.failed", { error: error instanceof Error ? error.message : String(error) });
      console.error("Failed to place Roadside Assistance order", error);
      return response.status(500).json({ error: "Failed to place order" });
    }
  });

  router.get("/api/digital-site/orders", (_request, response) => response.json(repository.listOrders()));

  router.get("/api/digital-site/orders/:id", (request, response) => {
    const order = repository.getOrderById(request.params.id);
    return order ? response.json(order) : response.status(404).json({ error: "Order not found" });
  });

  return router;
}

export const digitalSiteRouter = createDigitalSiteRouter({ repository: rsaOrders, publish: publishAndRecordContactEvent });
