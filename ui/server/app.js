import express from "express";
import path from "node:path";
import { contactEventsClientConfig, contactEventsRouter, logContact } from "./contactEvents/index.js";
import { digitalSiteClientConfig } from "./digitalSite/index.js";
import { logPos, posRouter } from "./pos/index.js";
import { logRostering, rosteringClientConfig, rosteringRouter } from "./rostering/index.js";
import { uiRoot } from "./shared/paths.js";

const noStore = { setHeaders: (response) => response.setHeader("Cache-Control", "no-store") };
const noStoreApi = (_request, response, next) => {
  response.setHeader("Cache-Control", "no-store");
  next();
};

// To add a feature tab: create server/<feature>/ exporting a router and mount it below.
export function createApp() {
  const app = express();
  app.use(express.json({ limit: "4mb" }));
  app.use("/api", noStoreApi);
  app.use(express.static(path.join(uiRoot, "public", "dist"), noStore));
  app.use(express.static(path.join(uiRoot, "public"), noStore));

  app.get("/api/config", (_request, response) => response.json({
    ...contactEventsClientConfig,
    ...rosteringClientConfig,
    ...digitalSiteClientConfig,
  }));

  app.use(contactEventsRouter);
  app.use(posRouter);
  app.use(rosteringRouter);
  app.use(digitalSiteRouter);
  return app;
}

export const app = createApp();

export function logServerInitialized(port) {
  logRostering("server.initialized", { port, logPath: logRostering.logPath });
  logPos("server.initialized", { port, logPath: logPos.logPath });
  logContact("server.initialized", { port, logPath: logContact.logPath });
  logDigitalSite("server.initialized", { port, logPath: logDigitalSite.logPath });
}
