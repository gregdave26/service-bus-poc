import React, { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  Alert,
  AppBar,
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
  { id: "contact-updated", name: "Contact updated", folder: "Contact events", data: { contactId: "contact-1042", firstName: "Ada", lastName: "Lovelace", phone: "0400000000", email: "ada@example.com", hasInsurance: true, hasParksResorts: false, hasCarwashProduct: true } },
  { id: "new-member", name: "New member welcome", folder: "Contact events", data: { contactId: "contact-1088", firstName: "Grace", lastName: "Hopper", phone: "0400000011", email: "grace@example.com", hasInsurance: false, hasParksResorts: true, hasCarwashProduct: false } },
  { id: "regression", name: "Routing regression", folder: "Regression checks", data: { contactId: "contact-test", firstName: "Test", lastName: "Contact", phone: "0400000099", email: "test@example.com", hasInsurance: true, hasParksResorts: true, hasCarwashProduct: true } },
];

async function getJson(url, options) {
  const response = await fetch(url, options);
  const body = await response.json();
  if (!response.ok) throw new Error(body.error || `Request failed (${response.status})`);
  return body;
}

function displayName(service, config) {
  if (service === "producer") return config.producerLabels?.DisplayName || "Message Publisher";
  return config.subscriberLabels?.[service]?.DisplayName || service.replace(/-/g, " ").replace(/\b\w/g, (letter) => letter.toUpperCase());
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

function ServiceCard({ service, config, selected, onClick }) {
  const connected = String(service.state).toLowerCase() !== "offline";
  const name = displayName(service.serviceName, config);
  return <Card variant="outlined" sx={{ borderColor: selected ? "primary.main" : "divider", bgcolor: selected ? "primary.50" : "background.paper" }}>
    <CardActionArea onClick={service.selectable === false ? undefined : onClick} disabled={service.selectable === false} sx={{ p: 2, minHeight: 132 }}>
      <Stack spacing={1}>
        <Stack direction="row" justifyContent="space-between"><Typography variant="caption" color="text.secondary">{service.infrastructure ? "Infrastructure" : "Service"}</Typography><Typography color="primary.main">{service.icon || "◎"}</Typography></Stack>
        <Typography variant="h6">{name}</Typography>
        <Stack direction="row" spacing={1} alignItems="center"><Box component="span" className={`status-dot ${connected ? "connected" : "offline"}`} /><Typography variant="body2" color="text.secondary">{connected ? "Connected" : "Offline"}{service.messagesHandled === null ? "" : ` · ${service.messagesHandled ?? 0} handled`}</Typography></Stack>
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
      <Table size="small" aria-label="Event activity stream"><TableHead><TableRow><TableCell>Event</TableCell><TableCell>Service</TableCell><TableCell>Direction</TableCell><TableCell>Status</TableCell><TableCell>Time</TableCell><TableCell /></TableRow></TableHead><TableBody>
        {activity.map((message) => { const open = expanded === message.messageId; const status = message.direction === "sent" ? "Published" : (statusMap.get(message.serviceName)?.state || "Observed"); return <React.Fragment key={message.messageId}>
          <TableRow hover onClick={() => setExpanded(open ? null : message.messageId)} sx={{ cursor: "pointer" }}><TableCell sx={{ fontWeight: 700 }}>{JSON.parse(message.payload || "{}").type || message.eventId}</TableCell><TableCell>{displayName(message.serviceName, config)}</TableCell><TableCell><Chip size="small" label={message.direction === "sent" ? "Published" : "Received"} color={message.direction === "sent" ? "primary" : "secondary"} /></TableCell><TableCell><Chip size="small" label={status} color={status === "Offline" ? "warning" : "default"} /></TableCell><TableCell>{new Date(message.timestamp).toLocaleString()}</TableCell><TableCell>{open ? <ExpandLessIcon /> : <ExpandMoreIcon />}</TableCell></TableRow>
          <TableRow><TableCell colSpan={6} sx={{ p: 0, border: 0 }}><Collapse in={open}><Box component="pre" sx={{ m: 1.5, overflow: "auto", p: 2, bgcolor: "grey.900", color: "grey.100", borderRadius: 1, fontSize: 12 }}>{message.payload}</Box></Collapse></TableCell></TableRow>
        </React.Fragment>; })}
      </TableBody></Table>}
  </Paper>;
}

function App() {
  const [config, setConfig] = useState({ subscriberLabels: {}, producerLabels: {} });
  const [statuses, setStatuses] = useState([]);
  const [messages, setMessages] = useState([]);
  const [emulator, setEmulator] = useState({ running: false });
  const [selectedService, setSelectedService] = useState(null);
  const [activeService, setActiveService] = useState(null);
  const [selectedDraft, setSelectedDraft] = useState(drafts[0]);
  const [form, setForm] = useState(drafts[0].data);
  const [toast, setToast] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  async function refresh() {
    try {
      const [nextConfig, nextEmulator, nextStatuses, nextMessages] = await Promise.all([getJson("/api/config"), getJson("/api/emulator-status"), getJson("/api/status"), getJson("/api/messages")]);
      setConfig({ subscriberLabels: nextConfig.subscriberLabels || {}, producerLabels: nextConfig.producerLabels || {} });
      setEmulator(nextEmulator); setStatuses(nextStatuses); setMessages(nextMessages); setError(null);
    } catch (refreshError) { setError(refreshError.message); } finally { setLoading(false); }
  }

  useEffect(() => { refresh(); const timer = setInterval(refresh, 3000); return () => clearInterval(timer); }, []);

  const services = useMemo(() => {
    const reported = statuses.filter((service) => !["Dashboard", "producer"].includes(service.serviceName));
    const names = new Set(reported.map((service) => service.serviceName));
    const observed = [...new Set(messages.map((message) => message.serviceName))].filter((name) => !names.has(name) && !["Dashboard", "producer"].includes(name)).map((serviceName) => ({ serviceName, state: "Live", messagesHandled: messages.filter((message) => message.serviceName === serviceName).length, selectable: true }));
    const producer = statuses.find((service) => service.serviceName === "producer") || { serviceName: "producer", state: "Ready", messagesHandled: messages.filter((message) => message.serviceName === "producer").length, selectable: true, infrastructure: true };
    return [{ serviceName: "Service Bus emulator", state: emulator.running ? "Running" : "Offline", messagesHandled: null, selectable: false, infrastructure: true, icon: "◈" }, { ...producer, icon: "◉" }, ...reported, ...observed];
  }, [statuses, messages, emulator]);

  function selectDraft(draft) { setSelectedDraft(draft); setForm({ ...draft.data }); }
  function updateForm(event) { const { name, value, type, checked } = event.target; setForm((current) => ({ ...current, [name]: type === "checkbox" ? checked : value })); }
  function saveDraft() { selectedDraft.data = { ...form }; setToast("Draft saved locally."); }
  async function publish(event) { event.preventDefault(); try { await getJson("/api/publish", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(form) }); setToast("Message published."); refresh(); } catch (publishError) { setToast(publishError.message); } }

  return <Container maxWidth="xl" sx={{ py: 3 }}>
    <AppBar position="static" color="transparent" elevation={0} sx={{ mb: 3 }}><Toolbar disableGutters><Box className="brand-mark">P</Box><Box sx={{ ml: 1.5 }}><Typography variant="overline" color="text.secondary">Messaging workspace</Typography><Typography variant="h5" color="text.primary">Contact events</Typography></Box><Box sx={{ ml: "auto", display: "flex", alignItems: "center" }}><Typography variant="body2" color="text.secondary" sx={{ mr: 1 }}>{loading ? "Checking services…" : error ? "Status unavailable" : "Live"}</Typography><IconButton onClick={refresh} aria-label="Refresh status"><RefreshIcon /></IconButton></Box></Toolbar></AppBar>
    {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>Could not refresh status: {error}</Alert>}
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 3 }}>{services.map((service) => <Box key={service.serviceName} sx={{ flex: 1, minWidth: 0 }}><ServiceCard service={service} config={config} selected={selectedService === service.serviceName} onClick={() => { setSelectedService(service.serviceName); setActiveService(service.serviceName); }} /></Box>)}</Stack>
    <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", lg: "260px minmax(320px, 1fr) minmax(320px, 1.15fr)" }, gap: 2 }}>
      <Paper variant="outlined"><Box sx={{ p: 2, display: "flex", justifyContent: "space-between" }}><Box><Typography variant="h6">Draft explorer</Typography><Typography variant="body2" color="text.secondary">Published by Producer</Typography></Box><IconButton aria-label="Create draft" onClick={() => { const draft = { id: `new-${Date.now()}`, name: "Untitled draft", folder: "New drafts", data: { contactId: "", firstName: "", lastName: "", phone: "", email: "", hasInsurance: false, hasParksResorts: false, hasCarwashProduct: false } }; drafts.push(draft); selectDraft(draft); }}><AddIcon /></IconButton></Box><Divider /><List dense>{["Contact events", "Regression checks"].map((folder) => <React.Fragment key={folder}><ListItemText primary={`› ${folder}`} sx={{ px: 2, py: 1, fontWeight: 700 }} />{drafts.filter((draft) => draft.folder === folder).map((draft) => <ListItemButton key={draft.id} selected={draft.id === selectedDraft.id} onClick={() => selectDraft(draft)} sx={{ pl: 3 }}><ListItemText primary={`▱ ${draft.name}`} /></ListItemButton>)}</React.Fragment>)}</List></Paper>
      <Paper variant="outlined" component="form" onSubmit={publish}><Box sx={{ p: 2 }}><Typography variant="h6">Message editor</Typography><Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>Edit the selected draft, then save it or publish it through the Producer.</Typography><Stack spacing={2}>{[["contactId", "Contact ID"], ["firstName", "First name"], ["lastName", "Last name"], ["phone", "Phone"], ["email", "Email"]].map(([name, label]) => <TextField key={name} name={name} label={label} value={form[name]} onChange={updateForm} required fullWidth size="small" type={name === "email" ? "email" : "text"} />)}<Stack direction="row" flexWrap="wrap">{[["hasInsurance", "Insurance"], ["hasParksResorts", "Parks & Resorts"], ["hasCarwashProduct", "Carwash"]].map(([name, label]) => <FormControlLabel key={name} control={<Checkbox name={name} checked={form[name]} onChange={updateForm} />} label={label} />)}</Stack><Stack direction="row" justifyContent="flex-end" spacing={1}><Button variant="outlined" onClick={saveDraft}>Save draft</Button><Button type="submit" variant="contained">Publish</Button></Stack></Stack></Box></Paper>
      <ActivityStream messages={messages} statuses={statuses} config={config} />
    </Box>
    <MessageDialog service={activeService} messages={messages} config={config} onClose={() => setActiveService(null)} />
    <Snackbar open={Boolean(toast)} autoHideDuration={4500} onClose={() => setToast(null)} message={toast} />
    {loading && <CircularProgress size={24} sx={{ position: "fixed", bottom: 24, left: 24 }} />}
  </Container>;
}

createRoot(document.getElementById("root")).render(<ThemeProvider theme={theme}><App /></ThemeProvider>);
