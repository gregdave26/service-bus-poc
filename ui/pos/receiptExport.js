const sundryDebtor = "01*POSSND";

function valueOrEmpty(value) {
  return value === null || value === undefined ? "" : String(value);
}

function pipeValue(value) {
  return valueOrEmpty(value).replaceAll("|", " ").replaceAll(/\r?\n/g, " ");
}

function formatInvoiceDate(transactionDate) {
  const date = new Date(transactionDate);
  if (Number.isNaN(date.getTime())) return "";
  const parts = new Intl.DateTimeFormat("en-AU", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    timeZone: "Australia/Perth",
  }).formatToParts(date);
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]));
  return `${values.day}-${values.month}-${values.year}`;
}

function terminalId(tillId) {
  const digits = String(tillId ?? "").match(/\d+/)?.[0] ?? "";
  return digits ? digits.padStart(3, "0").slice(-3) : "";
}

function paymentDetails(receipt) {
  const payments = receipt.payments ?? [];
  const firstPayment = payments[0] ?? {};
  return {
    paymentMethod: payments.length > 1 ? "SPLIT" : (firstPayment.paymentType ?? ""),
    eftposId: firstPayment.eftposTerminalId ?? "",
    refNo: firstPayment.eftposRefNo ?? "",
    paymentGl: firstPayment.eftposTerminalId && firstPayment.eftposRefNo
      ? `${firstPayment.eftposTerminalId}-${firstPayment.eftposRefNo}`
      : "",
  };
}

function customerNumber(receipt) {
  return receipt.customer?.customerNumber
    ?? receipt.customer?.membershipNumber
    ?? sundryDebtor;
}

function refundFlag(receipt) {
  return receipt.financials.totalIncGst < 0 ? "CREDIT" : (receipt.financials.refundFlag ?? "INVOICE");
}

function amount(value) {
  return value === null || value === undefined ? "" : Number(value).toFixed(2);
}

function signedAmount(value, isRefund) {
  const numericValue = Number(value);
  return amount(isRefund ? -Math.abs(numericValue) : numericValue);
}

function detailRecord(receipt, lineItem, payment, index) {
  const isRefund = refundFlag(receipt) === "CREDIT";
  const lineAmount = isRefund ? -Math.abs(lineItem.lineAmount) : lineItem.lineAmount;
  const gstAmount = isRefund ? -Math.abs(lineItem.gstAmount) : lineItem.gstAmount;
  const fields = [
    "2",
    receipt.receiptBarcode,
    lineItem.lineNumber ?? index + 1,
    lineItem.itemCode,
    lineItem.itemDescription,
    lineItem.quantity,
    amount(lineItem.unitPrice),
    amount(lineAmount),
    amount(gstAmount),
    payment.paymentGl || lineItem.membershipProductGlCode,
    "",
  ];
  return {
    txType: "2",
    sourceReference: `${refundFlag(receipt)}-${receipt.receiptBarcode}`,
    recordLine: fields.map(pipeValue).join("|"),
    index,
  };
}

export function formatReceiptForPsv(receipt, generatedAt = new Date()) {
  const payment = paymentDetails(receipt);
  const refund = refundFlag(receipt);
  const isRefund = refund === "CREDIT";
  const headerFields = [
    "1",
    receipt.receiptBarcode,
    receipt.eventId,
    formatInvoiceDate(receipt.transactionDate),
    receipt.storeId,
    terminalId(receipt.tillId),
    payment.paymentMethod,
    payment.eftposId,
    payment.refNo,
    customerNumber(receipt),
    refund,
    signedAmount(receipt.financials.totalIncGst, isRefund),
    signedAmount(receipt.financials.totalGst, isRefund),
    signedAmount(receipt.financials.totalExGst, isRefund),
    "",
  ];
  const header = {
    txType: "1",
    sourceReference: `${refund}-${receipt.receiptBarcode}`,
    recordLine: headerFields.map(pipeValue).join("|"),
  };
  const details = (receipt.lineItems ?? []).map((lineItem, index) => detailRecord(receipt, lineItem, payment, index));
  const records = [header, ...details];
  const timestampParts = new Intl.DateTimeFormat("en-CA", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hourCycle: "h23",
    timeZone: "Australia/Perth",
  }).formatToParts(generatedAt);
  const timestamp = Object.fromEntries(timestampParts.map((part) => [part.type, part.value]));
  const fileTimestamp = `${timestamp.year}${timestamp.month}${timestamp.day}${timestamp.hour}${timestamp.minute}${timestamp.second}`;
  return {
    fileName: `CARSPOS_AR_INV_001_${fileTimestamp}.psv`,
    records,
    content: records.map((record) => record.recordLine).join("\r\n"),
  };
}
