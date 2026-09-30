import React, { useState } from "react";
import {
  Alert, Box, Button, IconButton, MenuItem, Stack, Tab, Tabs, TextField, Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import AddIcon from "@mui/icons-material/Add";

function emptyLineItemDraft() {
  return { itemCode: "", quantity: 1 };
}

function initialFields(catalog) {
  return {
    storeId: catalog.stores[0]?.id ?? "",
    tillId: catalog.tills[0]?.id ?? "",
    operatorId: catalog.operators[0]?.id ?? "",
    paymentType: catalog.paymentMethods.find((method) => method.id === "CASH")?.id ?? catalog.paymentMethods[0]?.id ?? "",
    seed: "",
    itemCount: "",
    lineItems: [emptyLineItemDraft()],
    membershipNumber: "",
    membershipLevel: "0",
    customerNumber: "",
    vehicleRegistration: "",
    vehicleVin: "",
    refundFlag: "INVOICE",
  };
}

function updateField(setFields, field, value) {
  setFields((current) => ({ ...current, [field]: value }));
}

function ReceiptFields({ catalog, fields, setFields }) {
  const setField = (field) => (event) => updateField(setFields, field, event.target.value);

  return <>
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2 }}>
      <TextField select label="Store" value={fields.storeId} onChange={setField("storeId")} fullWidth size="small">{catalog.stores.map((store) => <MenuItem key={store.id} value={store.id}>{store.name}</MenuItem>)}</TextField>
      <TextField select label="Till" value={fields.tillId} onChange={setField("tillId")} fullWidth size="small">{catalog.tills.map((till) => <MenuItem key={till.id} value={till.id}>{till.name}</MenuItem>)}</TextField>
      <TextField select label="Operator" value={fields.operatorId} onChange={setField("operatorId")} fullWidth size="small">{catalog.operators.map((operator) => <MenuItem key={operator.id} value={operator.id}>{operator.name}</MenuItem>)}</TextField>
      <TextField select label="Payment type" value={fields.paymentType} onChange={setField("paymentType")} fullWidth size="small">{catalog.paymentMethods.map((method) => <MenuItem key={method.id} value={method.id}>{method.name}</MenuItem>)}</TextField>
    </Stack>
    <Typography variant="subtitle2" fontWeight={800} sx={{ mb: 1 }}>Receipt customer fields (optional)</Typography>
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2 }}>
      <TextField label="Membership number" value={fields.membershipNumber} onChange={setField("membershipNumber")} size="small" fullWidth />
      <TextField select label="Membership level" value={fields.membershipLevel} onChange={setField("membershipLevel")} size="small" fullWidth sx={{ minWidth: { sm: 220 } }} SelectProps={{ MenuProps: { PaperProps: { sx: { minWidth: 220 } } } }}>{catalog.membershipLevels.map((level) => <MenuItem key={level.id} value={level.id}>{level.name}</MenuItem>)}</TextField>
      <TextField label="Customer number" value={fields.customerNumber} onChange={setField("customerNumber")} size="small" fullWidth />
    </Stack>
    <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2 }}>
      <TextField label="Vehicle registration" value={fields.vehicleRegistration} onChange={setField("vehicleRegistration")} size="small" sx={{ width: { xs: "100%", sm: 180 }, flex: { xs: "1 1 auto", sm: "0 0 180px" } }} helperText="Target length: 10 characters (not enforced yet)" />
      <TextField label="Vehicle VIN" value={fields.vehicleVin} onChange={setField("vehicleVin")} size="small" fullWidth />
      <TextField select label="Refund flag" value={fields.refundFlag} onChange={setField("refundFlag")} size="small" sx={{ width: { xs: "100%", sm: 180 }, minWidth: { sm: 180 }, flex: { xs: "1 1 auto", sm: "0 0 180px" } }} SelectProps={{ MenuProps: { PaperProps: { sx: { minWidth: 180 } } } }}>
        <MenuItem value="INVOICE">INVOICE</MenuItem>
        <MenuItem value="CREDIT">CREDIT</MenuItem>
      </TextField>
    </Stack>
  </>;
}

function ManualItems({ catalog, fields, setFields }) {
  function updateLineItem(index, changes) {
    setFields((current) => ({ ...current, lineItems: current.lineItems.map((item, itemIndex) => itemIndex === index ? { ...item, ...changes } : item) }));
  }
  function addLineItem() {
    setFields((current) => ({ ...current, lineItems: [...current.lineItems, emptyLineItemDraft()] }));
  }
  function removeLineItem(index) {
    setFields((current) => ({ ...current, lineItems: current.lineItems.filter((_, itemIndex) => itemIndex !== index) }));
  }

  return <Stack spacing={1} sx={{ mb: 2 }}>
    {fields.lineItems.map((lineItem, index) => <Stack direction="row" spacing={1} key={index} alignItems="center">
      <TextField select label="Product" value={lineItem.itemCode} onChange={(event) => updateLineItem(index, { itemCode: event.target.value })} size="small" sx={{ minWidth: 240 }}>
        {catalog.products.map((product) => <MenuItem key={product.itemCode} value={product.itemCode}>{product.itemDescription} ({product.unitPrice.toFixed(2)})</MenuItem>)}
      </TextField>
      <TextField label="Qty" type="number" size="small" value={lineItem.quantity} onChange={(event) => updateLineItem(index, { quantity: event.target.value })} sx={{ width: 90 }} inputProps={{ min: 1, max: 99 }} />
      <IconButton aria-label="Remove line item" onClick={() => removeLineItem(index)} disabled={fields.lineItems.length === 1}><CloseIcon fontSize="small" /></IconButton>
    </Stack>)}
    <Button size="small" startIcon={<AddIcon />} onClick={addLineItem} sx={{ alignSelf: "flex-start" }}>Add product</Button>
  </Stack>;
}

function RandomOptions({ fields, setFields }) {
  return <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ mb: 2 }}>
    <TextField label="Item count (optional)" type="number" size="small" value={fields.itemCount} onChange={(event) => updateField(setFields, "itemCount", event.target.value)} helperText="Leave blank for a random 1-5 items" inputProps={{ min: 1, max: 20 }} />
    <TextField label="Seed (optional)" type="number" size="small" value={fields.seed} onChange={(event) => updateField(setFields, "seed", event.target.value)} helperText="Reuse a seed to reproduce the same items and customer" inputProps={{ min: 0 }} />
  </Stack>;
}

export function ReceiptGenerationForm({ catalog, onGenerate }) {
  const [tab, setTab] = useState(0);
  const [manualFields, setManualFields] = useState(() => initialFields(catalog));
  const [randomFields, setRandomFields] = useState(() => initialFields(catalog));
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const fields = tab === 0 ? manualFields : randomFields;
  const setFields = tab === 0 ? setManualFields : setRandomFields;

  async function submit(event) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    const request = {
      storeId: fields.storeId,
      tillId: fields.tillId,
      operatorId: fields.operatorId,
      paymentType: fields.paymentType,
      refundFlag: fields.refundFlag,
      customer: {
        membershipNumber: fields.membershipNumber || null,
        membershipLevel: fields.membershipLevel,
        customerNumber: fields.customerNumber || null,
        vehicleRegistration: fields.vehicleRegistration || null,
        vehicleVin: fields.vehicleVin || null,
      },
    };
    if (fields.seed.trim() !== "") request.seed = Number(fields.seed);
    if (tab === 0) {
      request.lineItems = fields.lineItems.filter((item) => item.itemCode).map((item) => ({ itemCode: item.itemCode, quantity: Number(item.quantity) || 1 }));
    } else if (fields.itemCount.trim() !== "") {
      request.itemCount = Number(fields.itemCount);
    }
    try {
      await onGenerate(request);
      if (tab === 0) setManualFields((current) => ({ ...current, lineItems: [emptyLineItemDraft()] }));
    } catch (submitError) {
      setError(submitError.message);
    } finally {
      setSubmitting(false);
    }
  }

  return <Box component="form" onSubmit={submit}>
    <Typography variant="subtitle1" fontWeight={800} gutterBottom>Generate a receipt</Typography>
    {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>{error}</Alert>}
    <Tabs value={tab} onChange={(_, value) => setTab(value)} sx={{ mb: 2 }} aria-label="Receipt generation mode">
      <Tab label="Manual receipt entry" />
      <Tab label="Random receipt generation" />
    </Tabs>
    <ReceiptFields catalog={catalog} fields={fields} setFields={setFields} />
    {tab === 0 ? <>
      <Typography variant="subtitle2" fontWeight={800} sx={{ mb: 1 }}>Receipt line items</Typography>
      <ManualItems catalog={catalog} fields={manualFields} setFields={setManualFields} />
      <Button type="submit" variant="contained" disabled={submitting}>Generate receipt</Button>
    </> : <>
      <Typography variant="subtitle2" fontWeight={800} sx={{ mb: 1 }}>Random receipt options</Typography>
      <RandomOptions fields={randomFields} setFields={setRandomFields} />
      <Button type="submit" variant="contained" disabled={submitting}>Generate random receipt</Button>
    </>}
  </Box>;
}
