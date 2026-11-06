import React, { useEffect, useRef, useState } from "react";
import { Alert, Box, CircularProgress } from "@mui/material";
import { AdyenCheckout, Card, Dropin } from "@adyen/adyen-web";
import "@adyen/adyen-web/styles/adyen.css";

function checkoutConfiguration(checkout, onResult, onError, onAdditionalDetails) {
  return {
    session: { id: checkout.sessionId, sessionData: checkout.sessionData },
    clientKey: checkout.clientKey,
    environment: checkout.environment,
    amount: { value: checkout.price.centAmount, currency: checkout.price.currencyCode },
    countryCode: "AU",
    locale: "en-AU",
    showPayButton: false, // Hide Adyen's button - we'll use our own "Make Payment" button
    onPaymentCompleted: (result) => onResult(result.resultCode),
    onPaymentFailed: (result) => onResult(result?.resultCode ?? "Refused"),
    onError: (error) => onError(error.message),
    // The Checkout session owns the initial payment submission. Only follow-up
    // redirect or 3DS details need to cross the Commerce API boundary.
    onAdditionalDetails,
  };
}

// Card data and 3D Secure stay inside Adyen's iframes; the browser result is only an interim status.
// Exposes the Adyen Dropin instance via ref so external buttons can trigger submission.
export const AdyenDropIn = React.forwardRef(function AdyenDropInComponent({
  checkout,
  onResult,
  onValidity,
  onAdditionalDetails,
}, ref) {
  const containerRef = useRef(null);
  const dropinRef = useRef(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Expose submit method via ref
  useEffect(() => {
    if (ref) {
      ref.current = {
        submit: () => dropinRef.current?.submit(),
      };
    }
  }, [ref]);

  useEffect(() => {
    let dropin;
    let cancelled = false;
    AdyenCheckout(checkoutConfiguration(checkout, onResult, setError, onAdditionalDetails))
      .then((adyenCheckout) => {
        if (cancelled) return;
        dropin = new Dropin(adyenCheckout, {
          paymentMethodComponents: [Card],
          onChange: (state) => onValidity?.(state.isValid === true),
        }).mount(containerRef.current);
        dropinRef.current = dropin;
      })
      .catch((initialiseError) => setError(initialiseError.message))
      .finally(() => setLoading(false));
    return () => {
      cancelled = true;
      dropin?.unmount();
    };
  }, [checkout, onResult, onAdditionalDetails, ref]);

  // Hide Adyen's pay button via showPayButton: false in checkout configuration
  // No DOM manipulation needed - the configuration is handled in checkoutConfiguration()

  return <Box>
    {loading && <Box sx={{ py: 4, textAlign: "center" }}><CircularProgress /></Box>}
    {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
    <div ref={containerRef} aria-label="Adyen payment form" />
  </Box>;
});

// A redirect payment method returns the shopper with ?redirectResult; Adyen finishes the session from it.
export async function completeAdyenRedirect(checkout, redirectResult) {
  return new Promise((resolve, reject) => {
    AdyenCheckout({
      ...checkoutConfiguration(checkout, resolve, (message) => reject(new Error(message))),
      session: { id: checkout.sessionId },
    }).then((adyenCheckout) => adyenCheckout.submitDetails({ details: { redirectResult } })).catch(reject);
  });
}
