import React from "react";
import Alert from "@mui/material/Alert";

export function PaymentGatewayNotice({ config }) {
  if (!config?.fallbackActive) return null;

  return React.createElement(Alert, { severity: "warning", role: "alert" },
    "Adyen connection failed. Using stub payments; no real payment will be taken. Restart after fixing the connection to retry Adyen.");
}
