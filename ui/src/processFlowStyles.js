export const processFlowColors = {
  idle: { background: "#fffbfe", border: "#c9c5ca" },
  active: { background: "#d9ccf2", border: "#6750a4" },
  completed: { background: "#c8e6c9", border: "#2e7d32" },
  fileReady: { background: "#d7f0dc", border: "#2e7d32" },
};

export function processStageSx(state) {
  const colors = processFlowColors[state];
  return { bgcolor: colors.background, borderColor: colors.border, borderWidth: state === "idle" ? 1 : 2 };
}
