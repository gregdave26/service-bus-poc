import React from "react";
import { Chip, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import { formatCurrency } from "./digitalSiteFlowState.js";

const ORDER_STATE_COLOURS = { Complete: "success", Open: "warning", Cancelled: "default", Confirmed: "info" };

function HistoryTable({ label, rows, columns }) {
  return <Table size="small" aria-label={label}>
    <TableHead><TableRow>{columns.map((column) => <TableCell key={column.title}>{column.title}</TableCell>)}</TableRow></TableHead>
    <TableBody>{rows.map((row) => <TableRow key={row.id}>{columns.map((column) => <TableCell key={column.title}>{column.render(row)}</TableCell>)}</TableRow>)}</TableBody>
  </Table>;
}

const formatDate = (value) => new Date(value).toLocaleString();

const CART_COLUMNS = [
  { title: "Cart", render: (cart) => cart.id.slice(0, 8) },
  { title: "Updated", render: (cart) => formatDate(cart.lastModifiedAt) },
  { title: "Cover", render: (cart) => cart.coverName ?? "—" },
  { title: "Total", render: (cart) => formatCurrency(cart.totalPrice) },
  { title: "Vehicle", render: (cart) => cart.vehicle?.rego ?? "—" },
];

const ORDER_COLUMNS = [
  { title: "Order", render: (order) => order.orderNumber ?? order.id.slice(0, 8) },
  { title: "Created", render: (order) => formatDate(order.createdAt) },
  { title: "Cover", render: (order) => order.coverName ?? "—" },
  { title: "Total", render: (order) => formatCurrency(order.totalPrice) },
  { title: "State", render: (order) => <Chip size="small" color={ORDER_STATE_COLOURS[order.orderState] ?? "default"} label={order.orderState} /> },
];

// The commercetools carts and orders for the mock CRM id, as myRAC might show them.
export function MemberHistory({ crmId, history }) {
  const activeCarts = history?.activeCarts ?? [];
  const orders = history?.orders ?? [];
  return <Paper variant="outlined" sx={{ p: 2 }}>
    <Typography variant="h6">Member history</Typography>
    <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>commercetools carts and orders where anonymousId = {crmId}</Typography>
    <Stack spacing={2}>
      <Typography variant="subtitle2">Active carts</Typography>
      {activeCarts.length === 0 ? <Typography color="text.secondary">No active carts.</Typography> : <HistoryTable label="Active carts" rows={activeCarts} columns={CART_COLUMNS} />}
      <Typography variant="subtitle2">Orders</Typography>
      {orders.length === 0 ? <Typography color="text.secondary">No orders yet. Complete the flow to place one.</Typography> : <HistoryTable label="Orders" rows={orders} columns={ORDER_COLUMNS} />}
    </Stack>
  </Paper>;
}
