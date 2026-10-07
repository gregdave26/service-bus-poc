import React, { useEffect, useMemo, useRef, useState } from "react";
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
  LinearProgress,
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
  Tooltip,
  Toolbar,
  Typography,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import CloseIcon from "@mui/icons-material/Close";
import ExitToAppIcon from "@mui/icons-material/ExitToApp";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import AddIcon from "@mui/icons-material/Add";
import SaveIcon from "@mui/icons-material/Save";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import ContactPageIcon from "@mui/icons-material/ContactPage";
import CalendarMonthIcon from "@mui/icons-material/CalendarMonth";
import FileOpenIcon from "@mui/icons-material/FileOpen";
import AutoFixHighIcon from "@mui/icons-material/AutoFixHigh";
import VisibilityIcon from "@mui/icons-material/Visibility";
import { generateRosterFile } from "./rosteringGenerator.js";
import { PosProcessing } from "./localPosProcessing.jsx";
import { processFlowColors, processStageSx } from "./processFlowStyles.js";
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
// Field definitions (labels, columns, required/optional, aliases) are centralized server-side
// and fetched from GET /api/config as `rosteringInputDefinitions`; see docs/rostering-workflow.md.
const rosteringInputOrder = ["agentScheduleSummary", "agentScheduleDetail", "ctActiveForecast", "agentInfo"];
const rosteringFilenamePatterns = {
  agentScheduleSummary: { primary: "agentScheduleSummary_*.txt", second: "agentScheduleSummary2_*.txt", example: "agentScheduleSummary_100825_1750.txt" },
  agentScheduleDetail: { primary: "agentScheduleDetail_*.txt", second: "agentScheduleDetail2_*.txt", example: "agentScheduleDetail_100825_1750.txt" },
  ctActiveForecast: { primary: "ctActiveForecast_*.txt", second: "ctActiveForecast2_*.txt", example: "ctActiveForecast_100825_1750.txt" },
  agentInfo: { primary: "agentInfo_*.txt", second: "agentInfo2_*.txt", example: "agentInfo_100825_1750.txt" },
};

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
      <Stack direction="row" justifyContent="flex-end" spacing={1} sx={{ p: 2 }}>
        <Tooltip title="Cancel">
          <IconButton aria-label="Cancel" onClick={onClose}><ExitToAppIcon sx={{ transform: "rotate(180deg)" }} /></IconButton>
        </Tooltip>
        <Tooltip title="Create message">
          <span>
            <IconButton aria-label="Create message" type="submit" color="primary" disabled={!name.trim() || duplicate}>
              <SaveIcon />
            </IconButton>
          </span>
        </Tooltip>
      </Stack>
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

function ServiceCard({ service, config, selected, onClick, receivedFlash }) {
  const connected = String(service.state).toLowerCase() !== "offline";
  const name = displayName(service.serviceName, config);
  const labels = service.serviceName === "producer" ? config.producerLabels : config.subscriberLabels?.[service.serviceName];
  const stateLabel = connected ? (labels?.ConnectedLabel || "Connected") : (labels?.OfflineLabel || "Offline");
  const countLabel = labels?.MessagesHandledLabel || "handled";
  return <Card variant="outlined" aria-label={receivedFlash ? `${name} received a message` : name} sx={{ borderColor: selected ? "primary.main" : "divider", bgcolor: selected ? "primary.light" : "background.paper", animation: receivedFlash ? "serviceReceiveFlash 900ms ease-out" : "none", "@media (prefers-reduced-motion: reduce)": { animation: "none" }, "@keyframes serviceReceiveFlash": { "0%": { backgroundColor: processFlowColors.completed.background, boxShadow: `0 0 0 6px ${processFlowColors.completed.background}` }, "100%": { backgroundColor: selected ? undefined : "background.paper", boxShadow: "0 0 0 0 transparent" } } }}>
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

function RosteringWorkflow({ rosteringMappings = {}, rosteringInputDefinitions = {} }) {
  const [files, setFiles] = useState([null, null, null, null]);
  const [result, setResult] = useState(null);
  const [xml, setXml] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [infoInput, setInfoInput] = useState(null);
  const [preview, setPreview] = useState(null);
  function selectFile(index, file) {
    setFiles((current) => current.map((value, position) => position === index ? file : value));
    setResult(null); setXml(""); setError(null);
  }
  async function generateFile(index, inputName) {
    const generated = generateRosterFile(inputName, rosteringInputDefinitions[inputName]);
    const file = new File([generated.content], generated.name, { type: generated.type });
    setBusy(true); setError(null);
    try {
      await getJson("/api/rostering/temp-files", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: generated.name, content: generated.content }) });
      selectFile(index, file);
      setPreview(null);
    } catch (generationError) { setError(generationError.message); }
    finally { setBusy(false); }
  }
  async function viewFile(inputName, file) {
    if (!file) return;
    setPreview({ inputName, file, content: await file.text(), generated: false });
  }
  function saveFile() {
    if (!preview) return;
    const link = document.createElement("a");
    link.href = URL.createObjectURL(new Blob([preview.content], { type: "text/plain" }));
    link.download = preview.file.name;
    link.click();
    URL.revokeObjectURL(link.href);
  }
  async function upload() {
    if (files.some((file) => !file)) return setError("Choose all four source files before validating.");
    setBusy(true); setError(null); setResult(null); setXml("");
    try {
      const response = await getJson("/api/rostering/upload", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ files: await Promise.all(files.map(async (file) => ({ name: file.name, content: await file.text() }))) }) });
      setResult(response);
    } catch (uploadError) { setError(uploadError.message); }
    finally { setBusy(false); }
  }
  async function extract() {
    setBusy(true); setError(null);
    try { const response = await getJson(`/api/rostering/${result.batchId}/extract`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ sequence: 1 }) }); setXml(response.xml); setResult((current) => ({ ...current, ...response })); }
    catch (extractError) { setError(extractError.message); }
    finally { setBusy(false); }
  }
  function download() {
    const link = document.createElement("a");
    link.href = URL.createObjectURL(new Blob([xml], { type: "application/xml" }));
    link.download = result.filename ?? `lineup-${result.batchId}.xml`; link.click(); URL.revokeObjectURL(link.href);
  }
  const stages = [
    { label: "Create input files", detail: files.every(Boolean) ? "Complete — 4 TXT files ready" : files.some(Boolean) ? `${files.filter(Boolean).length} of 4 TXT files ready` : "Select or generate 4 TXT files", active: files.some(Boolean), completed: files.every(Boolean) },
    { label: "Insert into ODS", detail: result?.valid ? `Complete — ${result.rowCount} rows loaded` : "Insert into ODS", active: Boolean(result?.valid), completed: Boolean(result?.valid) },
    { label: "Convert to lineup XML", detail: xml ? "Complete — ready for download" : "Convert to Lineup XML", active: Boolean(xml), completed: Boolean(xml) },
  ];
  const selectedFileCount = files.filter(Boolean).length;
  const allFilesReady = files.every(Boolean);
  const selectedFileNames = files.flatMap((file, index) => file ? [rosteringInputDefinitions[rosteringInputOrder[index]]?.label ?? file.name] : []);
  const remainingFileCount = rosteringInputOrder.length - selectedFileCount;
  return <Stack spacing={2}>
    <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}>
      <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" gap={1} mb={3}>
        <Box><Typography variant="h6">Rostering workflow simulation</Typography><Typography variant="body2" color="text.secondary">Create the source files, load the ODS tables, then generate the lineup XML.</Typography></Box>
        <Box sx={{ minWidth: { sm: 260 } }}>
          <Stack direction="row" justifyContent="space-between" alignItems="center" gap={1}>
            <Chip label={xml ? "XML ready" : result?.valid ? "ODS loaded" : files.some(Boolean) ? "Files selected" : "Awaiting files"} color={xml || result?.valid ? "success" : "primary"} size="small" />
            <Typography variant="caption" color="text.secondary">{selectedFileCount} of {rosteringInputOrder.length} files</Typography>
          </Stack>
          <LinearProgress variant="determinate" value={(selectedFileCount / rosteringInputOrder.length) * 100} sx={{ mt: 1, height: 8, borderRadius: 3, backgroundColor: processFlowColors.idle.background, "& .MuiLinearProgress-bar": { backgroundColor: selectedFileCount === rosteringInputOrder.length ? processFlowColors.completed.border : processFlowColors.active.border } }} aria-label={`${selectedFileCount} of ${rosteringInputOrder.length} roster files selected`} />
        </Box>
      </Stack>
      <Alert severity={selectedFileCount === rosteringInputOrder.length ? "success" : "info"} sx={{ mb: 2 }}>
        <b>{selectedFileCount} of {rosteringInputOrder.length} input files selected.</b>{" "}
        {selectedFileCount === 0 ? "Choose the four required TXT files below." : selectedFileCount === rosteringInputOrder.length ? "All required files are ready to validate." : `${remainingFileCount} file${remainingFileCount === 1 ? "" : "s"} remaining: ${rosteringInputOrder.filter((inputName, index) => !files[index]).map((inputName) => rosteringInputDefinitions[inputName]?.label ?? inputName).join(", ")}.`}
        {selectedFileNames.length > 0 && <Typography display="block" variant="caption" color="text.secondary">Selected: {selectedFileNames.join(", ")}</Typography>}
      </Alert>
      <Stack direction={{ xs: "column", md: "row" }} alignItems="center" spacing={1} sx={{ mb: 3 }}>
        {stages.map((stage, index) => <React.Fragment key={stage.label}>
          <Paper variant="outlined" sx={{ p: 2, flex: 1, width: "100%", ...processStageSx(stage.completed ? "completed" : stage.active ? "active" : "idle") }}>
            <Typography variant="subtitle2" fontWeight={800}>{stage.label}</Typography>
            <Typography variant="caption" color="text.secondary">{stage.detail}</Typography>
          </Paper>
          {index < stages.length - 1 && <Typography aria-hidden="true" fontSize={24} sx={{ color: stage.completed ? processFlowColors.completed.border : processFlowColors.idle.border, transform: { xs: "rotate(90deg)", md: "none" } }}>→</Typography>}
        </React.Fragment>)}
      </Stack>
      <Alert severity="info" sx={{ mb: 2 }}>Choose one .txt file for each named input. Each file contains pipe-delimited CSV data using <b>|</b> between columns. Filenames may use separators (for example, <b>agent_schedule_detail.txt</b>). Required columns (identity, date, group, and numeric fields) must be present; optional descriptive columns may be omitted entirely. Validation details appear below and source rows are preserved.</Alert>
      <Stack spacing={1}>{rosteringInputOrder.map((inputName, index) => {
        const file = files[index];
        const definition = rosteringInputDefinitions[inputName];
        const mapping = rosteringMappings[inputName];
        const requiredColumns = definition?.fields.filter((field) => field.required).map((field) => field.name).join(", ");
        const optionalColumns = definition?.fields.filter((field) => !field.required).map((field) => field.name).join(", ");
        return <Paper key={inputName} variant="outlined" sx={{ p: 1.25, ...processStageSx(file ? "fileReady" : "idle") }}>
          <Stack direction={{ xs: "column", sm: "row" }} alignItems={{ sm: "center" }} justifyContent="space-between" gap={1}>
            <Box sx={{ minWidth: 0 }}>
              <Typography variant="body2" fontWeight={800}>{definition?.label ?? inputName}</Typography>
              <Typography variant="caption" color={file ? "text.primary" : "text.secondary"} noWrap title={file?.name}>{file?.name ?? "No file selected"}</Typography>
            </Box>
            <Stack direction="row" spacing={0.5} alignItems="center" flexShrink={0}>
              <Tooltip title="Select file">
                <IconButton component="label" size="small" color="primary" aria-label={`Select ${definition?.label ?? inputName} file`}>
                  <FileOpenIcon fontSize="small" />
                  <input hidden type="file" accept=".txt,text/plain" onChange={(event) => { const file = event.target.files?.[0] ?? null; selectFile(index, file); if (file) viewFile(inputName, file); }} />
                </IconButton>
              </Tooltip>
              <Tooltip title="Generate sample file">
                <IconButton size="small" color="primary" onClick={() => generateFile(index, inputName)} disabled={busy} aria-label={`Generate ${definition?.label ?? inputName} sample file`}>
                  <AutoFixHighIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title={file ? "View file" : "Select or generate a file to view it"}>
                <span>
                  <IconButton size="small" color="primary" disabled={!file} onClick={() => viewFile(inputName, file)} aria-label={`View ${definition?.label ?? inputName} file`}>
                    <VisibilityIcon fontSize="small" />
                  </IconButton>
                </span>
              </Tooltip>
              <IconButton size="small" aria-label={`Show ${definition?.label ?? inputName} column requirements`} onClick={() => setInfoInput(inputName)} sx={{ border: 1, borderColor: "divider", borderRadius: 1 }}>
                <Typography component="span" fontWeight={800} fontSize="0.85rem">?</Typography>
              </IconButton>
            </Stack>
          </Stack>
        </Paper>;
      })}</Stack>
      <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ mt: 2 }}>
        <Button variant="contained" onClick={upload} disabled={busy || !allFilesReady}>{busy ? "Validating…" : "Insert into ODS"}</Button>
      </Stack>
      {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
    </Paper>
    <Dialog open={Boolean(preview)} onClose={() => setPreview(null)} fullWidth maxWidth="md" aria-labelledby="rostering-file-preview-title">
      <DialogTitle id="rostering-file-preview-title">{preview?.file.name}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Typography variant="body2" color="text.secondary">{preview?.generated ? `Generated sample file · auto-saved to ${preview.tempDirectory}` : "Selected source file"} · pipe-delimited TXT</Typography>
          <TextField aria-label="Roster file contents" value={preview?.content ?? ""} multiline minRows={14} fullWidth InputProps={{ readOnly: true }} sx={{ "& textarea": { fontFamily: "monospace", fontSize: 12 } }} />
          <Stack direction="row" justifyContent="flex-end" spacing={1}>
            <Button onClick={() => setPreview(null)}>Close</Button>
            <Button variant="contained" onClick={saveFile}>Save TXT</Button>
          </Stack>
        </Stack>
      </DialogContent>
    </Dialog>
    <Dialog open={Boolean(infoInput)} onClose={() => setInfoInput(null)} fullWidth maxWidth="sm" aria-labelledby="rostering-input-info-title">
      <DialogTitle id="rostering-input-info-title">{infoInput && (rosteringInputDefinitions[infoInput]?.label ?? infoInput)}</DialogTitle>
      <DialogContent dividers>
        {infoInput && (() => {
          const definition = rosteringInputDefinitions[infoInput];
          const mapping = rosteringMappings[infoInput];
          const filenamePatterns = rosteringFilenamePatterns[infoInput];
          const requiredColumns = definition?.fields.filter((field) => field.required).map((field) => field.name).join(", ");
          const optionalColumns = definition?.fields.filter((field) => !field.required).map((field) => field.name).join(", ");
          return <Stack spacing={1}>
            {filenamePatterns && <Box>
              <Typography variant="body2" fontWeight={800}>Expected filenames</Typography>
              <Typography variant="body2"><b>Primary run:</b> <code>{filenamePatterns.primary}</code></Typography>
              <Typography variant="body2"><b>Afternoon / second run:</b> <code>{filenamePatterns.second}</code></Typography>
              <Typography variant="body2"><b>Example:</b> <code>{filenamePatterns.example}</code></Typography>
            </Box>}
            <Typography variant="body2"><b>File format:</b> pipe-delimited CSV text (`|`) in a `.txt` file.</Typography>
            <Typography variant="body2"><b>Required columns:</b> {requiredColumns || "None"}</Typography>
            <Typography variant="body2"><b>Optional columns:</b> {optionalColumns || "None"}</Typography>
            {mapping && <Typography variant="body2"><b>ODS target:</b> {mapping.destinationTable}</Typography>}
          </Stack>;
        })()}
      </DialogContent>
    </Dialog>
    {result && <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}>
      <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" gap={1}><Box><Typography variant="h6">Batch validation</Typography><Typography variant="body2" color="text.secondary">{result.rowCount} loaded row{result.rowCount === 1 ? "" : "s"} · {result.batchId}</Typography></Box><Chip label={result.valid ? "Valid and loaded" : "Needs correction"} color={result.valid ? "success" : "error"} /></Stack>
      <Table size="small" aria-label="Rostering validation results"><TableHead><TableRow><TableCell>Input</TableCell><TableCell>File</TableCell><TableCell>Status</TableCell><TableCell>Rows</TableCell><TableCell>Direct load target</TableCell><TableCell>Headers / issue</TableCell></TableRow></TableHead><TableBody>{result.validation.map((item, index) => <TableRow key={`${item.file}-${index}`}><TableCell>{item.label ?? "Batch"}</TableCell><TableCell>{item.file}</TableCell><TableCell>{item.valid ? "Valid" : "Invalid"}</TableCell><TableCell>{item.rowCount ?? "—"}</TableCell><TableCell>{item.mapping?.destinationTable ?? "—"}</TableCell><TableCell>{item.valid ? item.headers.join(", ") : item.error}</TableCell></TableRow>)}</TableBody></Table>
    </Paper>}
    <Button sx={{ alignSelf: "flex-start" }} variant="contained" onClick={extract} disabled={busy || !result?.valid}>Convert to Lineup XML</Button>
    {xml && <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}><Stack direction="row" justifyContent="space-between" alignItems="center"><Box><Typography variant="h6">RAC roadside lineup XML</Typography><Typography variant="caption" color="text.secondary">{result.filename} · companion marker: {result.doneFilename} (zero bytes)</Typography></Box><Button onClick={download}>Download XML</Button></Stack><TextField aria-label="Lineup XML preview" value={xml} multiline minRows={12} fullWidth InputProps={{ readOnly: true }} sx={{ mt: 2, "& textarea": { fontFamily: "monospace", fontSize: 12 } }} /></Paper>}
  </Stack>;
}

function App() {
  const [config, setConfig] = useState({ subscriberLabels: {}, producerLabels: {} });
  const [statuses, setStatuses] = useState([]);
  const [messages, setMessages] = useState([]);
  const [posCatalog, setPosCatalog] = useState(null);
  const [posReceipts, setPosReceipts] = useState([]);
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
  const [receivedFlashServices, setReceivedFlashServices] = useState(new Set());
  const previousMessageIds = useRef(null);

  async function refresh() {
    try {
      const [nextConfig, nextEmulator, nextStatuses, nextMessages, nextPosReceipts] = await Promise.all([getJson("/api/config"), getJson("/api/emulator-status"), getJson("/api/status"), getJson("/api/messages"), getJson("/api/pos/receipts")]);
      setConfig({ subscriberLabels: nextConfig.subscriberLabels || {}, producerLabels: nextConfig.producerLabels || {}, messageTypes: nextConfig.messageTypes || {}, rosteringMappings: nextConfig.rosteringMappings || {}, rosteringInputDefinitions: nextConfig.rosteringInputDefinitions || {} });
      setEmulator(nextEmulator); setStatuses(nextStatuses); setMessages(nextMessages); setPosReceipts(nextPosReceipts); setError(null);
    } catch (refreshError) { setError(refreshError.message); } finally { setLoading(false); }
  }

  useEffect(() => { refresh(); const timer = setInterval(refresh, 3000); return () => clearInterval(timer); }, []);
  useEffect(() => { getJson("/api/pos/catalog").then(setPosCatalog).catch((catalogError) => setError(catalogError.message)); }, []);

  async function generatePosReceipt(request) {
    const receipt = await getJson("/api/pos/receipts", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
    const latestReceipts = await getJson("/api/pos/receipts");
    setPosReceipts(latestReceipts);
    return receipt;
  }

  useEffect(() => {
    if (loading) return;
    const currentIds = new Set(messages.map((message) => message.messageId ?? message.id ?? `${message.serviceName}-${message.timestamp}`));
    if (previousMessageIds.current === null) {
      previousMessageIds.current = currentIds;
      return;
    }
    const receivedServices = new Set(messages
      .filter((message) => message.direction === "received")
      .filter((message) => !previousMessageIds.current.has(message.messageId ?? message.id ?? `${message.serviceName}-${message.timestamp}`))
      .map((message) => message.serviceName));
    previousMessageIds.current = currentIds;
    if (!receivedServices.size) return;
    setReceivedFlashServices(receivedServices);
    const timer = setTimeout(() => setReceivedFlashServices(new Set()), 1000);
    return () => clearTimeout(timer);
  }, [messages, loading]);

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
    <AppBar position="static" color="transparent" elevation={0} sx={{ mb: 3 }}><Toolbar disableGutters><Box className="brand-mark"><img src="/rac-logo.png" alt="RAC logo" /></Box><Box sx={{ ml: 1.5 }}><Typography variant="overline" color="text.secondary">Messaging Workspace</Typography><Typography variant="h5" color="text.primary">{activeTab === "contact" ? "Contact Events" : activeTab === "pos" ? "Local Processing" : "Rostering"}</Typography></Box><Stack direction="row" spacing={1} sx={{ ml: { xs: 1.5, sm: 4 }, flexWrap: "wrap" }} role="tablist" aria-label="Messaging workspace pages"><Button size="small" variant={activeTab === "contact" ? "contained" : "text"} onClick={() => setActiveTab("contact")} role="tab" aria-selected={activeTab === "contact"} startIcon={<ContactPageIcon />}>Contact Events</Button><Button size="small" variant={activeTab === "pos" ? "contained" : "text"} onClick={() => setActiveTab("pos")} role="tab" aria-selected={activeTab === "pos"} startIcon={<ReceiptLongIcon />}>Local Processing</Button><Button size="small" variant={activeTab === "rostering" ? "contained" : "text"} onClick={() => setActiveTab("rostering")} role="tab" aria-selected={activeTab === "rostering"} startIcon={<CalendarMonthIcon />}>Rostering</Button></Stack><Box sx={{ ml: "auto", display: "flex", alignItems: "center" }}><Typography variant="body2" color="text.secondary" sx={{ mr: 1 }}>{loading ? "Checking services…" : error ? "Status unavailable" : "Live"}</Typography><IconButton onClick={refresh} aria-label="Refresh status"><RefreshIcon /></IconButton></Box></Toolbar></AppBar>
    {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>Could not refresh status: {error}</Alert>}
    {activeTab === "rostering" ? <RosteringWorkflow rosteringMappings={config.rosteringMappings} rosteringInputDefinitions={config.rosteringInputDefinitions} /> : activeTab === "pos" ? (posCatalog ? <PosProcessing catalog={posCatalog} receipts={posReceipts} onGenerate={generatePosReceipt} /> : <CircularProgress />) : <>
    <Stack direction="row" flexWrap="wrap" spacing={2} useFlexGap sx={{ mb: 3 }}>{services.map((service) => <Box key={service.serviceName} sx={{ flex: { xs: "1 1 100%", sm: "1 1 220px" }, minWidth: 0 }}><ServiceCard service={service} config={config} selected={selectedService === service.serviceName} receivedFlash={receivedFlashServices.has(service.serviceName)} onClick={() => { setSelectedService(service.serviceName); setActiveService(service.serviceName); }} /></Box>)}</Stack>
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
