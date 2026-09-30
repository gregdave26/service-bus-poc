import React, { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  Alert,
  AppBar,
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Button,
  Card,
  CardActionArea,
  Checkbox,
  Chip,
  CircularProgress,
  Collapse,
  Container,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  IconButton,
  List,
  ListItemButton,
  ListItemText,
  Paper,
  Snackbar,
  Stack,
  MenuItem,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Toolbar,
  Typography,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import CloseIcon from "@mui/icons-material/Close";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import AddIcon from "@mui/icons-material/Add";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import { createTheme, ThemeProvider } from "@mui/material/styles";
import "./styles.css";

const theme = createTheme({
  palette: {
    mode: "light",
    primary: { main: "#6750a4" },
    secondary: { main: "#006a60" },
    background: { default: "#f7f2fa", paper: "#fffbfe" },
  },
  shape: { borderRadius: 12 },
  typography: { fontFamily: "Inter, Roboto, system-ui, sans-serif" },
});

const drafts = [
  { id: "contact-updated", name: "Contact updated", type: "ContactUpdated", folder: "Contact events", data: { contactId: "1042c5b8-1a3d-4d7a-9f02-7c4f7e2b8c11", firstName: "Ada", lastName: "Lovelace", phone: "0400000000", email: "ada@example.com", hasInsurance: true, hasParksResorts: false, hasCarwashProduct: true } },
  { id: "product-holding-change", name: "Product holding change", type: "ProductHoldingChange", folder: "Contact events", data: { contactId: "1088c5b8-2b4e-4e8b-a013-8d5f8f3c9d22", holdingId: "holding-1088", productType: "insurance", action: "created" } },
  { id: "regression", name: "Routing regression", type: "ContactUpdated", folder: "Regression checks", data: { contactId: "c0ffee00-0000-4000-8000-000000000099", firstName: "Test", lastName: "Contact", phone: "0400000099", email: "test@example.com", hasInsurance: true, hasParksResorts: true, hasCarwashProduct: true } },
];

function fieldsFor(type, config) { return config.messageTypes?.[type]?.Fields ?? []; }
function emptyData(fields) { return Object.fromEntries(fields.map((field) => [field.Name, field.Type === "boolean" ? false : ""])); }
function createUuid() { return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`; }
function normalizedName(name) { return name.trim().toLowerCase(); }

async function getJson(url, options) {
  const response = await fetch(url, options);
  const body = await response.json();
  if (!response.ok) throw new Error(body.error || `Request failed (${response.status})`);
  return body;
}

function displayName(service, config) {
  if (!service) return "Unknown service";
  if (service === "producer") return config.producerLabels?.DisplayName || "Message Publisher";
  return config.subscriberLabels?.[service]?.DisplayName || service.replace(/-/g, " ").replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function messageType(payload, fallback) {
  try {
    return JSON.parse(payload || "{}").type || fallback;
  } catch {
    return fallback;
  }
}

function MessageDialog({ service, messages, config, onClose }) {
  const sent = service === "producer";
  const filtered = messages.filter((message) => message.serviceName === service && message.direction === (sent ? "sent" : "received"));
  return <Dialog open={Boolean(service)} onClose={onClose} fullWidth maxWidth="lg" aria-labelledby="service-dialog-title">
    <DialogTitle id="service-dialog-title" sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
      {displayName(service, config)}
      <IconButton aria-label="Close service details" onClick={onClose}><CloseIcon /></IconButton>
    </DialogTitle>
    <DialogContent dividers>
      <Typography variant="h6" gutterBottom>Message activity</Typography>
      <Typography color="text.secondary" variant="body2" gutterBottom>{sent ? "Messages published by this service." : "Messages received by this service subscription."}</Typography>
      {filtered.length === 0 ? <Typography color="text.secondary" sx={{ py: 4, textAlign: "center" }}>No {sent ? "published" : "received"} messages for this service yet.</Typography> :
        <Stack spacing={1}>{filtered.slice().reverse().map((message) => <Paper key={message.messageId} variant="outlined" sx={{ p: 1.5 }}>
          <Typography variant="subtitle2">{message.eventId}</Typography>
          <Typography variant="caption" color="text.secondary">{new Date(message.timestamp).toLocaleString()} · {message.direction}</Typography>
          <Box component="pre" sx={{ mt: 1, mb: 0, overflow: "auto", p: 1.5, bgcolor: "grey.900", color: "grey.100", borderRadius: 1, fontSize: 12 }}>{message.payload}</Box>
        </Paper>)}</Stack>}
    </DialogContent>
  </Dialog>;
}

function NewMessageDialog({ open, config, existingNames, onClose, onCreate }) {
  const types = Object.entries(config.messageTypes ?? {});
  const [name, setName] = useState("");
  const [type, setType] = useState(types[0]?.[0] ?? "ContactUpdated");
  const [values, setValues] = useState({});
  const selectedFields = fieldsFor(type, config);
  const duplicate = existingNames.some((existingName) => normalizedName(existingName) === normalizedName(name));
  useEffect(() => {
    if (open) {
      const base = "Untitled draft";
      let candidate = base;
      let suffix = 2;
      while (existingNames.some((existingName) => normalizedName(existingName) === normalizedName(candidate))) candidate = `${base} ${suffix++}`;
      setName(candidate);
      setType(types[0]?.[0] ?? "ContactUpdated");
      setValues(emptyData(fieldsFor(types[0]?.[0] ?? "ContactUpdated", config)));
    }
  }, [open]);
  function submit(event) {
    event.preventDefault();
    if (!name.trim() || duplicate) return;
    onCreate({ name: name.trim(), type, data: values });
  }
  return <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm" aria-labelledby="new-message-dialog-title">
    <Box component="form" onSubmit={submit}>
      <DialogTitle id="new-message-dialog-title">New message</DialogTitle>
      <DialogContent dividers><Stack spacing={2}>
        <TextField autoFocus label="Message name" value={name} onChange={(event) => setName(event.target.value)} error={duplicate} helperText={duplicate ? "A message with this name already exists." : "Choose a unique name for this message."} required />
        <TextField select label="Message type" value={type} onChange={(event) => { setType(event.target.value); setValues(emptyData(fieldsFor(event.target.value, config))); }}>{types.map(([key, value]) => <MenuItem key={key} value={key}>{value.DisplayName || key}</MenuItem>)}</TextField>
        <Typography variant="subtitle2">{types.find(([key]) => key === type)?.[1]?.DisplayName || type} fields</Typography>
        {selectedFields.filter((field) => field.Type !== "boolean").map((field) => <TextField key={field.Name} select={Array.isArray(field.Options) && field.Options.length > 0} label={field.Label || field.Name} value={values[field.Name] ?? ""} onChange={(event) => setValues((current) => ({ ...current, [field.Name]: event.target.value }))} required={field.Required} fullWidth size="small" type={field.Type || "text"}>{field.Options?.map((option) => <MenuItem key={option} value={option}>{option}</MenuItem>)}</TextField>)}
        <Stack direction="row" flexWrap="wrap">{selectedFields.filter((field) => field.Type === "boolean").map((field) => <FormControlLabel key={field.Name} control={<Checkbox checked={Boolean(values[field.Name])} onChange={(event) => setValues((current) => ({ ...current, [field.Name]: event.target.checked }))} />} label={field.Label || field.Name} />)}</Stack>
      </Stack></DialogContent>
      <Stack direction="row" justifyContent="flex-end" spacing={1} sx={{ p: 2 }}><Button onClick={onClose}>Cancel</Button><Button type="submit" variant="contained" disabled={!name.trim() || duplicate}>Create message</Button></Stack>
    </Box>
  </Dialog>;
}

function MessageEditorDialog({ open, draft, form, fields, config, messageName, duplicateName, onNameChange, onChange, onSave, onPublish, onClose }) {
    const configurationErrors = config.messageTypes?.[draft.type]?.ConfigurationErrors ?? [];
    const preview = JSON.stringify({ type: draft.type, data: form }, null, 2);
    return <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm" aria-labelledby="message-editor-dialog-title">
      <Box component="form" onSubmit={onPublish}>
        <DialogTitle id="message-editor-dialog-title" sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          Message editor
          <IconButton aria-label="Close message editor" onClick={onClose}><CloseIcon /></IconButton>
        </DialogTitle>
        <DialogContent dividers>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>Edit the selected draft, then save it or publish it through the Producer.</Typography>
          <Stack spacing={2}>
            {configurationErrors.length > 0 && <Alert severity="error" role="alert">
              <Typography variant="subtitle2">Message form configuration error</Typography>
              <Box component="ul" sx={{ m: 0, pl: 2 }}>{configurationErrors.map((configurationError) => <li key={configurationError}>{configurationError}</li>)}</Box>
            </Alert>}
            <TextField label="Message name" value={messageName} onChange={(event) => onNameChange(event.target.value)} error={duplicateName} helperText={duplicateName ? "A message with this name already exists." : "Choose a unique name for this message."} required fullWidth />
            <TextField label="Contact ID" value={form.contactId ?? ""} InputProps={{ readOnly: true }} helperText="Generated automatically." fullWidth />
            <Typography variant="subtitle2">{config.messageTypes?.[draft.type]?.DisplayName || draft.type}</Typography>
            {fields.filter((field) => field.Name !== "contactId" && field.Type !== "boolean").map((field) => <TextField key={field.Name} name={field.Name} select={Array.isArray(field.Options) && field.Options.length > 0} label={field.Label || field.Name} value={form[field.Name] ?? ""} onChange={onChange} required={field.Required} fullWidth size="small" type={field.Type || "text"}>{field.Options?.map((option) => <MenuItem key={option} value={option}>{option}</MenuItem>)}</TextField>)}
            <Stack direction="row" flexWrap="wrap">{fields.filter((field) => field.Type === "boolean").map((field) => <FormControlLabel key={field.Name} control={<Checkbox name={field.Name} checked={Boolean(form[field.Name])} onChange={onChange} />} label={field.Label || field.Name} />)}</Stack>
            <Accordion defaultExpanded disableGutters variant="outlined">
              <AccordionSummary expandIcon={<ExpandMoreIcon />} aria-controls="message-preview-content" id="message-preview-header">
                <Typography fontWeight={600}>Preview</Typography>
              </AccordionSummary>
              <AccordionDetails>
                <TextField
                  aria-label="Message JSON preview"
                  value={preview}
                  fullWidth
                  multiline
                  minRows={8}
                  maxRows={18}
                  InputProps={{ readOnly: true }}
                  sx={{ "& textarea": { fontFamily: "monospace", fontSize: 12 } }}
                />
              </AccordionDetails>
            </Accordion>
          </Stack>
        </DialogContent>
        <Stack direction="row" justifyContent="flex-end" spacing={1} sx={{ p: 2 }}>
          <Button onClick={onClose}>Cancel</Button>
          <Button variant="outlined" onClick={onSave} disabled={!messageName.trim() || duplicateName || configurationErrors.length > 0}>Save message</Button>
          <Button type="submit" variant="contained" disabled={!messageName.trim() || duplicateName || configurationErrors.length > 0}>Publish</Button>
        </Stack>
      </Box>
    </Dialog>;
}

function ServiceCard({ service, config, selected, onClick }) {
  const connected = String(service.state).toLowerCase() !== "offline";
  const name = displayName(service.serviceName, config);
  const labels = service.serviceName === "producer" ? config.producerLabels : config.subscriberLabels?.[service.serviceName];
  const stateLabel = connected ? (labels?.ConnectedLabel || "Connected") : (labels?.OfflineLabel || "Offline");
  const countLabel = labels?.MessagesHandledLabel || "handled";
  return <Card variant="outlined" sx={{ borderColor: selected ? "primary.main" : "divider", bgcolor: selected ? "primary.light" : "background.paper" }}>
    <CardActionArea onClick={service.selectable === false ? undefined : onClick} disabled={service.selectable === false} sx={{ p: 2, minHeight: 132 }}>
      <Stack spacing={1}>
        <Stack direction="row" justifyContent="space-between"><Typography variant="caption" color="text.secondary">{service.infrastructure ? "Infrastructure" : "Service"}</Typography><Typography color="primary.main">{service.icon || "◎"}</Typography></Stack>
        <Typography variant="h6">{name}</Typography>
        <Stack direction="row" spacing={1} alignItems="center"><Box component="span" className={`status-dot ${connected ? "connected" : "offline"}`} /><Typography variant="body2" color="text.secondary">{stateLabel}{service.messagesHandled === null ? "" : ` · ${service.messagesHandled ?? 0} ${countLabel}`}</Typography></Stack>
      </Stack>
    </CardActionArea>
  </Card>;
}

function ActivityStream({ messages, statuses, config }) {
  const statusMap = useMemo(() => new Map(statuses.map((status) => [status.serviceName, status])), [statuses]);
  const [expanded, setExpanded] = useState(null);
  const activity = messages.slice().sort((a, b) => Date.parse(b.timestamp) - Date.parse(a.timestamp));
  return <Paper variant="outlined" sx={{ overflow: "hidden" }}>
    <Box sx={{ p: 2, display: "flex", justifyContent: "space-between" }}><Box><Typography variant="h6">Event activity stream</Typography><Typography variant="body2" color="text.secondary">Published and received events from the dashboard</Typography></Box><Chip label="Live" color="success" size="small" /></Box>
    <Divider />
    {activity.length === 0 ? <Typography color="text.secondary" sx={{ p: 4, textAlign: "center" }}>No published or received events yet.</Typography> :
      <Box sx={{ overflowX: "auto" }}><Table size="small" aria-label="Event activity stream" sx={{ minWidth: 760 }}><TableHead><TableRow><TableCell>Event</TableCell><TableCell>Service</TableCell><TableCell>Direction</TableCell><TableCell>Status</TableCell><TableCell>Time</TableCell><TableCell /></TableRow></TableHead><TableBody>
        {activity.map((message, index) => { const rowId = String(index); const open = expanded === rowId; const status = message.direction === "sent" ? "Published" : (statusMap.get(message.serviceName)?.state || "Observed"); return <React.Fragment key={rowId}>
          <TableRow hover onClick={() => setExpanded(open ? null : rowId)} sx={{ cursor: "pointer" }}><TableCell sx={{ fontWeight: 700 }}>{messageType(message.payload, message.eventId)}</TableCell><TableCell>{displayName(message.serviceName, config)}</TableCell><TableCell><Chip size="small" label={message.direction === "sent" ? "Published" : "Received"} color={message.direction === "sent" ? "primary" : "secondary"} /></TableCell><TableCell><Chip size="small" label={status} color={status === "Offline" ? "warning" : "default"} /></TableCell><TableCell>{new Date(message.timestamp).toLocaleString()}</TableCell><TableCell>{open ? <ExpandLessIcon /> : <ExpandMoreIcon />}</TableCell></TableRow>
          <TableRow><TableCell colSpan={6} sx={{ p: 0, border: 0 }}><Collapse in={open}><Box component="pre" sx={{ m: 1.5, overflow: "auto", p: 2, bgcolor: "grey.900", color: "grey.100", borderRadius: 1, fontSize: 12 }}>{message.payload}</Box></Collapse></TableCell></TableRow>
        </React.Fragment>; })}
      </TableBody></Table></Box>}
  </Paper>;
}

function ReceiptDetailDialog({ receipt, onClose }) {
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
        {receipt?.receiptJson ?? JSON.stringify(receipt?.receipt ?? receipt, null, 2)}
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
      <TextField select label="Membership level" value={membershipLevel} onChange={(event) => setMembershipLevel(event.target.value)} size="small" fullWidth>{catalog.membershipLevels.map((level) => <MenuItem key={level.id} value={level.id}>{level.name}</MenuItem>)}</TextField>
      <TextField label="Customer number" value={customerNumber} onChange={(event) => setCustomerNumber(event.target.value)} size="small" fullWidth />
    </Stack>
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2 }}>
      <TextField label="Vehicle registration" value={vehicleRegistration} onChange={(event) => setVehicleRegistration(event.target.value)} size="small" fullWidth />
      <TextField label="Vehicle VIN" value={vehicleVin} onChange={(event) => setVehicleVin(event.target.value)} size="small" fullWidth />
      <TextField select label="Refund flag" value={refundFlag} onChange={(event) => setRefundFlag(event.target.value)} size="small">
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

function PosProcessing({ catalog, receipts, onGenerate }) {
  const stages = ["POS Receipt", "POS_RECEIPT_EVENT", "POS_RECEIPT_LINE_ITEM", "Reporting / Finance export"];
  const [selectedReceipt, setSelectedReceipt] = useState(null);
  const [detailError, setDetailError] = useState(null);

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
    <Stack direction={{ xs: "column", md: "row" }} alignItems="center" spacing={1}>
      {stages.map((stage, index) => <React.Fragment key={stage}>
        <Paper variant="outlined" sx={{ p: 2, flex: 1, width: "100%", bgcolor: index === 0 ? "primary.50" : "background.default", borderColor: index === 0 ? "primary.main" : "divider" }}>
          <Typography variant="subtitle2" fontWeight={800}>{stage}</Typography>
          <Typography variant="caption" color="text.secondary">{index === 0 ? "Captured locally" : index === stages.length - 1 ? "Ready for export" : "Event generated"}</Typography>
        </Paper>
        {index < stages.length - 1 && <Typography aria-hidden="true" color="primary" fontSize={24} sx={{ transform: { xs: "rotate(90deg)", md: "none" } }}>→</Typography>}
      </React.Fragment>)}
    </Stack>
    <Divider sx={{ my: 3 }} />
    {catalog ? <ReceiptGenerationForm catalog={catalog} onGenerate={onGenerate} /> : <Typography color="text.secondary">Loading catalog…</Typography>}
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

function App() {
  const [config, setConfig] = useState({ subscriberLabels: {}, producerLabels: {} });
  const [statuses, setStatuses] = useState([]);
  const [messages, setMessages] = useState([]);
  const [posReceipts, setPosReceipts] = useState([]);
  const [posCatalog, setPosCatalog] = useState(null);
  const [activeTab, setActiveTab] = useState("contact");
  const [emulator, setEmulator] = useState({ running: false });
  const [selectedService, setSelectedService] = useState(null);
  const [activeService, setActiveService] = useState(null);
  const [newMessageOpen, setNewMessageOpen] = useState(false);
  const [editorOpen, setEditorOpen] = useState(false);
  const [selectedDraft, setSelectedDraft] = useState(drafts[0]);
  const [form, setForm] = useState(drafts[0].data);
  const [messageName, setMessageName] = useState(drafts[0].name);
  const [toast, setToast] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  async function refresh() {
    try {
      const [nextConfig, nextEmulator, nextStatuses, nextMessages, nextPosReceipts] = await Promise.all([getJson("/api/config"), getJson("/api/emulator-status"), getJson("/api/status"), getJson("/api/messages"), getJson("/api/pos/receipts")]);
      setConfig({ subscriberLabels: nextConfig.subscriberLabels || {}, producerLabels: nextConfig.producerLabels || {}, messageTypes: nextConfig.messageTypes || {} });
      setEmulator(nextEmulator); setStatuses(nextStatuses); setMessages(nextMessages); setPosReceipts(nextPosReceipts); setError(null);
    } catch (refreshError) { setError(refreshError.message); } finally { setLoading(false); }
  }

  async function generatePosReceipt(request) {
    await getJson("/api/pos/receipts", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(request) });
    await refresh();
  }

  useEffect(() => { refresh(); const timer = setInterval(refresh, 3000); return () => clearInterval(timer); }, []);
  useEffect(() => { getJson("/api/pos/catalog").then(setPosCatalog).catch((catalogError) => setError(catalogError.message)); }, []);

  const services = useMemo(() => {
    const reported = statuses.filter((service) => !["Dashboard", "producer"].includes(service.serviceName));
    const names = new Set(reported.map((service) => service.serviceName));
    const observed = [...new Set(messages.map((message) => message.serviceName))].filter((name) => !names.has(name) && !["Dashboard", "producer"].includes(name)).map((serviceName) => ({ serviceName, state: "Live", messagesHandled: messages.filter((message) => message.serviceName === serviceName).length, selectable: true }));
    const producer = statuses.find((service) => service.serviceName === "producer") || { serviceName: "producer", state: "Ready", messagesHandled: messages.filter((message) => message.serviceName === "producer").length, selectable: true, infrastructure: true };
    return [{ serviceName: "Service Bus emulator", state: emulator.running ? "Running" : "Offline", messagesHandled: null, selectable: false, infrastructure: true, icon: "◈" }, { ...producer, icon: "◉" }, ...reported, ...observed];
  }, [statuses, messages, emulator]);

  function selectDraft(draft) { setSelectedDraft(draft); setForm({ ...draft.data }); setMessageName(draft.name); setEditorOpen(true); }
  function updateForm(event) { const { name, value, type, checked } = event.target; setForm((current) => ({ ...current, [name]: type === "checkbox" ? checked : value })); }
  function hasDuplicateName(name, currentDraft) {
    return drafts.some((draft) => draft !== currentDraft && normalizedName(draft.name) === normalizedName(name));
  }
  function saveDraft() {
    const trimmedName = messageName.trim();
    if (!trimmedName || hasDuplicateName(trimmedName, selectedDraft)) return;
    selectedDraft.name = trimmedName;
    selectedDraft.data = { ...form };
    setMessageName(trimmedName);
    setToast("Message saved locally.");
  }
  async function publish(event) { event.preventDefault(); try { await getJson("/api/publish", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ ...form, type: selectedDraft.type }) }); setToast("Message published."); refresh(); } catch (publishError) { setToast(publishError.message); } }
  function createDraft({ name, type, data }) {
    const fields = fieldsFor(type, config);
    const draftData = data || emptyData(fields);
    if (fields.some((field) => field.Name === "contactId") && !draftData.contactId) draftData.contactId = createUuid();
    const draft = { id: `new-${Date.now()}`, name, type, folder: "New drafts", data: draftData };
    drafts.push(draft);
    selectDraft(draft);
    setNewMessageOpen(false);
  }
  const editorFields = fieldsFor(selectedDraft.type, config);
  const duplicateEditorName = drafts.some((draft) => draft !== selectedDraft && normalizedName(draft.name) === normalizedName(messageName));

  return <Container maxWidth="xl" sx={{ py: 3 }}>
    <AppBar position="static" color="transparent" elevation={0} sx={{ mb: 3 }}><Toolbar disableGutters><Box className="brand-mark">P</Box><Box sx={{ ml: 1.5 }}><Typography variant="overline" color="text.secondary">Messaging Workspace</Typography><Typography variant="h5" color="text.primary">{activeTab === "contact" ? "Contact Events" : "Local Processing"}</Typography></Box><Stack direction="row" spacing={1} sx={{ ml: { xs: 1.5, sm: 4 }, flexWrap: "wrap" }} role="tablist" aria-label="Messaging workspace pages"><Button size="small" variant={activeTab === "contact" ? "contained" : "text"} onClick={() => setActiveTab("contact")} role="tab" aria-selected={activeTab === "contact"}>Contact Events</Button><Button size="small" variant={activeTab === "pos" ? "contained" : "text"} onClick={() => setActiveTab("pos")} role="tab" aria-selected={activeTab === "pos"} startIcon={<ReceiptLongIcon />}>Local Processing</Button></Stack><Box sx={{ ml: "auto", display: "flex", alignItems: "center" }}><Typography variant="body2" color="text.secondary" sx={{ mr: 1 }}>{loading ? "Checking services…" : error ? "Status unavailable" : "Live"}</Typography><IconButton onClick={refresh} aria-label="Refresh status"><RefreshIcon /></IconButton></Box></Toolbar></AppBar>
    {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>Could not refresh status: {error}</Alert>}
    {activeTab === "pos" ? <PosProcessing catalog={posCatalog} receipts={posReceipts} onGenerate={generatePosReceipt} /> : <>
    <Stack direction="row" flexWrap="wrap" spacing={2} useFlexGap sx={{ mb: 3 }}>{services.map((service) => <Box key={service.serviceName} sx={{ flex: { xs: "1 1 100%", sm: "1 1 220px" }, minWidth: 0 }}><ServiceCard service={service} config={config} selected={selectedService === service.serviceName} onClick={() => { setSelectedService(service.serviceName); setActiveService(service.serviceName); }} /></Box>)}</Stack>
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", lg: "260px minmax(320px, 1fr)" }, gap: 2 }}>
      <Paper variant="outlined"><Box sx={{ p: 2, display: "flex", justifyContent: "space-between" }}><Box><Typography variant="h6">Draft explorer</Typography><Typography variant="body2" color="text.secondary">Published by Producer</Typography></Box><IconButton aria-label="Create draft" onClick={() => setNewMessageOpen(true)}><AddIcon /></IconButton></Box><Divider /><List dense>{["Contact events", "Regression checks", "New drafts"].map((folder) => <React.Fragment key={folder}><ListItemText primary={`› ${folder}`} sx={{ px: 2, py: 1, fontWeight: 700 }} />{drafts.filter((draft) => draft.folder === folder).map((draft) => <ListItemButton key={draft.id} selected={draft.id === selectedDraft.id} onClick={() => selectDraft(draft)} sx={{ pl: 3 }}><ListItemText primary={`▱ ${draft.name}`} /></ListItemButton>)}</React.Fragment>)}</List></Paper>
      <ActivityStream messages={messages} statuses={statuses} config={config} />
    </Box>
    <MessageDialog service={activeService} messages={messages} config={config} onClose={() => setActiveService(null)} />
    <NewMessageDialog open={newMessageOpen} config={config} existingNames={drafts.map((draft) => draft.name)} onClose={() => setNewMessageOpen(false)} onCreate={createDraft} />
    <MessageEditorDialog open={editorOpen} draft={selectedDraft} form={form} fields={editorFields} config={config} messageName={messageName} duplicateName={duplicateEditorName} onNameChange={setMessageName} onChange={updateForm} onSave={saveDraft} onPublish={(event) => { if (duplicateEditorName || !messageName.trim()) { event.preventDefault(); return; } saveDraft(); publish(event); }} onClose={() => setEditorOpen(false)} />
    <Snackbar open={Boolean(toast)} autoHideDuration={4500} onClose={() => setToast(null)} message={toast} />
    </>}
    {loading && <CircularProgress size={24} sx={{ position: "fixed", bottom: 24, left: 24 }} />}
  </Container>;
}

createRoot(document.getElementById("root")).render(<ThemeProvider theme={theme}><App /></ThemeProvider>);
