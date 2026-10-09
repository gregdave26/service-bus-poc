import React, { useEffect, useState } from "react";
import { Box, Button, Chip, CircularProgress, List, ListItem, ListItemIcon, ListItemText, Stack, Typography } from "@mui/material";
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
import { colors } from "@racwa/styles";
import { AdyenDropIn } from "./AdyenDropIn.jsx";
import { describeCheckoutStatus, formatCurrency } from "./digitalSiteFlowState.js";
import { StubPaymentPanel } from "./StubPaymentPanel.jsx";

const ROADSIDE_PHONE = "13 11 11";
const DEMO_REGOS = ["1ANURAG", "1ABC123", "1RAC000", "1UTE999"];
const STATUS_POLL_INTERVAL_MS = 1500;

export function StepButtons({ onBack, onNext, nextLabel = "Next", nextDisabled, loading }) {
  return <Stack direction="row" spacing={2} sx={{ mt: 4 }}>
    {onBack && <Button variant="outlined" onClick={onBack} disabled={loading}>Back</Button>}
    {onNext && <Button variant="contained" onClick={onNext} disabled={nextDisabled || loading} startIcon={loading ? <CircularProgress size={16} color="inherit" /> : null}>{nextLabel}</Button>}
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

const COVER_FEATURES = [
  ["Car covered", ["✓", "✓", "✓", "✓"]],
  ["Person covered", ["", "", "✓", "✓"]],
  ["Unlimited callouts", ["✓", "✓", "✓", "✓"]],
  ["Free metro towing", ["25km", "100km", "100km", "200km"]],
  ["Taxi", ["", "up to $55", "up to $55", "up to $100"]],
  ["Locksmith", ["", "up to $165", "up to $165", "up to $200"]],
  ["Trailer / caravan towing", ["", "✓", "✓", "✓"]],
  ["Free fuel", ["", "", "", "up to $50"]],
  ["24 hours a day, 7 days a week", ["✓", "✓", "✓", "✓"]],
  ["Vehicle recovery", ["", "✓", "✓", "✓"]],
];

const COVER_ORDER = ["STD", "CLAS", "ULTI", "ULPL"];

export function CoverSelectionStep({ state, dispatch, covers }) {
  const orderedCovers = COVER_ORDER
    .map((id) => covers.find((cover) => cover.id === id))
    .filter(Boolean);

  return <Stack spacing={3}>
    <Typography variant="h2" sx={{ textAlign: "center" }}>Your choice of cover options</Typography>
    <Box sx={{ width: "100%", overflow: "visible" }}>
      <Box sx={{ width: "100%" }}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "92px repeat(4, minmax(0, 1fr))", sm: "140px repeat(4, minmax(0, 1fr))" }, alignItems: "stretch" }}>
          <Box />
          {orderedCovers.map((cover) => <Card
            key={cover.id}
            background="gray"
            sx={{
              borderRadius: 0,
              padding: { xs: "0.5rem !important", sm: "0.75rem !important" },
              margin: "0 !important",
              width: "auto !important",
              textAlign: "center",
              border: "1px solid white",
              bgcolor: cover.id === "CLAS" ? colors.racYellow : colors.racGrayLight,
              color: colors.dieselDeepest,
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              gap: { xs: 0.5, sm: 0.75 },
            }}
          >
            <Typography
              variant="caption"
              sx={{
                minHeight: { xs: "1rem", sm: "1.2rem" },
                display: "block",
                color: colors.dieselDeepest,
                visibility: cover.id === "CLAS" ? "visible" : "hidden",
              }}
            >
              Most popular
            </Typography>
            <Typography
              variant="h4"
              sx={{
                minHeight: { xs: "2rem", sm: "1.8rem" },
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                textAlign: "center",
                fontSize: { xs: "0.62rem", sm: "0.8rem" },
                lineHeight: 1.1,
                overflowWrap: "anywhere",
                m: 0,
              }}
            >
              {cover.name}
            </Typography>
            <Typography variant="h3" sx={{ fontSize: { xs: "0.85rem", sm: "1.25rem" }, lineHeight: 1, m: 0, whiteSpace: "nowrap" }}>{formatCurrency(cover.annualPrice / 12)}<Typography component="span" variant="caption" sx={{ fontSize: { xs: "0.45rem", sm: "0.6rem" } }}> /month</Typography></Typography>
            <Typography variant="caption" sx={{ fontSize: { xs: "0.5rem", sm: "0.65rem" }, lineHeight: 1.1, m: 0 }}>or {formatCurrency(cover.annualPrice)} /yr</Typography>
            <Typography variant="body2" sx={{ minHeight: { xs: "2.2rem", sm: "2.5rem" }, mt: 0, fontSize: { xs: "0.52rem", sm: "0.68rem" }, lineHeight: 1.15, m: 0, display: "flex", alignItems: "center", justifyContent: "center" }}>{cover.chip}</Typography>
          </Card>)}
          {COVER_FEATURES.flatMap(([feature, values]) => [
            <Box key={`${feature}-label`} sx={{ p: { xs: 0.5, sm: 1 }, bgcolor: colors.subtleBg, color: colors.dieselDeepest, fontSize: { xs: "0.58rem", sm: "0.8rem" }, overflowWrap: "anywhere" }}>{feature}</Box>,
            ...values.map((value, index) => <Box key={`${feature}-${index}`} sx={{ p: { xs: 0.5, sm: 1 }, textAlign: "center", bgcolor: index === 1 ? colors.racGrayLight : colors.subtleBg, color: colors.dieselDeepest, fontSize: { xs: "0.6rem", sm: "0.8rem" }, overflowWrap: "anywhere", borderLeft: "1px solid white" }}>{value}</Box>),
          ])}
          <Box />
          {orderedCovers.map((cover) => <Button
            key={`${cover.id}-join`}
            variant={state.coverId === cover.id ? "contained" : "outlined"}
            onClick={() => dispatch({ type: "chooseCover", value: cover.id })}
            sx={{
              m: 1,
              borderColor: colors.dieselDeep,
              color: state.coverId === cover.id ? colors.dieselDeepest : colors.dieselDeep,
              bgcolor: state.coverId === cover.id ? colors.racYellow : colors.dieselDeepest,
              "&:hover": { bgcolor: state.coverId === cover.id ? colors.racYellowLight : colors.dieselDeep },
            }}
          >
            Join now
          </Button>)}
        </Box>
      </Box>
    </Box>
    <Typography variant="caption" color="text.secondary">
      Benefits are subject to the Roadside Assistance Entitlements, terms and conditions.
    </Typography>
    <Typography variant="body2" sx={{ textAlign: "center" }}>
      <strong>Are you currently broken down?</strong> Please note an extra fee may apply if you need assistance within 48 hours of joining.
    </Typography>
  </Stack>;
}

function MemberIdentity({ crmId, onNewMember }) {
  return <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
    <Typography variant="body2" color="text.secondary">Signed in as mock member</Typography>
    <Chip size="small" label={crmId} />
    <Button size="small" onClick={onNewMember}>New member</Button>
  </Stack>;
}

function ActiveCartPrompt({ cart, onResume, onDiscard, busy }) {
  return <RacwaAlertNotification severity="info">
    <strong>You have an unfinished purchase.</strong> {cart.coverName ?? "Roadside Assistance"} for {formatCurrency(cart.totalPrice)}{cart.vehicle ? ` on ${cart.vehicle.rego}` : ""}.
    <Stack direction="row" spacing={2} sx={{ mt: 1 }}>
      <Button size="small" variant="contained" onClick={() => onResume(cart)} disabled={busy}>Resume</Button>
      <Button size="small" variant="outlined" onClick={() => onDiscard(cart)} disabled={busy}>Start fresh</Button>
    </Stack>
  </RacwaAlertNotification>;
}

export function QuickCheckStep({ state, dispatch, activeCart, onNewMember, onResumeCart, onDiscardCart, cartBusy }) {
  return <Stack spacing={3}>
    <Typography variant="h2">A quick check</Typography>
    <MemberIdentity crmId={state.crmId} onNewMember={onNewMember} />
    {activeCart && <ActiveCartPrompt cart={activeCart} onResume={onResumeCart} onDiscard={onDiscardCart} busy={cartBusy} />}
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

const PERSONAL_FIELDS = [
  ["title", "Title"],
  ["firstName", "First name"],
  ["lastName", "Last name"],
  ["dateOfBirth", "Date of birth"],
  ["email", "Email address"],
  ["phone", "Phone number"],
  ["addressLine1", "Street address"],
  ["suburb", "Suburb"],
  ["state", "State"],
  ["postcode", "Postcode"],
];

function PersonalDetails({ state, dispatch }) {
  return <Stack spacing={2}>
    <Typography variant="h3">Your personal details</Typography>
    <Typography color="text.secondary">Tell us who will hold the Roadside Assistance membership.</Typography>
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, minmax(0, 1fr))" }, gap: 2 }}>
      {PERSONAL_FIELDS.map(([field, label]) => <RacwaTextInput
        key={field}
        id={`personal-${field}`}
        label={label}
        type={field === "dateOfBirth" ? "date" : field === "email" ? "email" : field === "phone" ? "tel" : "text"}
        value={state.personalDetails[field]}
        onChange={(event) => dispatch({ type: "changePersonalDetails", field, value: event.target.value })}
        inputProps={field === "postcode" ? { maxLength: 4, inputMode: "numeric" } : undefined}
      />)}
    </Box>
  </Stack>;
}

export function YourDetailsStep({ state, dispatch, lookupVehicle, ...members }) {
  return <Stack spacing={4}>
    <Box>
      <Typography variant="h2">Your details</Typography>
      <Typography color="text.secondary">Join RAC Roadside Assistance by entering your details, vehicle and cover.</Typography>
    </Box>
    <MemberIdentity crmId={state.crmId} onNewMember={members.onNewMember} />
    {members.activeCart && <ActiveCartPrompt cart={members.activeCart} onResume={members.onResumeCart} onDiscard={members.onDiscardCart} busy={members.cartBusy} />}
    <PersonalDetails state={state} dispatch={dispatch} />
    <VehicleDetailsStep state={state} dispatch={dispatch} lookupVehicle={lookupVehicle} />
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

export function ConfirmPayStep({ state, dispatch, paymentPlans, simulatePayment, onPaymentResult }) {
  return state.checkout
    ? <PayStep state={state} simulatePayment={simulatePayment} onPaymentResult={onPaymentResult} />
    : <PaymentPlanStep state={state} dispatch={dispatch} cart={state.cart} paymentPlans={paymentPlans} />;
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
        sublabel={`${formatCurrency(cover.annualPrice)} yearly · ${cover.chip}`}
        footer={<Perks perks={cover.perks} />}
        showFooter
        highlightSelected
      />)}
    </RacwaRadioGroup>
    <Typography variant="caption" color="text.secondary"><sup>1</sup> Extended benefits have a 14-day waiting period. Includes accommodation, hire car, vehicle recovery, and passenger transport.</Typography>
  </Stack>;
}


export function PaymentPlanStep({ state, dispatch, cart, paymentPlans }) {
  return <Stack spacing={3}>
    <Box>
      <Typography variant="h2">Payment plan</Typography>
      <Typography color="text.secondary">Choose a way to pay that works for you</Typography>
    </Box>
    <Typography id="payment-plan-label" sx={{ fontWeight: 500 }}>Choose your payment plan for {cart.coverName} cover</Typography>
    <RacwaRadioGroup name="paymentPlan" aria-labelledby="payment-plan-label" defaultValue={state.paymentPlan ?? undefined} onChange={(_event, value) => dispatch({ type: "choosePaymentPlan", value })} sx={{ gap: 2 }}>
      {paymentPlans.map((plan) => <RacwaRadioListItem
        key={plan.id}
        value={plan.id}
        label={`${plan.title} · ${formatCurrency(cart.totalPrice)}`}
        sublabel={`${plan.chips.join(" · ")}. Paid today by card.`}
        highlightSelected
      />)}
    </RacwaRadioGroup>
    {state.paymentPlan && <>
      <RacwaAlertNotification severity="info"><strong>Your cover and cancellation.</strong> Roadside Assistance is a 12 month membership with no refunds for cancellations.</RacwaAlertNotification>
      <Card background="gray" sx={{ p: 2 }}>
        <Typography sx={{ fontWeight: 500 }}>Payment authority terms</Typography>
        <Typography variant="body2" color="text.secondary">You authorise RAC to charge {formatCurrency(cart.totalPrice)} to the card you enter on the next page. Card details are collected by the payment provider and never reach RAC systems.</Typography>
      </Card>
      <RacwaCheckboxGroup key={state.paymentPlan}>
        <RacwaCheckboxListItem checked={state.acceptedPaymentAuthTerms} label="I've read and agree to the payment authority terms" onChange={(_event, value) => dispatch({ type: "acceptTerms", field: "acceptedPaymentAuthTerms", value })} />
        <RacwaCheckboxListItem checked={state.acceptedRoadsideAssistTerms} label="I've read and agree to the Roadside Assistance Entitlements" onChange={(_event, value) => dispatch({ type: "acceptTerms", field: "acceptedRoadsideAssistTerms", value })} />
      </RacwaCheckboxGroup>
    </>}
  </Stack>;
}

export function PayStep({ state, simulatePayment, onPaymentResult }) {
  const { checkout } = state;
  return <Stack spacing={3}>
    <Box>
      <Typography variant="h2">Payment</Typography>
      <Typography color="text.secondary">{state.cart?.coverName} Roadside Assistance · {formatCurrency(checkout.amount)} yearly</Typography>
    </Box>
    {checkout.gateway === "Adyen"
      ? <AdyenDropIn checkout={checkout} onResult={onPaymentResult} />
      : <StubPaymentPanel checkout={checkout} simulatePayment={simulatePayment} onResult={onPaymentResult} />}
  </Stack>;
}

function useCheckoutStatus(paymentId, getCheckoutStatus, onSettled) {
  const [status, setStatus] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    let timer;
    let stopped = false;
    async function poll() {
      try {
        const latest = await getCheckoutStatus(paymentId);
        if (stopped) return;
        setStatus(latest);
        setError(null);
        if (describeCheckoutStatus(latest).done) {
          onSettled?.();
          return;
        }
      } catch (pollError) {
        if (stopped) return;
        setError(pollError.message);
      }
      timer = setTimeout(poll, STATUS_POLL_INTERVAL_MS);
    }
    poll();
    return () => {
      stopped = true;
      clearTimeout(timer);
    };
  }, [paymentId, getCheckoutStatus, onSettled]);

  return { status, error };
}

function StatusDetail({ label, value }) {
  return value ? <Typography variant="body2"><strong>{label}:</strong> {value}</Typography> : null;
}

export function ConfirmationStep({ state, getCheckoutStatus, onSettled, onRestart, onTryAgain }) {
  const { status, error } = useCheckoutStatus(state.checkout.paymentId, getCheckoutStatus, onSettled);
  const progress = describeCheckoutStatus(status);
  return <Stack spacing={3}>
    <RacwaCardNotification severity={progress.severity} title={progress.title} subtitle={status?.orderNumber ? `Order ${status.orderNumber}` : `Payment ${state.checkout.merchantReference}`}>
      {state.cart?.coverName} Roadside Assistance · {formatCurrency(state.checkout.amount)} yearly
      {state.cart?.vehicle ? ` · ${state.cart.vehicle.rego}` : ""}
    </RacwaCardNotification>
    {!progress.done && <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}><CircularProgress size={16} /><Typography variant="body2" color="text.secondary">Checking with the payment provider and commercetools…</Typography></Stack>}
    {state.paymentResult && !progress.done && <Typography variant="body2" color="text.secondary">The payment form reported <strong>{state.paymentResult}</strong>. Your cover is confirmed only once the payment notification is processed.</Typography>}
    {error && <RacwaAlertNotification severity="warning">Could not read the payment status: {error}</RacwaAlertNotification>}
    {status && <Card background="gray" sx={{ p: 2 }}>
      <StatusDetail label="Payment" value={status.paymentState} />
      <StatusDetail label="PSP reference" value={status.pspReference} />
      <StatusDetail label="Refusal reason" value={status.refusalReason} />
      <StatusDetail label="Processing result" value={status.processingResult} />
      <StatusDetail label="Order state" value={status.orderState} />
      <StatusDetail label="Provisioning" value={status.provisioningStatus} />
      <StatusDetail label="ProductHoldingChange published" value={status.orderId ? (status.holdingEventPublished ? "Yes" : "Not yet") : null} />
      <StatusDetail label="Correlation id" value={status.correlationId} />
    </Card>}
    {status?.holdingEventPublished && <RacwaAlertNotification severity="info">A ProductHoldingChange event was published to contact.events. Open the Contact Events tab to follow it.</RacwaAlertNotification>}
    <Stack direction="row" spacing={2}>
      {status?.paymentState === "Refused" && <Button variant="contained" onClick={onTryAgain}>Try another payment</Button>}
      <Button variant={status?.paymentState === "Refused" ? "outlined" : "contained"} onClick={onRestart}>Start another purchase</Button>
    </Stack>
  </Stack>;
}
