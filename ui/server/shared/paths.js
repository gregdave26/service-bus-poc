import path from "node:path";
import { fileURLToPath } from "node:url";

const sharedDirectory = path.dirname(fileURLToPath(import.meta.url));

export const uiRoot = path.resolve(sharedDirectory, "..", "..");
export const repositoryRoot = path.resolve(uiRoot, "..");
