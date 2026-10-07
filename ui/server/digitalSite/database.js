import { mkdirSync } from "node:fs";
import { DatabaseSync } from "node:sqlite";
import path from "node:path";
import { uiRoot } from "../shared/paths.js";
import { createOrderRepository } from "./orderRepository.js";

const digitalSiteDbPath = process.env.DIGITAL_SITE_DB_PATH ?? path.join(uiRoot, "data", "digital-site.db");
mkdirSync(path.dirname(digitalSiteDbPath), { recursive: true });
const digitalSiteDb = new DatabaseSync(digitalSiteDbPath);
const rsaOrders = createOrderRepository(digitalSiteDb);

export { digitalSiteDb, rsaOrders };
