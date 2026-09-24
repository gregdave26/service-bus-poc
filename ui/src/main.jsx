import React, { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  Alert, AppBar, Box, Button, Card, CardActionArea, CardContent, Checkbox,
  Chip, Container, CssBaseline, Dialog, DialogActions, DialogContent,
  DialogTitle, Divider, Fab, IconButton, Snackbar, Stack, TextField,
  Toolbar, Tooltip, Typography,
} from "@mui/material";
import { createTheme, ThemeProvider } from "@mui/material/styles";
import RefreshIcon from "@mui/icons-material/Refresh";
import AddIcon from "@mui/icons-material/Add";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import CodeIcon from "@mui/icons-material/Code";
import CloudQueueIcon from "@mui/icons-material/CloudQueue";
import SendIcon from "@mui/icons-material/Send";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import "./styles.css";

const emptyDraft = () => ({
  contactId: "", firstName: "", lastName: "", phone: "", email: "",
  hasInsurance: false, hasParksResorts: false, hasCarwashProduct: false,
});
const initialDrafts = [
  { id: "contact-updated", name: "Contact updated", folder: "Contact events", data: { contactId: "contact-1042", firstName: "Ada", lastName: "Lovelace", phone: "0400000000", email: "ada@example.com", hasInsurance: true, hasParksResorts: false, hasCarwashProduct: true } },
  { id: "new-member", name: "New member welcome", folder: "Contact events", data: { contactId: "contact-1088", firstName: "Grace", lastName: "Hopper", phone: "0400000011", email: "grace@example.com", hasInsurance: false, hasParksResorts: true, hasCarwashProduct: false } },
  { id: "regression", name: "Routing regression", folder: "Regression checks", data: { contactId: "contact-test", firstName: "Test", lastName: "Contact", phone: "0400000099", email: "test@example.com", hasInsurance: true, hasParksResorts: true, hasCarwashProduct: true } },
];
const theme = createTheme({
  palette: { primary: { main: "#3f51b5" }, background: { default: "#fbf9fc", paper: "#fff" } },
  shape: { borderRadius: 12 },
  typography: { fontFamily: "Inter, ui-sans-serif, system-ui, -apple-system, sans-serif" },
});

async function json(response, endpoint) {
  const type = response.headers.get("content-type") || "";
  if (!response.ok || !type.includes("application/json")) throw new Error(`${endpoint} returned HTTP ${response.status}.`);
  return response.json();
}
function payloadOf(message) { try { return JSON.parse(message.payload); } catch { return null; } }

function StatusTile({ service, label, selected, onSelect }) {
  const connected = String(service.state).toLowerCase() !== "offline";
  const content = (
    <CardContent sx={{ minHeight: 132, p: 2.5 }}>
      <Stack direction="row" justifyContent="space-between" color="text.secondary">
        <Typography variant="caption" fontWeight={700}>{service.infrastructure ? "Infrastructure" : "Service"}</Typography>
        {service.icon || <CloudQueueIcon color="primary" fontSize="small" />}
      </Stack>
      <Typography variant="h6" mt={2} fontSize="1rem"><Box component="span" className={`status-dot ${connected ? "" : "offline"}`} />{label}</Typography>
      <Typography variant="body2" color="text.secondary">{connected ? (service.ConnectedLabel || "Connected") : (service.OfflineLabel || "Offline")}{service.messagesHandled == null ? "" : ` · ${service.messagesHandled} ${service.MessagesHandledLabel || "handled"}`}</Typography>
    </CardContent>
  );
  return service.selectable === false ? <Card className={service.infrastructure ? "infrastructure" : ""}>{content}</Card> : (
    <Card className={`${selected ? "selected-tile " : ""}${service.infrastructure ? "infrastructure" : ""}`}>
      <CardActionArea onClick={onSelect} aria-label={`Filter messages for ${label}`} aria-pressed={selected}>{content}</CardActionArea>
    </Card>
  );
}

function DraftExplorer({ drafts, selected, onSelect, onNew }) {
  const folders = [...new Set(drafts.map((draft) => draft.folder))];
  return <Card component="aside" className="panel">
    <Box className="panel-heading"><Box><Typography variant="h6">Draft explorer</Typography><Typography variant="caption" color="text.secondary">Published by Producer</Typography></Box><Tooltip title="Create draft"><IconButton onClick={onNew} aria-label="Create draft"><AddIcon /></IconButton></Tooltip></Box>
    <Box component="nav" aria-label="Draft folders" p={1.5}>
      {folders.map((folder) => <Box component="details" key={folder} open className="folder">
        <Box component="summary" sx={{ cursor: "pointer", fontWeight: 700, p: 1 }}>{folder}</Box>
        <Stack component="ul" spacing={.5} sx={{ listStyle: "none", m: 0, p: "0 0 8px 1rem" }}>
          {drafts.filter((draft) => draft.folder === folder).map((draft) => <Box component="li" key={draft.id}>
            <Button fullWidth onClick={() => onSelect(draft)} className={selected.id === draft.id ? "draft-selected" : ""} sx={{ justifyContent: "flex-start", color: "text.secondary" }}>▱ {draft.name} ⋯</Button>
          </Box>)}
        </Stack>
      </Box>)}
    </Box>
  </Card>;
}

function Editor({ draft, onChange, onSave, onPublish }) {
  const set = (key) => (event) => onChange({ ...draft.data, [key]: event.target.type === "checkbox" ? event.target.checked : event.target.value });
  return <Card className="panel"><Box className="panel-heading"><Typography variant="h6">Message editor</Typography></Box>
    <Box component="form" onSubmit={onPublish} className="editor" aria-label="Message editor">
      <Typography variant="body2" color="text.secondary" mb={2}>Edit the selected draft, then save it or publish it through the Producer.</Typography>
      <Box className="form-grid">
        {["contactId", "firstName", "lastName", "phone", "email"].map((field) => <TextField key={field} label={field === "contactId" ? "Contact ID" : field.replace(/^./, (c) => c.toUpperCase())} type={field === "email" ? "email" : "text"} required value={draft.data[field]} onChange={set(field)} fullWidth className={field === "email" ? "full" : ""} />)}
        <Stack direction="row" flexWrap="wrap" gap={2} className="full">
          {[["hasInsurance", "Insurance"], ["hasParksResorts", "Parks & Resorts"], ["hasCarwashProduct", "Carwash"]].map(([key, label]) => <Box key={key} display="flex" alignItems="center"><Checkbox checked={!!draft.data[key]} onChange={set(key)} inputProps={{ "aria-label": label }} /><Typography variant="body2" fontWeight={600}>{label}</Typography></Box>)}
        </Stack>
      </Box>
      <Stack direction="row" justifyContent="flex-end" gap={1.5} mt={3}><Button variant="outlined" onClick={onSave}>Save draft</Button><Button variant="contained" type="submit" startIcon={<SendIcon />}>Publish</Button></Stack>
    </Box>
  </Card>;
}

function MessageDetail({ messages, loading, service, onInspect }) {
  const filtered = messages.filter((message) => !service || message.serviceName === service);
  return <Card className="panel"><Box className="panel-heading"><Box><Typography variant="h6">Received messages</Typography><Typography variant="caption" color="text.secondary">Expandable read-only event inspection</Typography></Box><Chip label="JSON" color="success" size="small" /></Box>
    <Box p={2.5}>{loading ? <Typography color="text.secondary" textAlign="center" p={4}>Loading messages…</Typography> : filtered.length === 0 ? <Typography color="text.secondary" textAlign="center" p={4}>No messages for this service yet.</Typography> : <Stack spacing={1.5}>{filtered.map((message) => <details className="message" key={message.messageId}>
      <Box component="summary" className="message-summary"><ExpandMoreIcon fontSize="small" /><Box flex={1} minWidth={0}><Typography fontWeight={700} noWrap>{message.eventId || message.messageId}</Typography><Typography variant="caption" color="text.secondary">{message.serviceName} · {new Date(message.timestamp).toLocaleString()}</Typography></Box><Chip label={message.direction} size="small" color={message.direction === "received" ? "success" : "primary"} /></Box>
      <Box px={2} pb={2}><pre>{JSON.stringify(payloadOf(message) || message.payload, null, 2)}</pre><Alert icon={<CheckCircleIcon />} severity="success" sx={{ mt: 2 }}><b>Live message</b><br /><Typography variant="caption">Original dashboard-reported payload. This panel is read-only.</Typography></Alert><Button size="small" onClick={() => onInspect(message)} sx={{ mt: 1 }}>View service details</Button></Box>
    </details>)}</Stack>}</Box>
  </Card>;
}

function App() {
  const [drafts, setDrafts] = useState(initialDrafts);
  const [selectedDraft, setSelectedDraft] = useState(initialDrafts[0]);
  const [messages, setMessages] = useState([]), [statuses, setStatuses] = useState([]), [config, setConfig] = useState({ subscriberLabels: {}, producerLabels: {} });
  const [emulator, setEmulator] = useState({ running: false }), [selectedService, setSelectedService] = useState(null), [loading, setLoading] = useState(true);
  const [updated, setUpdated] = useState("Checking services…"), [toast, setToast] = useState({ open: false, text: "", error: false }), [inspect, setInspect] = useState(null);
  const notify = (text, error = false) => setToast({ open: true, text, error });
  const refresh = async () => { try { const values = await Promise.all(["/api/config", "/api/emulator-status", "/api/status", "/api/messages"].map((url) => fetch(url).then((r) => json(r, url)))); setConfig(values[0]); setEmulator(values[1]); setStatuses(values[2]); setMessages(values[3]); setUpdated(`Updated ${new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`); } catch (error) { setEmulator({ running: false }); setUpdated("Status unavailable"); notify(`Could not refresh status: ${error.message}`, true); } finally { setLoading(false); } };
  useEffect(() => { refresh(); const timer = setInterval(refresh, 3000); return () => clearInterval(timer); }, []);
  const services = useMemo(() => { const reported = statuses.map((s) => ({ ...s, ...(s.serviceName === "producer" ? config.producerLabels : config.subscriberLabels[s.subscriptionName] || {}) })); const names = reported.map((s) => s.serviceName); const live = [...new Set(messages.map((m) => m.serviceName))].filter((n) => !names.includes(n) && !["Dashboard", "producer"].includes(n)).map((serviceName) => ({ serviceName, state: "Live", messagesHandled: messages.filter((m) => m.serviceName === serviceName).length, selectable: true })); const producer = reported.find((s) => s.serviceName === "producer") || { ...config.producerLabels, serviceName: "producer", state: "Ready", messagesHandled: messages.filter((m) => m.serviceName === "producer").length, selectable: false, icon: <SendIcon color="primary" /> }; return [{ serviceName: "emulator", displayName: "Service Bus emulator", state: emulator.running ? "Running" : "Offline", selectable: false, infrastructure: true, icon: <CloudQueueIcon color="primary" /> }, { ...producer, infrastructure: true, icon: <SendIcon color="primary" /> }, ...reported.filter((s) => !["Dashboard", "producer"].includes(s.serviceName)), ...live]; }, [statuses, messages, config, emulator]);
  useEffect(() => { if (!selectedService && services.find((s) => s.selectable !== false)) setSelectedService(services.find((s) => s.selectable !== false).serviceName); }, [services, selectedService]);
  const updateDraft = (data) => setSelectedDraft((draft) => { const next = { ...draft, data }; setDrafts((all) => all.map((item) => item.id === draft.id ? next : item)); return next; });
  const save = () => notify("Draft saved locally.");
  const publish = async (event) => { event.preventDefault(); try { const response = await fetch("/api/publish", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(selectedDraft.data) }); const result = await json(response, "/api/publish"); if (!result.success) throw new Error(result.error || "Publish failed."); notify(`Published ${result.eventId}`); refresh(); } catch (error) { notify(error.message, true); } };
  const newDraft = () => { const draft = { id: `new-${Date.now()}`, name: "Untitled draft", folder: "New drafts", data: emptyDraft() }; setDrafts((all) => [...all, draft]); setSelectedDraft(draft); };
  return <ThemeProvider theme={theme}><CssBaseline /><AppBar position="static" color="transparent" elevation={0} sx={{ borderBottom: 1, borderColor: "divider" }}><Toolbar><Box className="brand-mark">P</Box><Box ml={1.5}><Typography variant="overline" color="text.secondary" fontWeight={700}>Messaging workspace</Typography><Typography variant="h5" lineHeight={1}>Contact events</Typography></Box><Box flex={1} /><Typography variant="body2" color="text.secondary" mr={2} className="updated">{updated}</Typography><Tooltip title="Refresh status"><IconButton onClick={refresh} aria-label="Refresh status"><RefreshIcon /></IconButton></Tooltip></Toolbar></AppBar>
    <Container maxWidth={false} className="shell"><Typography variant="overline" color="text.secondary" fontWeight={700}>Service health</Typography><Box className="tiles">{services.map((service) => <StatusTile key={service.serviceName} service={service} label={service.displayName || service.serviceName} selected={selectedService === service.serviceName} onSelect={() => setSelectedService(service.serviceName)} />)}</Box>
      <Box className="workspace"><DraftExplorer drafts={drafts} selected={selectedDraft} onSelect={setSelectedDraft} onNew={newDraft} /><Editor draft={selectedDraft} onChange={updateDraft} onSave={save} onPublish={publish} /><MessageDetail messages={messages} loading={loading} service={selectedService} onInspect={setInspect} /></Box>
    </Container><Dialog open={!!inspect} onClose={() => setInspect(null)} fullWidth maxWidth="sm"><DialogTitle>Service details</DialogTitle><DialogContent dividers>{inspect && <Stack spacing={1}><Typography><b>Service:</b> {inspect.serviceName}</Typography><Typography><b>Direction:</b> {inspect.direction}</Typography><Typography><b>Subscription:</b> {inspect.subscriptionName || "—"}</Typography><Typography><b>Event ID:</b> {inspect.eventId}</Typography></Stack>}</DialogContent><DialogActions><Button onClick={() => setInspect(null)}>Close</Button></DialogActions></Dialog>
    <Snackbar open={toast.open} autoHideDuration={4500} onClose={() => setToast((value) => ({ ...value, open: false }))}><Alert severity={toast.error ? "error" : "success"} onClose={() => setToast((value) => ({ ...value, open: false }))}>{toast.text}</Alert></Snackbar><Fab color="primary" size="small" onClick={newDraft} aria-label="Create draft" className="mobile-add"><AddIcon /></Fab>
  </ThemeProvider>;
}

createRoot(document.getElementById("root")).render(<App />);
