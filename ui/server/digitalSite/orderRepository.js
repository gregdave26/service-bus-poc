export function createOrderRepository(db) {
  db.exec(`
    CREATE TABLE IF NOT EXISTS RSA_ORDER (
      OrderId TEXT PRIMARY KEY,
      Sequence INTEGER NOT NULL UNIQUE,
      CreatedAt TEXT NOT NULL,
      ContactId TEXT NOT NULL,
      CoverId TEXT NOT NULL,
      PaymentPlan TEXT NOT NULL,
      VehicleRego TEXT,
      PublishStatus TEXT NOT NULL CHECK (PublishStatus IN ('pending', 'published', 'publish_failed')),
      EventId TEXT,
      PublishError TEXT,
      OrderJson TEXT NOT NULL
    )
  `);

  const insertStatement = db.prepare(`
    INSERT INTO RSA_ORDER (OrderId, Sequence, CreatedAt, ContactId, CoverId, PaymentPlan, VehicleRego, PublishStatus, OrderJson)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
  `);
  const updatePublishStatement = db.prepare(`
    UPDATE RSA_ORDER SET PublishStatus = ?, EventId = ?, PublishError = ? WHERE OrderId = ?
  `);
  const selectColumns = "OrderJson, PublishStatus, EventId, PublishError";

  function toOrder(row) {
    return row
      ? { ...JSON.parse(row.OrderJson), publishStatus: row.PublishStatus, eventId: row.EventId, publishError: row.PublishError }
      : null;
  }

  return {
    nextSequenceNumber() {
      return (db.prepare("SELECT COALESCE(MAX(Sequence), 0) AS lastSequence FROM RSA_ORDER").get().lastSequence) + 1;
    },
    insertOrder(order, sequence) {
      insertStatement.run(
        order.orderId, sequence, order.createdAt, order.contactId, order.cover.id,
        order.paymentPlan.id, order.vehicle?.rego ?? null, order.publishStatus, JSON.stringify(order),
      );
    },
    updatePublishStatus(orderId, { publishStatus, eventId = null, publishError = null }) {
      updatePublishStatement.run(publishStatus, eventId, publishError, orderId);
    },
    getOrderById(orderId) {
      return toOrder(db.prepare(`SELECT ${selectColumns} FROM RSA_ORDER WHERE OrderId = ?`).get(orderId));
    },
    listOrders(limit = 20) {
      return db.prepare(`SELECT ${selectColumns} FROM RSA_ORDER ORDER BY Sequence DESC LIMIT ?`).all(limit).map(toOrder);
    },
  };
}
