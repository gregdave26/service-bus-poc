/**
 * Money is handled in integer cents internally so that line totals, subtotal,
 * tax, and the grand total always reconcile exactly - floating point decimal
 * arithmetic (e.g. 0.1 + 0.2) cannot be relied on to satisfy that invariant.
 */
export function toCents(amount) {
  return Math.round(amount * 100);
}

export function toAmount(cents) {
  return Math.round(cents) / 100;
}
