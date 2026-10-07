import { createFeatureLogger } from "../shared/featureLogger.js";

export const logRostering = createFeatureLogger({
  label: "Rostering",
  filePrefix: "rostering",
  directoryEnvironmentVariable: "ROSTERING_LOG_DIR",
});
