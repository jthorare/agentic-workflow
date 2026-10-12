import { test } from "node:test";
import assert from "node:assert/strict";
import { listCommands, runCommand } from "../../src/commands/registry.js";
import { startApp } from "../../src/app.js";

test("the app registers its tab commands", () => {
  startApp();
  assert.deepEqual(listCommands().map((c) => c.id).sort(), ["close-tab", "new-tab"]);
});

test("an unknown command throws", () => {
  assert.throws(() => runCommand("nope" as never, startApp()));
});
