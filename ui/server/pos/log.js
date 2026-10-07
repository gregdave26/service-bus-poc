import { createFeatureLogger } from "../shared/featureLogger.js";

export const logPos = createFeatureLogger({
  label: "POS",
  filePrefix: "pos",
  directoryEnvironmentVariable: "POS_LOG_DIR",
});
