// Pairs each state's main and PR crops into one labelled image.
//
//   node compose.mjs <dir> --main-sha <sha> --pr-sha <sha> [--pixel]
//
// Reads <dir>/main and <dir>/pr from run-scenario.mjs, writes <dir>/<state>.png, and prints
// "<state>\t<path>" per state. A side without the state gets a "not on this build" panel.
// --pixel also writes <dir>/pixel-diff.txt: "<state>\tidentical" or what differs, per state.

import { writeFileSync } from "node:fs";
import { join, resolve } from "node:path";

import { assertBothSidesRan, composeState, pixelDiff, stateNames } from "./lib/compose-state.mjs";

const parseArgs = (args) => {
  const options = { dir: args[0] };
  for (let i = 1; i < args.length; i++) {
    if (args[i] === "--main-sha") options.mainSha = args[++i];
    else if (args[i] === "--pr-sha") options.prSha = args[++i];
    else if (args[i] === "--pixel") options.pixel = true;
  }
  if (!options.dir || !options.mainSha || !options.prSha) {
    throw new Error("usage: compose.mjs <dir> --main-sha <sha> --pr-sha <sha> [--pixel]");
  }
  return options;
};

const { dir, mainSha, prSha, pixel } = parseArgs(process.argv.slice(2));
const root = resolve(dir);
assertBothSidesRan(root);
const diffs = [];
for (const state of stateNames(root)) {
  console.log(`${state}\t${await composeState(root, state, mainSha, prSha)}`);
  if (pixel) diffs.push(`${state}\t${await pixelDiff(root, state)}`);
}
if (pixel) {
  writeFileSync(join(root, "pixel-diff.txt"), `${diffs.join("\n")}\n`);
  console.log(`pixel-diff\t${join(root, "pixel-diff.txt")}`);
}
