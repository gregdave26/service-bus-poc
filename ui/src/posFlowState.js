export function posStageState(flow, index) {
  if (flow.state === "processing") {
    if (index < flow.activeStage) return "completed";
    if (index === flow.activeStage) return "active";
  }
  if (flow.state === "completed" && index <= (flow.exportData ? 2 : 1)) {
    return "completed";
  }
  return "idle";
}
