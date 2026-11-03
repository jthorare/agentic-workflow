# Probe template

A Playwright script that proves each option works on the seeded local stack. Copy it to `<proto-dir>/probe.mjs` (the absolute `$AW_DIR/prototypes/<slug>` path echoed in Phase 1), edit the `CONFIG` block and each option's `steps`, and run it with the prototype worktree as the working directory as `node <proto-dir>/probe.mjs <proto-dir>`. The file lives outside the worktree, so it loads the project's own `playwright` package from the cwd, not from its own folder.

It uses the project's existing Playwright. If the project has none, **Ask the user** before installing anything. It signs in through the real login form (no cookie shortcuts).

```js
import { mkdirSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";

// probe.mjs lives outside the worktree: resolve playwright from the cwd (the worktree).
const require = createRequire(process.cwd() + "/package.json");
const { chromium } = require("playwright");

const OUT = process.argv[2]; // the absolute $AW_DIR/prototypes/<slug> path, first CLI argument
if (!OUT) {
  console.error("usage: node probe.mjs <output-dir> (the absolute $AW_DIR/prototypes/<slug> path)");
  process.exit(2);
}

// ---- CONFIG ---------------------------------------------------------------
const CONFIG = {
  baseUrl: "http://localhost:<port>", // from the brief's Stack section
  route: "<full URL of the screen>", // the seeded route; re-read it after every re-seed
  login: {
    // The real sign-in form. Replace the selectors and read credentials from the
    // project's seed output or env; never hardcode a real password here.
    email: process.env.PROTO_EMAIL,
    password: process.env.PROTO_PASSWORD,
    steps: async (page) => {
      await page.goto(`${CONFIG.baseUrl}/login`);
      await page.getByLabel("Email").fill(CONFIG.login.email);
      await page.getByLabel("Password").fill(CONFIG.login.password);
      await page.getByRole("button", { name: /sign in/i }).click();
    },
  },
  flags: [], // client-side flags as "storage-key=value"
  // The element that proves the REAL screen has rendered with data.
  ready: (page) => page.getByTestId("<real-screen-testid>"),
  viewport: { width: 1600, height: 1000 },
  settleMs: 1500,
};

// Local stack only (hard rule 6): refuse any non-loopback base URL.
for (const url of [CONFIG.baseUrl, new URL(CONFIG.route, CONFIG.baseUrl).href]) {
  const host = new URL(url).hostname;
  if (!["localhost", "127.0.0.1", "::1", "[::1]"].includes(host)) {
    console.error(`refusing to run against non-loopback host: ${host}`);
    process.exit(2);
  }
}

// One entry per option from the brief. The first is always `current`.
const OPTIONS = [
  {
    id: "current", // matches OPTIONS[].id in PrototypeOptions.tsx
    steps: async (page) => {
      await CONFIG.ready(page).waitFor();
    },
  },
  // { id: "a", steps: async (page) => {
  //     await page.getByTestId("...").click();
  //     await page.getByText("...").waitFor(); // assert the NEW thing is visible
  //   } },
];
// ---------------------------------------------------------------------------

mkdirSync(`${OUT}/shots`, { recursive: true });
const errors = [];
const results = [];

const browser = await chromium.launch();
// Many apps gate the headless build's user agent ("HeadlessChrome") with an
// "Unsupported browser" page. Present as plain Chrome of the same version.
const userAgent = `Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${browser.version()} Safari/537.36`;
const context = await browser.newContext({ viewport: CONFIG.viewport, userAgent });
const page = await context.newPage();
page.setDefaultTimeout(60_000);
page.on("pageerror", (e) => errors.push(String(e)));

try {
  await context.addInitScript((flags) => {
    for (const flag of flags) {
      const [key, value] = flag.split("=");
      try {
        localStorage.setItem(key, value ?? "true");
      } catch {}
    }
  }, CONFIG.flags);
  await CONFIG.login.steps(page);

  for (const option of OPTIONS) {
    // Select through the switcher the user will use, then reload the route so the
    // choice is read back from localStorage (proves persistence too).
    await page.goto(CONFIG.route);
    await CONFIG.ready(page).waitFor();
    const pill = page.getByTestId(`prototype-option-${option.id}`);
    await pill.click();
    if ((await pill.getAttribute("aria-pressed")) !== "true")
      throw new Error(`switcher did not select ${option.id}`);
    await page.evaluate(() => document.fonts.ready);
    await option.steps(page);
    await page.mouse.move(5, 5); // keep the pointer off the subject
    // Let transitions, animations and late rows finish. A shot taken right after the
    // click can show the previous option's highlight while the DOM is already right.
    await page.waitForTimeout(CONFIG.settleMs);
    const shot = `${OUT}/shots/${option.id}.png`;
    await page.screenshot({ path: shot });
    results.push({ option: option.id, shot });
    console.log(`ok - ${option.id} -> ${shot}`);
  }
} catch (error) {
  await page.screenshot({ path: `${OUT}/shots/failure.png` }).catch(() => {});
  console.error("FAILED:", error);
  errors.push(`probe: ${String(error)}`);
} finally {
  await browser.close();
}

writeFileSync(`${OUT}/probe-result.json`, JSON.stringify({ results, errors }, null, 2));
console.log(`page errors: ${errors.length}`);
if (errors.length > 0) {
  console.error(errors.join("\n"));
  process.exit(1);
}
```

## Reading the result

- Exit code 0 and `page errors: 0` is necessary, not sufficient. **Open every PNG in `shots/` yourself.**
- `shots/current.png` must look like the real screen. Compare it with a screenshot of the real app without the prototype. `shots/failure.png` appears only when a step failed; read it first.
- Compare each option's shot with its description in the brief and say what you saw.
- Add per-option assertions that the NEW element is visible (`waitFor`) before any "is gone" assertion.
