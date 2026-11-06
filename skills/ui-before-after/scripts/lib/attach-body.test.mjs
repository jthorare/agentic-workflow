import assert from "node:assert/strict";
import { test } from "node:test";

import { attachPlan, placeVideos } from "./attach-body.mjs";

const URL_A = "https://github.com/user-attachments/assets/aaaa";
const URL_B = "https://github.com/user-attachments/assets/bbbb";
const IMG = "https://github.com/user-attachments/assets/img1";

const BODY = [
  "| Field | ![main vs PR: field](./field.png) |",
  "| Menu | ![main vs PR: menu](./menu.png) |",
  "",
  "Recording, main:",
  "",
  "./main/main.webm",
  "",
  "Recording, PR:",
  "",
  "./pr/pr.webm",
  "",
].join("\n");

test("attachPlan lists images with their alt text, then videos, in body order", () => {
  assert.deepEqual(attachPlan(BODY), {
    images: ["./field.png#main vs PR: field", "./menu.png#main vs PR: menu"],
    videos: ["./main/main.webm", "./pr/pr.webm"],
  });
});

test("attachPlan lists a file referenced twice once", () => {
  assert.deepEqual(attachPlan("![a](./x.png)\n![b](./x.png)\n").images, ["./x.png#a"]);
});

test("placeVideos moves the appended video urls under their recording lines in attach order", () => {
  const uploaded = `${BODY.replace("./field.png", IMG)}\n${URL_A}\n${URL_B}\n`;
  const placed = placeVideos(uploaded, ["./main/main.webm", "./pr/pr.webm"]);
  assert.deepEqual(placed.urls, [URL_A, URL_B]);
  assert.equal(
    placed.body,
    [
      `| Field | ![main vs PR: field](${IMG}) |`,
      "| Menu | ![main vs PR: menu](./menu.png) |",
      "",
      "Recording, main:",
      "",
      URL_A,
      "",
      "Recording, PR:",
      "",
      URL_B,
      "",
    ].join("\n"),
  );
});

test("placeVideos leaves earlier bare urls alone", () => {
  const placed = placeVideos(`intro\n${URL_A}\n\n./pr/pr.webm\n\n${URL_B}\n`, ["./pr/pr.webm"]);
  assert.equal(placed.body, `intro\n${URL_A}\n\n${URL_B}\n`);
});

test("placeVideos throws when fewer urls were appended than videos attached", () => {
  assert.throws(() => placeVideos(`./a.webm\n./b.webm\n${URL_A}\n`, ["./a.webm", "./b.webm"]), /2 videos/);
});

test("placeVideos throws when a video reference is missing from the body", () => {
  assert.throws(() => placeVideos(`text\n${URL_A}\n`, ["./a.webm"]), /\.\/a\.webm/);
});
