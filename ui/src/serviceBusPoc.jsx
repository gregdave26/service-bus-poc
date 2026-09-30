import React, { useEffect, useState } from "react";
import {
  Alert, Box, Button, Checkbox, Chip, Collapse, Dialog, DialogContent, DialogTitle,
  Divider, FormControlLabel, IconButton, MenuItem, Paper, Stack, Table, TableBody,
  TableCell, TableHead, TableRow, TextField, Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ExpandLessIcon from "@mui/icons-material/ExpandLess";
import AddIcon from "@mui/icons-material/Add";

export const drafts = [
  { id: "contact-updated", name: "Contact updated", type: "ContactUpdated", folder: "Contact events", data: { contactId: "1042c5b8-1a3d-4d7a-9f02-7c4f7e2b8c11", firstName: "Ada", lastName: "Lovelace", phone: "0400000000", email: "ada@example.com", hasInsurance: true, hasParksResorts: false, hasCarwashProduct: true } },
  { id: "product-holding-change", name: "Product holding change", type: "ProductHoldingChange", folder: "Contact events", data: { contactId: "1088c5b8-2b4e-4e8b-a013-8d5f8f3c9d22", holdingId: "holding-1088", productType: "insurance", action: "created" } },
  { id: "regression", name: "Routing regression", type: "ContactUpdated", folder: "Regression checks", data: { contactId: "c0ffee00-0000-4000-8000-000000000099", firstName: "Test", lastName: "Contact", phone: "0400000099", email: "test@example.com", hasInsurance: true, hasParksResorts: true, hasCarwashProduct: true } },
];

export function fieldsFor(type, config) { return config.messageTypes?.[type]?.Fields ?? []; }
export function emptyData(fields) { return Object.fromEntries(fields.map((field) => [field.Name, field.Type === "boolean" ? false : ""])); }
export function createUuid() { return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`; }
export function normalizedName(name) { return name.trim().toLowerCase(); }

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

export function MessageDialog({ service, messages, config, onClose }) {
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

export function NewMessageDialog({ open, config, existingNames, onClose, onCreate }) {
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

export function MessageEditorDialog({ open, draft, form, fields, config, messageName, duplicateName, onNameChange, onChange, onSave, onPublish, onClose }) {
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

export function ServiceCard({ service, config, selected, onClick }) {
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

export function ActivityStream({ messages, statuses, config }) {
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
