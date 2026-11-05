import assert from "node:assert/strict";
import test from "node:test";
import { SHORT_CONTACT_ID_LENGTH, shortContactId } from "../src/contactIdDisplay.js";

test("shortens a contact ID to the git-style prefix length", () => {
  assert.equal(SHORT_CONTACT_ID_LENGTH, 7);
  assert.equal(shortContactId("1042c5b8-1a3d-4d7a-9f02-7c4f7e2b8c11"), "1042c5b");
});

test("returns the whole value when it is already shorter than the prefix length", () => {
  assert.equal(shortContactId("c-1"), "c-1");
});

test("returns an empty string when the contact ID is missing", () => {
  assert.equal(shortContactId(undefined), "");
  assert.equal(shortContactId(null), "");
});
