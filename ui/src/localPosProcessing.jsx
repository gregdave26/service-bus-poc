import React, { useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogContent, DialogTitle, Divider, IconButton, MenuItem, Paper, Stack,
  Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import AddIcon from "@mui/icons-material/Add";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import { ReceiptGenerationForm as ReceiptGenerationTabs } from "./receiptGenerationForm.jsx";

async function getJson(url, options) {
  const response = await fetch(url, options);
  const body = await response.json();
  if (!response.ok) throw new Error(body.error || `Request failed (${response.status})`);
  return body;
}

function formatCanonicalJson(receipt) {
  const value = receipt?.receiptJson ?? receipt?.receipt ?? receipt;
  if (typeof value !== "string") return JSON.stringify(value, null, 2);
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

export function ReceiptDetailDialog({ receipt, onClose }) {
  const canonicalReceipt = receipt?.receipt ?? receipt;
  const fields = receipt ? [
    ["ReceiptBarcode", receipt.receiptBarcode],
    ["EventNumber", receipt.eventNumber],
    ["InvoiceDate", new Date(receipt.transactionDate).toLocaleString()],
    ["StoreNumber", receipt.storeId],
    ["TerminalID", receipt.terminalId],
    ["EFTPOSID", receipt.eftposId ?? "—"],
    ["RefNo", receipt.refNo ?? "—"],
    ["UserID", receipt.operatorId],
    ["Membership", `${canonicalReceipt?.customer?.membershipLevel ?? "—"}${canonicalReceipt?.customer?.membershipNumber ? ` (${canonicalReceipt.customer.membershipNumber})` : ""}`],
    ["CustomerNumber", receipt.customerNumber ?? canonicalReceipt?.customer?.customerNumber ?? "—"],
    ["Vehicle registration", canonicalReceipt?.customer?.vehicleRegistration ?? "—"],
    ["Vehicle VIN", canonicalReceipt?.customer?.vehicleVin ?? "—"],
    ["RefundFlag", receipt.refundFlag ?? canonicalReceipt?.financials?.refundFlag ?? "—"],
  ] : [];
  const lineItems = receipt?.lineItems ?? [];
  const payments = canonicalReceipt?.payments ?? [];

  return <Dialog open={Boolean(receipt)} onClose={onClose} fullWidth maxWidth="md" aria-labelledby="receipt-dialog-title">
    <DialogTitle id="receipt-dialog-title" sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
      Receipt details
      <IconButton aria-label="Close receipt details" onClick={onClose}><CloseIcon /></IconButton>
    </DialogTitle>
    <DialogContent dividers>
      <Typography variant="subtitle1" fontWeight={800} gutterBottom>POS_RECEIPT_EVENT row</Typography>
      <Table size="small" aria-label="Receipt fields">
        <TableBody>{fields.map(([label, value]) => <TableRow key={label}>
          <TableCell sx={{ width: "32%", fontWeight: 700, verticalAlign: "top" }}>{label}</TableCell>
          <TableCell sx={{ fontFamily: label === "Event ID" ? "monospace" : "inherit", wordBreak: "break-word" }}>{String(value ?? "")}</TableCell>
        </TableRow>)}</TableBody>
      </Table>
      <Typography variant="subtitle1" fontWeight={800} sx={{ mt: 3 }} gutterBottom>Payments ({receipt?.paymentType ?? "CASH"})</Typography>
      <Table size="small" aria-label="Receipt payments">
        <TableHead><TableRow><TableCell>Type</TableCell><TableCell align="right">Amount</TableCell><TableCell>EFTPOS terminal</TableCell><TableCell>EFTPOS ref</TableCell><TableCell>GL</TableCell></TableRow></TableHead>
        <TableBody>{payments.map((payment, index) => <TableRow key={index}>
          <TableCell>{payment.paymentType}</TableCell>
          <TableCell align="right">{payment.amount.toFixed(2)}</TableCell>
          <TableCell>{payment.eftposTerminalId ?? "—"}</TableCell>
          <TableCell>{payment.eftposRefNo ?? "—"}</TableCell>
          <TableCell>{payment.paymentGl}</TableCell>
        </TableRow>)}</TableBody>
      </Table>
      <Typography variant="subtitle1" fontWeight={800} sx={{ mt: 3 }} gutterBottom>POS_RECEIPT_LINE_ITEM rows</Typography>
      {lineItems.length === 0 ? <Typography color="text.secondary">No line items.</Typography> :
        <Table size="small" aria-label="Receipt line items">
          <TableHead><TableRow><TableCell>Item code</TableCell><TableCell>Description</TableCell><TableCell align="right">Qty</TableCell><TableCell align="right">Unit price</TableCell><TableCell align="right">Line amount</TableCell><TableCell align="right">GST</TableCell></TableRow></TableHead>
          <TableBody>{lineItems.map((lineItem) => <TableRow key={lineItem.lineNumber}>
            <TableCell sx={{ fontFamily: "monospace" }}>{lineItem.itemCode}</TableCell>
            <TableCell>{lineItem.itemDescription}</TableCell>
            <TableCell align="right">{lineItem.quantity}</TableCell>
            <TableCell align="right">{lineItem.unitPrice.toFixed(2)}</TableCell>
            <TableCell align="right">{lineItem.lineAmount.toFixed(2)}</TableCell>
            <TableCell align="right">{lineItem.gstAmount.toFixed(2)}</TableCell>
          </TableRow>)}</TableBody>
        </Table>}
      {canonicalReceipt?.financials && <Stack direction="row" justifyContent="flex-end" spacing={3} sx={{ mt: 2 }}>
        <Typography variant="body2">Ex GST: {canonicalReceipt.financials.totalExGst.toFixed(2)}</Typography>
        <Typography variant="body2">GST: {canonicalReceipt.financials.totalGst.toFixed(2)}</Typography>
        <Typography variant="subtitle2" fontWeight={800}>Inc GST: {canonicalReceipt.financials.totalIncGst.toFixed(2)}</Typography>
      </Stack>}
      <Typography variant="subtitle1" fontWeight={800} sx={{ mt: 3 }} gutterBottom>Exact canonical JSON</Typography>
      <Box component="pre" sx={{ m: 0, overflow: "auto", p: 2, bgcolor: "grey.900", color: "grey.100", borderRadius: 1, fontSize: 12 }}>
        {formatCanonicalJson(receipt)}
      </Box>
    </DialogContent>
  </Dialog>;
}

function emptyLineItemDraft() { return { itemCode: "", quantity: 1 }; }

function ReceiptGenerationForm({ catalog, onGenerate }) {
  const [storeId, setStoreId] = useState(catalog.stores[0]?.id ?? "");
  const [tillId, setTillId] = useState(catalog.tills[0]?.id ?? "");
  const [operatorId, setOperatorId] = useState(catalog.operators[0]?.id ?? "");
  const [paymentType, setPaymentType] = useState(catalog.paymentMethods.find((method) => method.id === "CASH")?.id ?? catalog.paymentMethods[0]?.id ?? "");
  const [mode, setMode] = useState("random");
  const [seed, setSeed] = useState("");
  const [itemCount, setItemCount] = useState("");
  const [lineItems, setLineItems] = useState([emptyLineItemDraft()]);
  const [membershipNumber, setMembershipNumber] = useState("");
  const [membershipLevel, setMembershipLevel] = useState("0");
  const [customerNumber, setCustomerNumber] = useState("");
  const [vehicleRegistration, setVehicleRegistration] = useState("");
  const [vehicleVin, setVehicleVin] = useState("");
  const [refundFlag, setRefundFlag] = useState("INVOICE");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  function updateLineItem(index, changes) {
    setLineItems((current) => current.map((item, itemIndex) => (itemIndex === index ? { ...item, ...changes } : item)));
  }
  function addLineItem() { setLineItems((current) => [...current, emptyLineItemDraft()]); }
  function removeLineItem(index) { setLineItems((current) => current.filter((_, itemIndex) => itemIndex !== index)); }

  async function submit(event) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    const request = {
      storeId, tillId, operatorId, paymentType, refundFlag,
      customer: {
        membershipNumber: membershipNumber || null,
        membershipLevel,
        customerNumber: customerNumber || null,
        vehicleRegistration: vehicleRegistration || null,
        vehicleVin: vehicleVin || null,
      },
    };
    if (seed.trim() !== "") request.seed = Number(seed);
    if (mode === "random") {
      if (itemCount.trim() !== "") request.itemCount = Number(itemCount);
    } else {
      request.lineItems = lineItems.filter((item) => item.itemCode).map((item) => ({ itemCode: item.itemCode, quantity: Number(item.quantity) || 1 }));
    }
    try {
      await onGenerate(request);
      setLineItems([emptyLineItemDraft()]);
    } catch (submitError) {
      setError(submitError.message);
    } finally {
      setSubmitting(false);
    }
  }

  return <Box component="form" onSubmit={submit}>
    <Typography variant="subtitle1" fontWeight={800} gutterBottom>Generate a receipt</Typography>
    {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2 }}>
      <TextField select label="Store" value={storeId} onChange={(event) => setStoreId(event.target.value)} fullWidth size="small">{catalog.stores.map((store) => <MenuItem key={store.id} value={store.id}>{store.name}</MenuItem>)}</TextField>
      <TextField select label="Till" value={tillId} onChange={(event) => setTillId(event.target.value)} fullWidth size="small">{catalog.tills.map((till) => <MenuItem key={till.id} value={till.id}>{till.name}</MenuItem>)}</TextField>
      <TextField select label="Operator" value={operatorId} onChange={(event) => setOperatorId(event.target.value)} fullWidth size="small">{catalog.operators.map((operator) => <MenuItem key={operator.id} value={operator.id}>{operator.name}</MenuItem>)}</TextField>
      <TextField select label="Payment type" value={paymentType} onChange={(event) => setPaymentType(event.target.value)} fullWidth size="small">{catalog.paymentMethods.map((method) => <MenuItem key={method.id} value={method.id}>{method.name}</MenuItem>)}</TextField>
    </Stack>
    <Typography variant="subtitle2" fontWeight={800} sx={{ mb: 1 }}>Receipt customer fields (optional)</Typography>
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2 }}>
      <TextField label="Membership number" value={membershipNumber} onChange={(event) => setMembershipNumber(event.target.value)} size="small" fullWidth />
      <TextField select label="Membership level" value={membershipLevel} onChange={(event) => setMembershipLevel(event.target.value)} size="small" fullWidth sx={{ minWidth: { sm: 220 } }} SelectProps={{ MenuProps: { PaperProps: { sx: { minWidth: 220 } } } }}>{catalog.membershipLevels.map((level) => <MenuItem key={level.id} value={level.id}>{level.name}</MenuItem>)}</TextField>
      <TextField label="Customer number" value={customerNumber} onChange={(event) => setCustomerNumber(event.target.value)} size="small" fullWidth />
    </Stack>
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2 }}>
      <TextField label="Vehicle registration" value={vehicleRegistration} onChange={(event) => setVehicleRegistration(event.target.value)} size="small" sx={{ width: { xs: "100%", sm: 180 }, flex: { xs: "1 1 auto", sm: "0 0 180px" } }} helperText="Target length: 10 characters (not enforced yet)" />
      <TextField label="Vehicle VIN" value={vehicleVin} onChange={(event) => setVehicleVin(event.target.value)} size="small" fullWidth />
      <TextField select label="Refund flag" value={refundFlag} onChange={(event) => setRefundFlag(event.target.value)} size="small" sx={{ width: { xs: "100%", sm: 180 }, minWidth: { sm: 180 }, flex: { xs: "1 1 auto", sm: "0 0 180px" } }} SelectProps={{ MenuProps: { PaperProps: { sx: { minWidth: 180 } } } }}>
        <MenuItem value="INVOICE">INVOICE</MenuItem>
        <MenuItem value="CREDIT">CREDIT</MenuItem>
      </TextField>
    </Stack>
    <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
      <Button size="small" variant={mode === "random" ? "contained" : "outlined"} onClick={() => setMode("random")}>Random items</Button>
      <Button size="small" variant={mode === "explicit" ? "contained" : "outlined"} onClick={() => setMode("explicit")}>Choose items</Button>
    </Stack>
    {mode === "random" ?
      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2 }}>
        <TextField label="Item count (optional)" type="number" size="small" value={itemCount} onChange={(event) => setItemCount(event.target.value)} helperText="Leave blank for a random 1-5 items" inputProps={{ min: 1, max: 20 }} />
        <TextField label="Seed (optional)" type="number" size="small" value={seed} onChange={(event) => setSeed(event.target.value)} helperText="Reuse a seed to reproduce the same items and customer" inputProps={{ min: 0 }} />
      </Stack> :
      <Stack spacing={1} sx={{ mb: 2 }}>
        {lineItems.map((lineItem, index) => <Stack direction="row" spacing={1} key={index} alignItems="center">
          <TextField select label="Product" value={lineItem.itemCode} onChange={(event) => updateLineItem(index, { itemCode: event.target.value })} size="small" sx={{ minWidth: 240 }}>
            {catalog.products.map((product) => <MenuItem key={product.itemCode} value={product.itemCode}>{product.itemDescription} ({product.unitPrice.toFixed(2)})</MenuItem>)}
          </TextField>
          <TextField label="Qty" type="number" size="small" value={lineItem.quantity} onChange={(event) => updateLineItem(index, { quantity: event.target.value })} sx={{ width: 90 }} inputProps={{ min: 1, max: 99 }} />
          <IconButton aria-label="Remove line item" onClick={() => removeLineItem(index)} disabled={lineItems.length === 1}><CloseIcon fontSize="small" /></IconButton>
        </Stack>)}
        <Button size="small" startIcon={<AddIcon />} onClick={addLineItem} sx={{ alignSelf: "flex-start" }}>Add product</Button>
      </Stack>}
    <Button type="submit" variant="contained" disabled={submitting}>Generate receipt</Button>
  </Box>;
}

export function PosProcessing({ catalog, receipts, onGenerate }) {
  const stages = [
    { name: "POS Receipt", description: "Sale captured locally" },
    { name: "POS_RECEIPT_EVENT", description: "Receipt header persisted to Cardzoids (ODS)" },
    { name: "POS_RECEIPT_LINE_ITEM", description: "Receipt lines persisted to Cardzoids (ODS)" },
    { name: "Reporting / Finance export", description: "Not part of this PoC" },
  ];
  const [selectedReceipt, setSelectedReceipt] = useState(null);
  const [detailError, setDetailError] = useState(null);
  const [flow, setFlow] = useState({ state: "idle", activeStage: -1 });

  function wait(milliseconds) {
    return new Promise((resolve) => setTimeout(resolve, milliseconds));
  }

  async function generateReceipt(request) {
    setFlow({ state: "processing", activeStage: 0 });
    try {
      await onGenerate(request);
      for (let stage = 0; stage <= 2; stage += 1) {
        setFlow({ state: "processing", activeStage: stage });
        await wait(650);
      }
      setFlow({ state: "completed", activeStage: 2 });
    } catch (error) {
      setFlow({ state: "error", activeStage: 0 });
      throw error;
    }
  }

  async function openReceipt(id) {
    try {
      setSelectedReceipt(await getJson(`/api/pos/receipts/${id}`));
    } catch (error) {
      setDetailError(error.message);
    }
  }

  return <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}>
    <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" gap={1} mb={3}>
      <Box><Typography variant="h6">Local POS processing</Typography><Typography variant="body2" color="text.secondary">Generate simulated receipts backed by the local SQLite POS_RECEIPT_EVENT / POS_RECEIPT_LINE_ITEM tables</Typography></Box>
      <Chip icon={<ReceiptLongIcon />} label={`${receipts.length} receipt${receipts.length === 1 ? "" : "s"}`} color="primary" size="small" />
    </Stack>
    <Stack direction={{ xs: "column", md: "row" }} alignItems="stretch" spacing={1}>
      {stages.map((stage, index) => {
        const completed = flow.state === "completed" && index <= 2;
        const active = flow.state === "processing" && index === flow.activeStage;
        const unavailable = index === 3;
        return <React.Fragment key={stage.name}>
          <Paper variant="outlined" sx={{
            p: 2,
            flex: 1,
            width: "100%",
            minHeight: 92,
            bgcolor: unavailable ? "action.hover" : completed ? "success.50" : active ? "primary.50" : "background.default",
            borderColor: unavailable ? "divider" : completed ? "success.main" : active ? "primary.main" : "divider",
            borderWidth: active || completed ? 2 : 1,
            transition: "background-color 300ms ease, border-color 300ms ease, box-shadow 300ms ease",
            boxShadow: active ? 3 : 0,
          }}>
            <Stack direction="row" spacing={1} alignItems="flex-start">
              {completed ? <CheckCircleIcon color="success" fontSize="small" /> : <Typography color={active ? "primary" : "text.disabled"} fontWeight={800}>{index + 1}</Typography>}
              <Box>
                <Typography variant="subtitle2" fontWeight={800}>{stage.name}</Typography>
                <Typography variant="caption" color={unavailable ? "text.secondary" : active ? "primary.main" : "text.secondary"}>
                  {unavailable ? stage.description : active ? "Processing…" : completed ? "Persisted" : stage.description}
                </Typography>
              </Box>
            </Stack>
          </Paper>
          {index < stages.length - 1 && <Typography aria-hidden="true" color={completed ? "success.main" : "text.disabled"} fontSize={24} sx={{ alignSelf: "center", transform: { xs: "rotate(90deg)", md: "none" }, transition: "color 300ms ease" }}>→</Typography>}
        </React.Fragment>;
      })}
    </Stack>
    {flow.state === "processing" && <Alert severity="info" sx={{ mt: 2 }}>Persisting the simulated sale. The flow will remain highlighted briefly so each completed step is visible.</Alert>}
    {flow.state === "completed" && <Alert severity="success" sx={{ mt: 2 }}>Receipt and its event and line items are persisted in Cardzoids (ODS). Reporting / Finance export is not implemented in this PoC.</Alert>}
    <Divider sx={{ my: 3 }} />
    {catalog ? <ReceiptGenerationTabs catalog={catalog} onGenerate={generateReceipt} /> : <Typography color="text.secondary">Loading catalog…</Typography>}
    <Divider sx={{ my: 3 }} />
    <Box>
      <Typography variant="subtitle1" fontWeight={800}>Persisted receipts</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        The latest rows read from the local SQLite database. This view refreshes with the dashboard polling.
      </Typography>
      {detailError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setDetailError(null)}>{detailError}</Alert>}
      {receipts.length === 0 ? <Typography color="text.secondary" sx={{ py: 3, textAlign: "center" }}>No receipts generated yet.</Typography> :
        <Box sx={{ overflowX: "auto" }}>
          <Table size="small" aria-label="Persisted POS receipts" sx={{ minWidth: 980 }}>
            <TableHead><TableRow>
              <TableCell>Receipt barcode</TableCell>
              <TableCell>Store</TableCell>
              <TableCell>Payment type</TableCell>
              <TableCell>Transaction date</TableCell>
              <TableCell align="right">Total (inc GST)</TableCell>
              <TableCell>Status</TableCell>
            </TableRow></TableHead>
            <TableBody>{receipts.map((receipt) => <TableRow key={receipt.eventId} hover onClick={() => openReceipt(receipt.eventId)} onKeyDown={(keyboardEvent) => { if (keyboardEvent.key === "Enter" || keyboardEvent.key === " ") { keyboardEvent.preventDefault(); openReceipt(receipt.eventId); } }} tabIndex={0} role="button" aria-label={`View details for receipt ${receipt.receiptBarcode}`} sx={{ cursor: "pointer" }}>
              <TableCell sx={{ fontWeight: 700 }}>{receipt.receiptBarcode}</TableCell>
              <TableCell>{receipt.storeId}</TableCell>
              <TableCell>{receipt.paymentType}</TableCell>
              <TableCell sx={{ whiteSpace: "nowrap" }}>{new Date(receipt.transactionDate).toLocaleString()}</TableCell>
              <TableCell align="right">{receipt.totalIncGst.toFixed(2)}</TableCell>
              <TableCell><Chip label={receipt.status} color="success" size="small" /></TableCell>
            </TableRow>)}</TableBody>
          </Table>
        </Box>}
    </Box>
    <ReceiptDetailDialog receipt={selectedReceipt} onClose={() => setSelectedReceipt(null)} />
  </Paper>;
}
