// Command line for lib/attach-body.mjs, used by attach.sh.
//
//   node attach-body.mjs plan <body.md>
//     Prints "image<TAB><path>#<alt>" and "video<TAB><path>" per file the body references.
//   node attach-body.mjs place <uploaded.md> <out.md> <video path>...
//     Writes the body with the appended video urls moved into place and prints each url in order.

import { readFileSync, writeFileSync } from "node:fs";

import { attachPlan, placeVideos } from "./lib/attach-body.mjs";

const [command, ...args] = process.argv.slice(2);

if (command === "plan" && args.length === 1) {
  const { images, videos } = attachPlan(readFileSync(args[0], "utf8"));
  for (const image of images) console.log(`image\t${image}`);
  for (const video of videos) console.log(`video\t${video}`);
} else if (command === "place" && args.length >= 3) {
  const [uploaded, out, ...videos] = args;
  const placed = placeVideos(readFileSync(uploaded, "utf8"), videos);
  writeFileSync(out, placed.body);
  for (const url of placed.urls) console.log(url);
} else {
  console.error("usage: attach-body.mjs plan <body.md> | place <uploaded.md> <out.md> <video path>...");
  process.exit(64);
}
