import React, { useCallback, useEffect, useMemo, useReducer, useState } from "react";
import { Alert, Box, CircularProgress, GlobalStyles, Paper, Stack } from "@mui/material";
import { ThemeProvider } from "@mui/material/styles";
import { RacwaStepperTemplate, theme as racwaTheme } from "@racwa/react-components";
import { fontFaces } from "@racwa/styles";
import { completeAdyenRedirect } from "./AdyenDropIn.jsx";
import { createDigitalSiteApi } from "./digitalSiteApi.js";
import {
  canContinue,
  cartMatchesSelection,
  createInitialFlowState,
  FLOW_STORAGE_KEY,
  flowReducer,
  generateCrmId,
  restoreFlowState,
  STEPPER_STEPS,
  stepperIndexFor,
  toCheckoutRequest,
  toCreateCartRequest,
  toUpdateCartRequest,
} from "./digitalSiteFlowState.js";
import { ConfirmationStep, ConfirmPayStep, CoverSelectionStep, StepButtons, YourDetailsStep } from "./digitalSiteSteps.jsx";
import { MemberHistory } from "./MemberHistory.jsx";

const REDIRECT_RESULT_PARAM = "redirectResult";

function loadFlowState() {
  return restoreFlowState(sessionStorage.getItem(FLOW_STORAGE_KEY)) ?? createInitialFlowState();
}

function returnUrl() {
  return `${window.location.origin}${window.location.pathname}?tab=digitalSite`;
}

function takeRedirectResult() {
  const url = new URL(window.location.href);
  const redirectResult = url.searchParams.get(REDIRECT_RESULT_PARAM);
  if (redirectResult) {
    url.searchParams.delete(REDIRECT_RESULT_PARAM);
    window.history.replaceState(null, "", url);
  }
  return redirectResult;
}

function CurrentStep({ state, dispatch, catalog, api, members, payment }) {
  switch (state.page) {
    case "coverSelection": return <CoverSelectionStep state={state} dispatch={dispatch} covers={catalog.covers} />;
    case "yourDetails": return <YourDetailsStep state={state} dispatch={dispatch} lookupVehicle={api.lookupVehicle} covers={catalog.covers} {...members} />;
    case "confirmPay": return <ConfirmPayStep state={state} dispatch={dispatch} covers={catalog.covers} paymentPlans={catalog.paymentPlans} simulatePayment={api.simulateStubPayment} onPaymentResult={payment.onPaymentResult} />;
    default: return <ConfirmationStep state={state} getCheckoutStatus={api.getCheckoutStatus} onSettled={payment.onSettled} onRestart={() => dispatch({ type: "restart" })} onTryAgain={() => dispatch({ type: "retryPayment" })} />;
  }
}

function DigitalSiteFlow({ api }) {
  const [state, dispatch] = useReducer(flowReducer, undefined, loadFlowState);
  const [catalog, setCatalog] = useState(null);
  const [activeCarts, setActiveCarts] = useState([]);
  const [history, setHistory] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const { crmId } = state;

  const refreshMember = useCallback(() => {
    api.getHistory(crmId).then((memberHistory) => {
      setHistory(memberHistory);
      setActiveCarts(memberHistory.activeCarts);
    }).catch((historyError) => setError(historyError.message));
  }, [api, crmId]);

  useEffect(() => {
    sessionStorage.setItem(FLOW_STORAGE_KEY, JSON.stringify(state));
  }, [state]);

  useEffect(() => {
    api.getCatalog().then(setCatalog).catch((catalogError) => setError(catalogError.message));
  }, [api]);

  useEffect(refreshMember, [refreshMember]);

  useEffect(() => {
    const redirectResult = takeRedirectResult();
    if (!redirectResult || !state.checkout) return;
    dispatch({ type: "paymentSubmitted", resultCode: "Received" });
    completeAdyenRedirect(state.checkout, redirectResult)
      .then((resultCode) => dispatch({ type: "paymentSubmitted", resultCode }))
      .catch((redirectError) => setError(redirectError.message));
    // Runs once on mount: the redirect result belongs to the checkout restored from sessionStorage.
  }, []);

  async function run(operation) {
    setBusy(true);
    setError(null);
    try {
      await operation();
    } catch (operationError) {
      setError(operationError.message);
    } finally {
      setBusy(false);
    }
  }

  const saveCart = () => run(async () => {
    if (cartMatchesSelection(state)) {
      dispatch({ type: "cartSaved", cart: state.cart });
      return;
    }
    const cart = state.cart
      ? await api.updateCart(state.cart.id, toUpdateCartRequest(state))
      : await api.createCart(toCreateCartRequest(state));
    dispatch({ type: "cartSaved", cart });
    refreshMember();
  });

  const startCheckout = () => run(async () => {
    dispatch({ type: "checkoutStarted", checkout: await api.startCheckout(toCheckoutRequest(state, returnUrl())) });
  });

  const discardCart = (cart) => run(async () => {
    await api.deleteCart(cart, crmId);
    dispatch({ type: "cartDiscarded", cartId: cart.id });
    refreshMember();
  });

  const onPaymentResult = useCallback((resultCode) => dispatch({ type: "paymentSubmitted", resultCode }), []);

  const members = {
    activeCart: activeCarts.find((cart) => cart.id !== state.cart?.id) ?? null,
    onNewMember: () => dispatch({ type: "newMember", crmId: generateCrmId() }),
    onResumeCart: (cart) => dispatch({ type: "resumeCart", cart }),
    onDiscardCart: discardCart,
    cartBusy: busy,
  };

  const nextActions = { yourDetails: saveCart, confirmPay: startCheckout };
  const onNext = nextActions[state.page] ?? (() => dispatch({ type: "next" }));
  const showButtons = state.page !== "finished" && state.page !== "coverSelection" && !state.checkout;
  const isOutsideStepper = state.page === "coverSelection" || state.page === "finished";

  return <Stack spacing={3}>
    <ThemeProvider theme={racwaTheme}>
      <GlobalStyles styles={{ "@font-face": fontFaces }} />
      <Paper
        variant="outlined"
        sx={{
          overflow: "hidden",
          position: "relative",
          minHeight: 640,
          bgcolor: "background.default",
          fontFamily: "'Stag Sans Web', Helvetica, Arial, sans-serif",
          "& *, & input, & button, & textarea, & select": {
            fontFamily: "inherit",
          },
        }}
        aria-label="RACWA digital site"
      >
        <RacwaStepperTemplate
          sidebarTitle="Join Roadside Assistance"
          steps={STEPPER_STEPS}
          activeStepIndex={stepperIndexFor(state.page)}
          onStepClick={(_name, index) => dispatch({ type: "goToStep", index })}
          mobileStepperProps={{ hideBack: isOutsideStepper, hideProgress: isOutsideStepper, onClickBack: () => dispatch({ type: "back" }) }}
        >
          {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}
          {catalog ? <>
            <CurrentStep state={state} dispatch={dispatch} catalog={catalog} api={api} members={members} payment={{ onPaymentResult, onSettled: refreshMember }} />
            {showButtons && <StepButtons
              onBack={state.page === "yourDetails" || state.page === "coverSelection" ? undefined : () => dispatch({ type: "back" })}
              onNext={state.page === "pay" ? undefined : onNext}
              nextLabel={state.page === "yourDetails" ? "Continue" : "Continue to payment"}
              nextDisabled={!canContinue(state)}
              loading={busy}
            />}
          </> : !error && <Box sx={{ py: 6, textAlign: "center" }}><CircularProgress /></Box>}
        </RacwaStepperTemplate>
      </Paper>
    </ThemeProvider>
    <MemberHistory crmId={crmId} history={history} />
  </Stack>;
}

export function DigitalSite({ apiBaseUrl }) {
  const api = useMemo(() => (apiBaseUrl ? createDigitalSiteApi(apiBaseUrl) : null), [apiBaseUrl]);
  return api ? <DigitalSiteFlow api={api} /> : <Box sx={{ py: 6, textAlign: "center" }}><CircularProgress /></Box>;
}
