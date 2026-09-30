import React, { useMemo, useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogContent, DialogTitle, Divider, IconButton, MenuItem, Paper, Stack,
  Table, TableBody, TableCell, TableHead, TableRow, TableSortLabel, TextField, Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import AddIcon from "@mui/icons-material/Add";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import DownloadIcon from "@mui/icons-material/Download";
import InsertDriveFileIcon from "@mui/icons-material/InsertDriveFile";
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

function compareReceiptValues(left, right, field) {
  const leftValue = field === "transactionDate" ? Date.parse(left) : left;
  const rightValue = field === "transactionDate" ? Date.parse(right) : right;
  if (typeof leftValue === "number" && typeof rightValue === "number") return leftValue - rightValue;
  return String(leftValue ?? "").localeCompare(String(rightValue ?? ""), undefined, { numeric: true, sensitivity: "base" });
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

function downloadPsv(exportData) {
  const blob = new Blob([exportData.content], { type: "text/plain;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = exportData.fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

function PsvExportDialog({ exportData, onClose }) {
  return <Dialog open={Boolean(exportData)} onClose={onClose} fullWidth maxWidth="lg" aria-labelledby="psv-dialog-title">
    <DialogTitle id="psv-dialog-title" sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
      D365 PSV export preview
      <IconButton aria-label="Close PSV export preview" onClick={onClose}><CloseIcon /></IconButton>
    </DialogTitle>
    <DialogContent dividers>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        This is a simulated POS event-to-PSV conversion based on the supplied `sp_Publish_POSReceipts` mapping. The production queue, Function, and Blob steps are not executed here.
      </Typography>
      <Typography variant="subtitle2" fontWeight={800} gutterBottom>{exportData?.fileName}</Typography>
      <Box component="pre" sx={{ m: 0, overflow: "auto", p: 2, bgcolor: "grey.900", color: "grey.100", borderRadius: 1, fontSize: 12, whiteSpace: "pre" }}>
        {exportData?.content}
      </Box>
      <Stack direction="row" justifyContent="flex-end" sx={{ mt: 2 }}>
        <Button variant="contained" startIcon={<DownloadIcon />} onClick={() => downloadPsv(exportData)}>
          Export PSV to disk
        </Button>
      </Stack>
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
    { name: "POS_RECEIPT_EVENT + POS_RECEIPT_LINE_ITEM", description: "Receipt header and lines persisted to Cardzoids (ODS)" },
    { name: "Reporting / Finance Export", description: "Generate the D365 PSV file" },
  ];
  const [selectedReceipt, setSelectedReceipt] = useState(null);
  const [detailError, setDetailError] = useState(null);
  const [flow, setFlow] = useState({ state: "idle", activeStage: -1 });
  const [selectedExport, setSelectedExport] = useState(null);
  const [exportedReceipts, setExportedReceipts] = useState(() => new Set());
  const [receiptSort, setReceiptSort] = useState({ field: "transactionDate", direction: "desc" });
  const sortedReceipts = useMemo(() => [...receipts].sort((left, right) => {
    const leftValue = receiptSort.field === "status"
      ? (exportedReceipts.has(left.receiptBarcode) ? "Exported" : "Not exported")
      : left[receiptSort.field];
    const rightValue = receiptSort.field === "status"
      ? (exportedReceipts.has(right.receiptBarcode) ? "Exported" : "Not exported")
      : right[receiptSort.field];
    const comparison = compareReceiptValues(leftValue, rightValue, receiptSort.field);
    return receiptSort.direction === "asc" ? comparison : -comparison;
  }), [receipts, receiptSort, exportedReceipts]);

  function wait(milliseconds) {
    return new Promise((resolve) => setTimeout(resolve, milliseconds));
  }

  async function generateReceipt(request) {
    setFlow({ state: "processing", activeStage: 0 });
    try {
      const receipt = await onGenerate(request);
      setFlow({ state: "processing", activeStage: 0, receipt });
      for (let stage = 0; stage <= 1; stage += 1) {
        setFlow({ state: "processing", activeStage: stage, receipt });
        await wait(650);
      }
      setFlow({ state: "completed", activeStage: 1, receipt });
    } catch (error) {
      setFlow({ state: "error", activeStage: 0 });
      throw error;
    }
  }

  async function exportReceipt() {
    const receipt = flow.receipt ?? receipts[0];
    if (!receipt) return;
    try {
      setDetailError(null);
      setFlow((current) => ({ ...current, state: "processing", activeStage: 2 }));
      const exportData = await getJson(`/api/pos/receipts/${receipt.receiptBarcode}/export`);
      await wait(650);
      setExportedReceipts((current) => new Set(current).add(receipt.receiptBarcode));
      setFlow((current) => ({ ...current, state: "completed", activeStage: 2, receipt, exportData }));
    } catch (error) {
      setFlow((current) => ({ ...current, state: "error", activeStage: 2 }));
      setDetailError(error.message);
    }
  }

  async function openReceipt(id) {
    try {
      setSelectedReceipt(await getJson(`/api/pos/receipts/${id}`));
    } catch (error) {
      setDetailError(error.message);
    }
  }

  function changeReceiptSort(field) {
    setReceiptSort((current) => ({
      field,
      direction: current.field === field && current.direction === "asc" ? "desc" : "asc",
    }));
  }

  function sortableHeader(label, field, align = "left") {
    return <TableCell align={align}>
      <TableSortLabel
        active={receiptSort.field === field}
        direction={receiptSort.field === field ? receiptSort.direction : "asc"}
        onClick={() => changeReceiptSort(field)}
      >
        {label}
      </TableSortLabel>
    </TableCell>;
  }

  return <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}>
    <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" gap={1} mb={3}>
      <Box><Typography variant="h6">Local POS processing</Typography><Typography variant="body2" color="text.secondary">Generate simulated receipts backed by the local SQLite POS_RECEIPT_EVENT / POS_RECEIPT_LINE_ITEM tables</Typography></Box>
      <Chip icon={<ReceiptLongIcon />} label={`${receipts.length} receipt${receipts.length === 1 ? "" : "s"}`} color="primary" size="small" />
    </Stack>
    <Stack direction={{ xs: "column", md: "row" }} alignItems="stretch" spacing={1}>
      {stages.map((stage, index) => {
        const completed = flow.state === "completed" && index <= (flow.exportData ? 2 : 1);
        const active = flow.state === "processing" && index === flow.activeStage;
        const exportStage = index === 2;
        const statusText = active
          ? (exportStage ? "Generating PSV…" : "Processing…")
          : completed
            ? (exportStage ? "PSV file ready" : "Complete")
            : exportStage
              ? "Ready to export"
              : stage.description;
        return <React.Fragment key={stage.name}>
          <Paper variant="outlined" sx={{
            p: 2,
            flex: 1,
            width: "100%",
            minHeight: 92,
            bgcolor: completed ? "success.50" : active ? "primary.50" : "background.default",
            borderColor: completed ? "success.main" : active ? "primary.main" : "divider",
            borderWidth: active || completed ? 2 : 1,
            transition: "background-color 300ms ease, border-color 300ms ease, box-shadow 300ms ease",
            boxShadow: active ? 3 : 0,
          }}>
            <Stack direction="row" spacing={1} alignItems="flex-start">
              {completed ? <CheckCircleIcon color="success" fontSize="small" /> : <Typography color={active ? "primary" : "text.disabled"} fontWeight={800}>{index + 1}</Typography>}
              <Box>
                <Typography variant="subtitle2" fontWeight={800}>{stage.name}</Typography>
                <Typography variant="caption" color={active ? "primary.main" : "text.secondary"}>
                  {statusText}
                </Typography>
                {!exportStage && flow.receipt?.receiptBarcode && <Typography variant="caption" display="block" sx={{ mt: 0.5, fontFamily: "monospace", fontSize: "0.7rem", fontWeight: 700 }}>
                  Receipt barcode: {flow.receipt.receiptBarcode}
                </Typography>}
                {exportStage && flow.exportData?.fileName && <Stack direction="row" alignItems="center" spacing={0.5} sx={{ mt: 0.5, minWidth: 0 }}>
                  <Typography variant="caption" sx={{ fontFamily: "monospace", fontSize: "0.7rem", overflowWrap: "anywhere" }}>
                    {flow.exportData.fileName}
                  </Typography>
                  <IconButton aria-label="View PSV file" size="small" onClick={() => setSelectedExport(flow.exportData)} sx={{ flexShrink: 0 }}>
                    <img className="psv-ready-icon" src="/psv-ready-icon.png" alt="" />
                  </IconButton>
                </Stack>}
              </Box>
            </Stack>
          </Paper>
          {index < stages.length - 1 && <Typography aria-hidden="true" color={completed ? "success.main" : "text.disabled"} fontSize={24} sx={{ alignSelf: "center", transform: { xs: "rotate(90deg)", md: "none" }, transition: "color 300ms ease" }}>→</Typography>}
        </React.Fragment>;
      })}
    </Stack>
    {flow.state === "processing" && <Alert severity="info" sx={{ mt: 2 }}>Processing the simulated sale and export.</Alert>}
    {flow.state === "completed" && flow.exportData && <Alert severity="success" sx={{ mt: 2 }}>Receipt data is persisted and the PSV file is available for viewing in the Reporting / Finance Export step.</Alert>}
    <Divider sx={{ my: 3 }} />
    {catalog ? <ReceiptGenerationTabs catalog={catalog} onGenerate={generateReceipt} /> : <Typography color="text.secondary">Loading catalog…</Typography>}
    <Divider sx={{ my: 3 }} />
    <Box>
      <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 0.5 }}>
        <Typography variant="subtitle1" fontWeight={800}>Persisted receipts</Typography>
        <Button
          size="small"
          variant="contained"
          startIcon={<InsertDriveFileIcon />}
          onClick={exportReceipt}
          disabled={receipts.length === 0 || flow.state === "processing"}
        >
          Export to D365 PSV
        </Button>
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        The latest rows read from the local SQLite database. This view refreshes with the dashboard polling.
      </Typography>
      {detailError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setDetailError(null)}>{detailError}</Alert>}
      {receipts.length === 0 ? <Typography color="text.secondary" sx={{ py: 3, textAlign: "center" }}>No receipts generated yet.</Typography> :
        <Box sx={{ overflowX: "auto" }}>
          <Table size="small" aria-label="Persisted POS receipts" sx={{ minWidth: 980 }}>
            <TableHead><TableRow>
              {sortableHeader("Receipt barcode", "receiptBarcode")}
              {sortableHeader("Store", "storeId")}
              {sortableHeader("Payment type", "paymentType")}
              {sortableHeader("Transaction date", "transactionDate")}
              {sortableHeader("Line items", "lineItemCount", "right")}
              {sortableHeader("Total (inc GST)", "totalIncGst", "right")}
              {sortableHeader("Status", "status")}
            </TableRow></TableHead>
            <TableBody>{sortedReceipts.map((receipt) => <TableRow key={receipt.eventId} hover onClick={() => openReceipt(receipt.eventId)} onKeyDown={(keyboardEvent) => { if (keyboardEvent.key === "Enter" || keyboardEvent.key === " ") { keyboardEvent.preventDefault(); openReceipt(receipt.eventId); } }} tabIndex={0} role="button" aria-label={`View details for receipt ${receipt.receiptBarcode}`} sx={{ cursor: "pointer" }}>
              <TableCell sx={{ fontWeight: 700 }}>{receipt.receiptBarcode}</TableCell>
              <TableCell>{receipt.storeId}</TableCell>
              <TableCell>{receipt.paymentType}</TableCell>
              <TableCell sx={{ whiteSpace: "nowrap" }}>{new Date(receipt.transactionDate).toLocaleString()}</TableCell>
              <TableCell align="right">{Number(receipt.lineItemCount ?? 0)}</TableCell>
              <TableCell align="right">{receipt.totalIncGst.toFixed(2)}</TableCell>
              <TableCell>
                <Chip
                  label={exportedReceipts.has(receipt.receiptBarcode) ? "Exported" : "Not exported"}
                  color={exportedReceipts.has(receipt.receiptBarcode) ? "success" : "default"}
                  size="small"
                />
              </TableCell>
            </TableRow>)}</TableBody>
          </Table>
        </Box>}
    </Box>
    <ReceiptDetailDialog receipt={selectedReceipt} onClose={() => setSelectedReceipt(null)} />
    <PsvExportDialog exportData={selectedExport} onClose={() => setSelectedExport(null)} />
  </Paper>;
}
