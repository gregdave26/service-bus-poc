import { appendFileSync, mkdirSync } from "node:fs";
import path from "node:path";
import { repositoryRoot } from "./paths.js";

export function createFeatureLogger({ label, filePrefix, directoryEnvironmentVariable }) {
  const directory = process.env[directoryEnvironmentVariable] ?? path.resolve(repositoryRoot, "logs");
  mkdirSync(directory, { recursive: true });
  const logPath = path.join(directory, `${filePrefix}-${new Date().toISOString().replace(/[:.]/g, "-")}-${process.pid}.log`);

  function log(event, details = {}) {
    const entry = { timestamp: new Date().toISOString(), event, ...details };
    try {
      appendFileSync(logPath, `${JSON.stringify(entry)}\n`, "utf8");
    } catch (error) {
      console.error(`Failed to write ${label} log`, error);
    }
  }

  log.logPath = logPath;
  return log;
}
