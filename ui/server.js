import { pathToFileURL } from "node:url";
import { app, logServerInitialized } from "./server/app.js";
import { logRostering } from "./server/rostering/index.js";

const port = Number.parseInt(process.env.PORT ?? "5080", 10);
logServerInitialized(port);

export { app };

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  app.listen(port, () => {
    logRostering("server.started", { port });
    console.log(`Service Bus POC dashboard listening on http://localhost:${port}`);
  });
}
