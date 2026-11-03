# Probe template

A Playwright script that proves each option works on the seeded local stack. Copy it to `$AW_DIR/prototypes/<slug>/probe.ts`, edit the `CONFIG` block and each option's `steps`, and run it from the prototype worktree with `bun` (it handles TypeScript and top-level await), as `bun /abs/path/probe.ts --out <dir>`. It resolves the project's own `playwright` package.

It uses the project's existing Playwright. If the project has none, **Ask the user** before installing anything. It signs in through the real login form (no cookie shortcuts).

```ts
import { mkdirSync, writeFileSync } from "node:fs";
import { chromium, type Page } from "playwright";

const outFlag = process.argv.indexOf("--out");
const OUT = outFlag > 0 ? (process.argv[outFlag + 1] ?? "") : ""; // $AW_DIR/prototypes/<slug>, passed as --out <dir>
if (OUT === "") throw new Error("pass --out <prototype output dir>");

// ---- CONFIG ---------------------------------------------------------------
const CONFIG = {
  baseUrl: "http://localhost:<port>", // from the brief's Stack section
  route: "<full URL of the screen>", // the seeded route; re-read it after every re-seed
  storageKey: "prototype-option:<slug>", // STORAGE_KEY in PrototypeOptions.tsx
  login: {
    // The real sign-in form. Replace the selectors and read credentials from the
    // project's seed output or env; never hardcode a real password here.
    email: process.env.PROTO_EMAIL ?? "",
    password: process.env.PROTO_PASSWORD ?? "",
    steps: async (page: Page) => {
      await page.goto(`${CONFIG.baseUrl}/login`);
      await page.getByLabel("Email").fill(CONFIG.login.email);
      await page.getByLabel("Password").fill(CONFIG.login.password);
      await page.getByRole("button", { name: /sign in/i }).click();
    },
  },
  flags: [] as string[], // client-side flags as "storage-key=value"
  // The element that proves the REAL screen has rendered with data. It must not be
  // inside anything an option replaces, or the wait hangs for that option.
  ready: (page: Page) => page.getByTestId("<real-screen-testid>"),
  viewport: { width: 1600, height: 1000 },
  settleMs: 1500,
};

type Option = {
  id: string; // matches OPTIONS[].id in PrototypeOptions.tsx
  // Click through the one interaction from the brief. Assert the NEW thing is visible.
  steps: (page: Page) => Promise<void>;
};

// One entry per option from the brief. The first is always `current`.
const OPTIONS: Option[] = [
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
const errors: string[] = [];
const results: { option: string; shot: string }[] = [];

const browser = await chromium.launch();
// Many apps gate the headless build's user agent ("HeadlessChrome") with an
// "Unsupported browser" page. Present as plain Chrome of the same version.
const userAgent = `Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${browser.version()} Safari/537.36`;
const context = await browser.newContext({ viewport: CONFIG.viewport, userAgent });

const watch = (page: Page) => {
  page.setDefaultTimeout(60_000);
  page.on("pageerror", (e: Error) => errors.push(String(e)));
};

try {
  await context.addInitScript((flags: string[]) => {
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
    await page.addInitScript((key: string) => {
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
    const pill = page.getByTestId(`prototype-option-${option.id}`);
    await pill.click();
    // The reload proves persistence: the choice must be read back from localStorage.
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
    .catch((shotError: unknown) => console.error("no failure screenshot:", shotError));
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
