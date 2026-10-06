import assert from "node:assert/strict";
import test from "node:test";
import { processFlowColors, processStageSx } from "../src/processFlowStyles.js";
import { posStageState } from "../src/posFlowState.js";

test("completed flow stages use the Contact Events completion colors", () => {
  assert.equal(processFlowColors.completed.background, "#c8e6c9");
  assert.equal(processFlowColors.completed.border, "#2e7d32");
  assert.deepEqual(processStageSx("completed"), {
    bgcolor: "#c8e6c9",
    borderColor: "#2e7d32",
    borderWidth: 2,
  });
});

test("active and idle stages remain visually distinct from completed stages", () => {
  assert.deepEqual(processStageSx("active"), {
    bgcolor: "#d9ccf2",
    borderColor: "#6750a4",
    borderWidth: 2,
  });
  assert.deepEqual(processStageSx("idle"), {
    bgcolor: "#fffbfe",
    borderColor: "#c9c5ca",
    borderWidth: 1,
  });
});

test("POS keeps previous steps completed while export is processing", () => {
  const flow = { state: "processing", activeStage: 2 };
  assert.deepEqual([0, 1, 2].map((index) => posStageState(flow, index)), [
    "completed", "completed", "active",
  ]);
});

test("POS only completes the export step when its file is ready", () => {
  assert.deepEqual([0, 1, 2].map((index) => posStageState({ state: "completed" }, index)), [
    "completed", "completed", "idle",
  ]);
  assert.deepEqual([0, 1, 2].map((index) => posStageState({
    state: "completed", exportData: { fileName: "receipt.psv" },
  }, index)), ["completed", "completed", "completed"]);
  assert.equal(posStageState({ state: "idle", activeStage: -1 }, 0), "idle");
});
