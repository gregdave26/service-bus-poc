import { createFeatureLogger } from "../shared/featureLogger.js";

export const logDigitalSite = createFeatureLogger({
  label: "Digital Site",
  filePrefix: "digital-site",
  directoryEnvironmentVariable: "DIGITAL_SITE_LOG_DIR",
});
