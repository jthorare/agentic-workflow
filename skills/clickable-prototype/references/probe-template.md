# Probe template

A Playwright script that proves each option works on the seeded local stack. Copy it to `<proto-dir>/probe.mjs` (the absolute `$AW_DIR/prototypes/<slug>` path echoed in Phase 1), edit the `CONFIG` block and each option's `steps`, and run it with the prototype worktree as the working directory: `cd <wt> && node <proto-dir>/probe.mjs --out <proto-dir>` (add `--baseline` for Gate 2). The file lives outside the worktree, so it loads the project's own `playwright` package from the cwd (`createRequire`), not from its own folder.

It uses the project's existing Playwright. If the project has none, **Ask the user** before installing anything. It signs in through the real login form (no cookie shortcuts). The login is the local seed account, which is not a secret: write it into `CONFIG` literally (the run takes no env prefix, so the allowlist matches).

```js
import { mkdirSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";

// probe.mjs lives outside the worktree: resolve playwright from the cwd (the worktree).
const require = createRequire(process.cwd() + "/package.json");
const { chromium } = require("playwright");

const outFlag = process.argv.indexOf("--out");
const OUT = outFlag > 0 ? (process.argv[outFlag + 1] ?? "") : ""; // $AW_DIR/prototypes/<slug>, passed as --out <dir>
if (OUT === "") {
  console.error("usage: node probe.mjs --out <output-dir> [--baseline]");
  process.exit(2);
}
// `--baseline` (Gate 2, before the switcher exists): no pill, just prove the real screen renders.
const BASELINE = process.argv.includes("--baseline");

// ---- CONFIG ---------------------------------------------------------------
const CONFIG = {
  baseUrl: "http://localhost:<port>", // from the brief's Stack section
  route: "<full URL of the screen>", // the seeded route; re-read it after every re-seed
  storageKey: "prototype-option:<slug>", // STORAGE_KEY in PrototypeOptions.tsx
  login: {
    // The real sign-in form. Replace the selectors and write the local seed login here.
    email: "<seed email>",
    password: "<seed password>",
    steps: async (page) => {
      await page.goto(`${CONFIG.baseUrl}/login`);
      await page.getByLabel("Email").fill(CONFIG.login.email);
      await page.getByLabel("Password").fill(CONFIG.login.password);
      await page.getByRole("button", { name: /sign in/i }).click();
      // Wait for the sign-in to settle before the first goto, or it can abort the login.
      await page.waitForURL((u) => !/login|sign-?in/i.test(u.pathname));
    },
  },
  flags: [], // client-side flags as "storage-key=value"
  // The element that proves the REAL screen has rendered with data. It must not be
  // inside anything an option replaces, or the wait hangs for that option.
  ready: (page) => page.getByTestId("<real-screen-testid>"),
  viewport: { width: 1600, height: 1000 },
  settleMs: 1500,
};

// Local stack only (hard rule 6): refuse any non-loopback base URL or route.
for (const url of [CONFIG.baseUrl, new URL(CONFIG.route, CONFIG.baseUrl).href]) {
  const host = new URL(url).hostname;
  if (!["localhost", "127.0.0.1", "::1", "[::1]"].includes(host)) {
    console.error(`refusing to run against non-loopback host: ${host}`);
    process.exit(2);
  }
}

// One entry per option from the brief. The first is always `current`.
// `id` matches OPTIONS[].id in PrototypeOptions.tsx; `steps` clicks through the one
// interaction from the brief and asserts the NEW thing is visible.
const OPTIONS = [
  {
    id: "current",
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

const watch = (page) => {
  page.setDefaultTimeout(60_000);
  page.on("pageerror", (e) => errors.push(String(e)));
};

try {
  await context.addInitScript((flags) => {
    for (const flag of flags) {
      const [key, value] = flag.split("=");
      try {
        localStorage.setItem(key, value ?? "true");
      } catch (error) {
        console.warn(`flag ${key} not set: storage blocked`, error);
      }
    }
  }, CONFIG.flags);

  const login = await context.newPage();
  watch(login);
  await CONFIG.login.steps(login);
  await CONFIG.ready(login).waitFor(); // sign-in worked only if the app shows the real screen
  await login.close();

  for (const option of OPTIONS) {
    // A fresh tab per option. Its init script clears the stored choice once, before the
    // first load, so the screen starts on `current` and never on the previous option.
    // The sessionStorage mark stops the later reload from clearing the choice again.
    const page = await context.newPage();
    watch(page);
    await page.addInitScript((key) => {
      try {
        if (!sessionStorage.getItem("probe-cleared")) {
          localStorage.removeItem(key);
          sessionStorage.setItem("probe-cleared", "1");
        }
      } catch (error) {
        console.warn("stored option not cleared: storage blocked", error);
      }
    }, CONFIG.storageKey);

    await page.goto(CONFIG.route);
    await CONFIG.ready(page).waitFor();
    if (BASELINE) {
      // Baseline: the switcher does not exist yet. Screenshot the real screen only.
      await page.evaluate(() => document.fonts.ready);
      await page.mouse.move(5, 5);
      await page.waitForTimeout(CONFIG.settleMs);
      const shot = `${OUT}/shots/current-baseline.png`;
      await page.screenshot({ path: shot });
      results.push({ option: "current-baseline", shot });
      console.log(`ok - baseline -> ${shot}`);
      await page.close();
      break;
    }
    // Select through the switcher the user will use, then reload: the choice must be
    // read back from localStorage (proves persistence too).
    const pill = page.getByTestId(`prototype-option-${option.id}`);
    await pill.click();
    await page.reload();
    await CONFIG.ready(page).waitFor();
    if ((await pill.getAttribute("aria-pressed")) !== "true")
      throw new Error(`${option.id} was not restored from localStorage after reload`);
    await page.evaluate(() => document.fonts.ready);
    await option.steps(page);
    await page.mouse.move(5, 5); // keep the pointer off the subject
    // Let transitions, animations and late rows finish. A shot taken right after the
    // switch can show the previous option's highlight while the DOM is already right.
    await page.waitForTimeout(CONFIG.settleMs);
    const shot = `${OUT}/shots/${option.id}.png`;
    await page.screenshot({ path: shot });
    results.push({ option: option.id, shot });
    console.log(`ok - ${option.id} -> ${shot}`);
    await page.close();
  }
} catch (error) {
  const open = context.pages();
  await open[open.length - 1]
    ?.screenshot({ path: `${OUT}/shots/failure.png` })
    .catch((shotError) => console.error("no failure screenshot:", shotError));
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
- `shots/current-baseline.png` (from `--baseline`) and `shots/current.png` must look like the real screen. Compare it with a screenshot of the real app without the prototype. `shots/failure.png` appears only when a step failed; read it first.
- Compare each option's shot with its description in the brief and say what you saw.
- Add per-option assertions that the NEW element is visible (`waitFor`) before any "is gone" assertion.
