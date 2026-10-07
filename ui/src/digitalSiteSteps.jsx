import React, { useState } from "react";
import { Box, Button, CircularProgress, List, ListItem, ListItemIcon, ListItemText, Stack, Typography } from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import {
  Card,
  RacwaAlertNotification,
  RacwaCardNotification,
  RacwaCheckboxGroup,
  RacwaCheckboxListItem,
  RacwaRadioGroup,
  RacwaRadioItem,
  RacwaRadioListItem,
  RacwaTextInput,
} from "@racwa/react-components";
import { formatCurrency, paymentPlanDescription, paymentPlanPrice } from "./digitalSiteFlowState.js";

const ROADSIDE_PHONE = "13 11 11";
const DEMO_REGOS = ["1ANURAG", "1ABC123", "1RAC000", "1UTE999"];

export function StepButtons({ onBack, onNext, nextLabel = "Next", nextDisabled, loading }) {
  return <Stack direction="row" spacing={2} sx={{ mt: 4 }}>
    {onBack && <Button variant="outlined" onClick={onBack} disabled={loading}>Back</Button>}
    <Button variant="contained" onClick={onNext} disabled={nextDisabled || loading} startIcon={loading ? <CircularProgress size={16} color="inherit" /> : null}>{nextLabel}</Button>
  </Stack>;
}

function Perks({ perks }) {
  return <List dense disablePadding>
    {perks.map((perk) => <ListItem key={perk} disablePadding>
      <ListItemIcon sx={{ minWidth: 28 }}><CheckIcon fontSize="small" color="success" /></ListItemIcon>
      <ListItemText primary={perk} />
    </ListItem>)}
  </List>;
}

export function QuickCheckStep({ state, dispatch }) {
  return <Stack spacing={3}>
    <Typography variant="h2">A quick check</Typography>
    <Typography id="broken-down-label" sx={{ fontWeight: 500 }}>Are you in a breakdown right now?</Typography>
    <RacwaRadioGroup name="isBrokenDown" aria-labelledby="broken-down-label" defaultValue={state.isBrokenDown ?? undefined} onChange={(_event, value) => dispatch({ type: "answerQuickCheck", value })}>
      <RacwaRadioItem value="Yes" label="Yes" />
      <RacwaRadioItem value="No" label="No" />
    </RacwaRadioGroup>
    {state.isBrokenDown === "No" && <RacwaAlertNotification severity="info">
      <strong>Great, you can join online.</strong> Roadside assistance starts after 48 hours. If you need help within this time, call {ROADSIDE_PHONE} to discuss waiving the waiting period. Extended benefits have a 14-day waiting period.
    </RacwaAlertNotification>}
    {state.isBrokenDown === "Yes" && <RacwaAlertNotification severity="warning">
      <strong>Please call us on {ROADSIDE_PHONE}.</strong> Normally there's a 48-hour waiting period but you can pay a fee to get roadside help now.
    </RacwaAlertNotification>}
  </Stack>;
}

export function VehicleDetailsStep({ state, dispatch, lookupVehicle }) {
  const [lookupError, setLookupError] = useState(null);
  const [searching, setSearching] = useState(false);
  const { searchChoice, rego, details } = state.vehicle;

  async function findVehicle() {
    setSearching(true);
    setLookupError(null);
    try {
      dispatch({ type: "vehicleFound", vehicle: await lookupVehicle(rego) });
    } catch (error) {
      setLookupError(error.message);
    } finally {
      setSearching(false);
    }
  }

  return <Stack spacing={3}>
    <Typography variant="h2">Your vehicle</Typography>
    <Typography id="vehicle-search-label" sx={{ fontWeight: 500 }}>How would you like to add your vehicle?</Typography>
    <RacwaRadioGroup name="searchChoice" aria-labelledby="vehicle-search-label" defaultValue={searchChoice ?? undefined} onChange={(_event, value) => dispatch({ type: "chooseVehicleSearch", value })}>
      <RacwaRadioItem value="RegoLookup" label="Find my vehicle by registration" />
      <RacwaRadioItem value="Skip" label="Skip for now" />
    </RacwaRadioGroup>
    {searchChoice === "RegoLookup" && <Stack spacing={2}>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: { sm: "flex-end" } }}>
        <RacwaTextInput
          id="rego"
          label="Registration number"
          sublabel={`Demo regos: ${DEMO_REGOS.join(", ")}`}
          value={rego}
          error={Boolean(lookupError)}
          helperText={lookupError ?? undefined}
          onChange={(event) => { setLookupError(null); dispatch({ type: "changeRego", value: event.target.value }); }}
          onKeyDown={(event) => { if (event.key === "Enter" && rego.trim()) findVehicle(); }}
          inputProps={{ maxLength: 10 }}
        />
        <Button variant="outlined" onClick={findVehicle} disabled={!rego.trim() || searching}>Find vehicle</Button>
      </Stack>
      {details && <RacwaCardNotification severity="success" title={`${details.year} ${details.make}`.toUpperCase()} subtitle={`${details.rego} · ${details.model} ${details.body} ${details.transmission} ${details.fuel}`.toUpperCase()}>
        Is this your vehicle? You can change it any time before checkout.
      </RacwaCardNotification>}
    </Stack>}
    {searchChoice === "Skip" && <RacwaAlertNotification severity="info">No problem. You can add your vehicle to your membership later in myRAC.</RacwaAlertNotification>}
  </Stack>;
}

export function ConfirmCoverStep({ state, dispatch, covers }) {
  return <Stack spacing={3}>
    <Box>
      <Typography variant="h2">Confirm your cover</Typography>
      <Typography color="text.secondary">Confirm your Roadside Assistance cover</Typography>
    </Box>
    <Typography id="cover-label" sx={{ fontWeight: 500 }}>Review your cover</Typography>
    <RacwaRadioGroup name="cover" aria-labelledby="cover-label" defaultValue={state.coverId ?? undefined} onChange={(_event, value) => dispatch({ type: "chooseCover", value })} sx={{ gap: 2 }}>
      {covers.map((cover) => <RacwaRadioListItem
        key={cover.id}
        value={cover.id}
        label={cover.name}
        sublabel={`${formatCurrency(cover.monthlyPrice)} monthly or ${formatCurrency(cover.annualPrice)} yearly · ${cover.chip}`}
        footer={<Perks perks={cover.perks} />}
        showFooter
        highlightSelected
      />)}
    </RacwaRadioGroup>
    <Typography variant="caption" color="text.secondary"><sup>1</sup> Extended benefits have a 14-day waiting period. Includes accommodation, hire car, vehicle recovery, and passenger transport.</Typography>
  </Stack>;
}

export function PaymentPlanStep({ state, dispatch, cover, paymentPlans }) {
  return <Stack spacing={3}>
    <Box>
      <Typography variant="h2">Payment plan</Typography>
      <Typography color="text.secondary">Choose a way to pay that works for you</Typography>
    </Box>
    <Typography id="payment-plan-label" sx={{ fontWeight: 500 }}>Choose your payment plan for {cover.name} cover</Typography>
    <RacwaRadioGroup name="paymentPlan" aria-labelledby="payment-plan-label" defaultValue={state.paymentPlan ?? undefined} onChange={(_event, value) => dispatch({ type: "choosePaymentPlan", value })} sx={{ gap: 2 }}>
      {paymentPlans.map((plan) => <RacwaRadioListItem
        key={plan.id}
        value={plan.id}
        label={`${plan.title} · ${formatCurrency(paymentPlanPrice(cover, plan.id))}`}
        sublabel={`${plan.chips.join(" · ")}. ${paymentPlanDescription(cover, plan.id)}`}
        highlightSelected
      />)}
    </RacwaRadioGroup>
    {state.paymentPlan && <>
      <RacwaAlertNotification severity="info"><strong>Your cover and cancellation.</strong> Roadside Assistance is a 12 month membership with no refunds for cancellations. Members are committed to the full 12 months, even if paid monthly.</RacwaAlertNotification>
      <Card background="gray" sx={{ p: 2 }}>
        <Typography sx={{ fontWeight: 500 }}>Direct debit authorisation terms</Typography>
        <Typography variant="body2" color="text.secondary">This is a demo: no payment details are collected and no money is taken.</Typography>
      </Card>
      <RacwaCheckboxGroup key={state.paymentPlan}>
        <RacwaCheckboxListItem checked={state.acceptedPaymentAuthTerms} label="I've read and agree to the direct debit authorisation terms" onChange={(_event, value) => dispatch({ type: "acceptTerms", field: "acceptedPaymentAuthTerms", value })} />
        <RacwaCheckboxListItem checked={state.acceptedRoadsideAssistTerms} label="I've read and agree to the Roadside Assistance Entitlements" onChange={(_event, value) => dispatch({ type: "acceptTerms", field: "acceptedRoadsideAssistTerms", value })} />
      </RacwaCheckboxGroup>
    </>}
  </Stack>;
}

export function ConfirmationStep({ order, onRestart }) {
  const published = order.publishStatus === "published";
  return <Stack spacing={3}>
    <RacwaCardNotification severity="success" title="You're covered" subtitle={`Order ${order.orderId}`}>
      {order.cover.name} Roadside Assistance · {formatCurrency(order.price.instalmentAmount)} {order.price.frequency}
      {order.vehicle ? ` · ${order.vehicle.rego}` : ""}
    </RacwaCardNotification>
    {published
      ? <RacwaAlertNotification severity="info">ProductHoldingChange event <strong>{order.eventId}</strong> was published to contact.events. Open the Contact Events tab to follow it.</RacwaAlertNotification>
      : <RacwaAlertNotification severity="warning">The order was saved but the ProductHoldingChange event could not be published: {order.publishError}</RacwaAlertNotification>}
    <Box><Button variant="contained" onClick={onRestart}>Start another purchase</Button></Box>
  </Stack>;
}
