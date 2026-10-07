import { createFeatureLogger } from "../shared/featureLogger.js";

export const logContact = createFeatureLogger({
  label: "Contact Events",
  filePrefix: "contact",
  directoryEnvironmentVariable: "CONTACT_LOG_DIR",
});
