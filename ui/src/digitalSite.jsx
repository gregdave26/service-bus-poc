import React, { useCallback, useEffect, useReducer, useState } from "react";
import { Alert, Box, Chip, CircularProgress, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import { ThemeProvider } from "@mui/material/styles";
import { RacwaStepperTemplate, theme as racwaTheme } from "@racwa/react-components";
import { canContinue, flowReducer, formatCurrency, initialFlowState, STEPPER_STEPS, stepperIndexFor, toOrderRequest } from "./digitalSiteFlowState.js";
import { ConfirmationStep, ConfirmCoverStep, PaymentPlanStep, QuickCheckStep, StepButtons, VehicleDetailsStep } from "./digitalSiteSteps.jsx";

async function getJson(url, options) {
  const response = await fetch(url, options);
  const body = await response.json();
  if (!response.ok) throw new Error(body.error || `Request failed (${response.status})`);
  return body;
}

const lookupVehicle = (rego) => getJson(`/api/digital-site/vehicles/${encodeURIComponent(rego.trim())}`);

function RecentOrders({ orders }) {
  return <Paper variant="outlined" sx={{ p: 2 }}>
    <Typography variant="h6" sx={{ mb: 1 }}>Recent Roadside Assistance orders</Typography>
    {orders.length === 0 ? <Typography color="text.secondary">No orders yet. Complete the flow to place one.</Typography> : <Table size="small" aria-label="Recent Roadside Assistance orders">
      <TableHead><TableRow><TableCell>Order</TableCell><TableCell>Created</TableCell><TableCell>Cover</TableCell><TableCell>Plan</TableCell><TableCell>Vehicle</TableCell><TableCell>Event</TableCell></TableRow></TableHead>
      <TableBody>{orders.map((order) => <TableRow key={order.orderId}>
        <TableCell>{order.orderId}</TableCell>
        <TableCell>{new Date(order.createdAt).toLocaleString()}</TableCell>
        <TableCell>{order.cover.name}</TableCell>
        <TableCell>{formatCurrency(order.price.instalmentAmount)} {order.price.frequency}</TableCell>
        <TableCell>{order.vehicle?.rego ?? "—"}</TableCell>
        <TableCell><Chip size="small" color={order.publishStatus === "published" ? "success" : "warning"} label={order.publishStatus === "published" ? "Published" : "Publish failed"} title={order.publishError ?? order.eventId ?? ""} /></TableCell>
      </TableRow>)}</TableBody>
    </Table>}
  </Paper>;
}

function CurrentStep({ state, dispatch, catalog }) {
  const cover = catalog.covers.find((candidate) => candidate.id === state.coverId);
  switch (state.page) {
    case "quickCheck": return <QuickCheckStep state={state} dispatch={dispatch} />;
    case "vehicleDetails": return <VehicleDetailsStep state={state} dispatch={dispatch} lookupVehicle={lookupVehicle} />;
    case "confirmCover": return <ConfirmCoverStep state={state} dispatch={dispatch} covers={catalog.covers} />;
    case "paymentPlan": return <PaymentPlanStep state={state} dispatch={dispatch} cover={cover} paymentPlans={catalog.paymentPlans} />;
    default: return <ConfirmationStep order={state.order} onRestart={() => dispatch({ type: "restart" })} />;
  }
}

export function DigitalSite() {
  const [state, dispatch] = useReducer(flowReducer, initialFlowState);
  const [catalog, setCatalog] = useState(null);
  const [orders, setOrders] = useState([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  const refreshOrders = useCallback(() => getJson("/api/digital-site/orders").then(setOrders).catch((ordersError) => setError(ordersError.message)), []);

  useEffect(() => {
    getJson("/api/digital-site/catalog").then(setCatalog).catch((catalogError) => setError(catalogError.message));
    refreshOrders();
  }, [refreshOrders]);

  async function checkout() {
    setSubmitting(true);
    setError(null);
    try {
      const order = await getJson("/api/digital-site/orders", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(toOrderRequest(state)) });
      dispatch({ type: "orderPlaced", order });
      refreshOrders();
    } catch (checkoutError) {
      setError(checkoutError.message);
    } finally {
      setSubmitting(false);
    }
  }

  const isPaymentPage = state.page === "paymentPlan";
  const showButtons = state.page !== "confirmation";
  const isOutsideStepper = state.page === "quickCheck" || state.page === "confirmation";

  return <Stack spacing={3}>
    <ThemeProvider theme={racwaTheme}>
      <Paper variant="outlined" sx={{ overflow: "hidden", position: "relative", minHeight: 640, bgcolor: "background.default" }} aria-label="RACWA digital site">
        <RacwaStepperTemplate
          sidebarTitle="Join Roadside Assistance"
          steps={STEPPER_STEPS}
          activeStepIndex={stepperIndexFor(state.page)}
          onStepClick={(_name, index) => dispatch({ type: "goToStep", index })}
          mobileStepperProps={{ hideBack: isOutsideStepper, hideProgress: isOutsideStepper, onClickBack: () => dispatch({ type: "back" }) }}
        >
          {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}
          {catalog ? <>
            <CurrentStep state={state} dispatch={dispatch} catalog={catalog} />
            {showButtons && <StepButtons
              onBack={state.page === "quickCheck" ? undefined : () => dispatch({ type: "back" })}
              onNext={isPaymentPage ? checkout : () => dispatch({ type: "next" })}
              nextLabel={isPaymentPage ? "Checkout" : "Next"}
              nextDisabled={!canContinue(state)}
              loading={submitting}
            />}
          </> : <Box sx={{ py: 6, textAlign: "center" }}><CircularProgress /></Box>}
        </RacwaStepperTemplate>
      </Paper>
    </ThemeProvider>
    <RecentOrders orders={orders} />
  </Stack>;
}
