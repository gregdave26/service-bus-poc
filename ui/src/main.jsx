import React, { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  Alert, AppBar, Box, Button, CircularProgress, Container, IconButton, List,
  ListItemButton, ListItemText, Paper, Snackbar, Stack, Toolbar, Typography,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import AddIcon from "@mui/icons-material/Add";
import ReceiptLongIcon from "@mui/icons-material/ReceiptLong";
import { createTheme, ThemeProvider } from "@mui/material/styles";
import {
  ActivityStream, MessageDialog, MessageEditorDialog, NewMessageDialog, ServiceCard,
  createUuid, drafts, emptyData, fieldsFor, normalizedName,
} from "./serviceBusPoc.jsx";
import { PosProcessing } from "./localPosProcessing.jsx";
import "./styles.css";

async function getJson(url, options) {
  const response = await fetch(url, options);
  const body = await response.json();
  if (!response.ok) throw new Error(body.error || `Request failed (${response.status})`);
  return body;
}

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
