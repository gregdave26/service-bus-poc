import assert from "node:assert/strict";
import test from "node:test";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { PaymentGatewayNotice } from "../src/PaymentGatewayNotice.js";

test("renders a non-dismissible warning when Adyen fell back to simulated payments", () => {
  const markup = renderToStaticMarkup(React.createElement(PaymentGatewayNotice, {
    config: { gateway: "Stub", configuredGateway: "Adyen", fallbackActive: true },
  }));

  assert.match(markup, /role="alert"/);
  assert.match(markup, /Adyen connection failed/);
  assert.match(markup, /no real payment will be taken/);
  assert.match(markup, /Restart after fixing the connection/);
  assert.doesNotMatch(markup, /<button/);
});

for (const config of [
  undefined,
  { gateway: "Stub" },
  { gateway: "Stub", configuredGateway: "Stub", fallbackActive: false },
  { gateway: "Adyen", configuredGateway: "Adyen", fallbackActive: false },
  { gateway: "AdyenComponent", configuredGateway: "AdyenComponent", fallbackActive: false },
]) {
  test(`does not warn for intentional modes or legacy configuration: ${JSON.stringify(config)}`, () => {
    assert.equal(renderToStaticMarkup(React.createElement(PaymentGatewayNotice, { config })), "");
  });
}
