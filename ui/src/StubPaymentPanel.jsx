import React, { useState } from "react";
import { Alert, Button, Stack, Typography } from "@mui/material";
import { Card } from "@racwa/react-components";

const OUTCOMES = [
  { outcome: "Authorised", label: "Approve payment", color: "success" },
  { outcome: "Refused", label: "Decline payment", color: "error" },
  { outcome: "Pending", label: "Leave pending", color: "inherit" },
];

// Stands in for the Drop-in: each button sends a signed notification through the same webhook module Adyen would call.
export function StubPaymentPanel({ checkout, simulatePayment, onResult }) {
  const [submitting, setSubmitting] = useState(null);
  const [error, setError] = useState(null);

  async function simulate(outcome) {
    setSubmitting(outcome);
    setError(null);
    try {
      await simulatePayment(checkout.paymentId, outcome);
      onResult(outcome);
    } catch (simulateError) {
      setError(simulateError.message);
      setSubmitting(null);
    }
  }

  return <Card background="gray" sx={{ p: 2 }}>
    <Stack spacing={2}>
      <Typography sx={{ fontWeight: 500 }}>Stub payment gateway</Typography>
      <Typography variant="body2" color="text.secondary">
        Adyen is not configured, so no card form is shown. Choose the outcome the payment provider should report for payment {checkout.merchantReference}.
      </Typography>
      {error && <Alert severity="error">{error}</Alert>}
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
        {OUTCOMES.map(({ outcome, label, color }) => <Button key={outcome} variant={outcome === "Authorised" ? "contained" : "outlined"} color={color} disabled={submitting !== null} onClick={() => simulate(outcome)}>{label}</Button>)}
      </Stack>
    </Stack>
  </Card>;
}
