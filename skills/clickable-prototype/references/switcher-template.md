# Switcher template

A typed, dev-only option switcher. Copy the block below into the prototype worktree as `PrototypeOptions.tsx` beside the feature's prototype file. It is never added to the app on the main branch.

The worked example is React + TypeScript with Vite (`import.meta.env.DEV`). It uses only React, so it needs no state library. Swap the `<Button>` for the app's own button component (real components and tokens, hard rule 5) and the pill classes for the app's own tokens.

Edit only the three constants at the top. The first option is always `current`: the real, untouched screen, so the user can compare.

```tsx
// Local-only design prototype. Never pushed or merged.
import { useSyncExternalStore } from "react";

// Replace with the app's own button component and token classes.
import { Button } from "<app button component>";

/** Off outside the dev build: every hook and component below becomes a no-op. */
export const prototypeEnabled = import.meta.env.DEV; // webpack/Next: process.env.NODE_ENV !== "production"

const STORAGE_KEY = "prototype-option:<slug>";

/** `current` first; add one entry per option from the brief. */
export const OPTIONS = [
  { id: "current", label: "Current" },
  { id: "a", label: "A Inline in the cell" },
  { id: "b", label: "B Side panel" },
] as const;

export type PrototypeOption = (typeof OPTIONS)[number]["id"];

const isOption = (value: string | null): value is PrototypeOption =>
  OPTIONS.some((option) => option.id === value);

const readOption = (): PrototypeOption => {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    return isOption(stored) ? stored : "current";
  } catch {
    return "current";
  }
};

// One module-level store so every mount point and the pill stay in step.
let selected: PrototypeOption = readOption();
const listeners = new Set<() => void>();
const subscribe = (listener: () => void) => {
  listeners.add(listener);
  return () => listeners.delete(listener);
};
const select = (id: PrototypeOption) => {
  selected = id;
  try {
    window.localStorage.setItem(STORAGE_KEY, id);
  } catch {
    // Storage blocked: the choice lasts this session only.
  }
  listeners.forEach((listener) => listener());
};

/**
 * The selected option, or `null` when the prototype is disabled (not a dev build).
 * Mount sites treat `null` and `"current"` the same: render the real element.
 */
export function usePrototypeOption(): PrototypeOption | null {
  const option = useSyncExternalStore(subscribe, () => selected);
  return prototypeEnabled ? option : null;
}

/** Fixed pill, bottom-left. Mount once at the screen root. */
export function PrototypeOptionSwitcher() {
  const option = useSyncExternalStore(subscribe, () => selected);
  if (!prototypeEnabled) return null;
  return (
    <div
      data-testid="prototype-option-switcher"
      role="group"
      aria-label="Prototype option"
      className="fixed bottom-20 left-4 z-50 flex items-center gap-1 rounded-xl border bg-white p-1 shadow-lg"
    >
      {OPTIONS.map(({ id, label }) => (
        <Button
          key={id}
          data-testid={`prototype-option-${id}`}
          aria-pressed={option === id}
          onClick={() => select(id)}
        >
          {label}
        </Button>
      ))}
    </div>
  );
}
```

The probe relies on two things here: the `data-testid` values `prototype-option-<id>` and `aria-pressed`. Keep them if you change the markup.

## Mount pattern

At the mount point of the real element, keep the real element as the fallthrough:

```tsx
import { usePrototypeOption } from "./PrototypeOptions";
import { ActionsOptionA, ActionsOptionB } from "./ActionsPrototype";

export function <Feature>HeaderActions() {
  const option = usePrototypeOption();
  if (option === "a") return <ActionsOptionA />;
  if (option === "b") return <ActionsOptionB />;
  return <RealActions />; // "current" and not a dev build
}
```

- Put every option body in the one `<Feature>Prototype` file and export one component per option.
- When an option needs state shared between mount points (an open panel, a selected row), keep it in the same kind of module-level store, and reset it when the option changes (an effect on `usePrototypeOption()`), so switching options never leaves another option's UI open.
- A mount point that renders in many rows (a table cell, a grid row) must be cheap: call `usePrototypeOption()` once in a parent and pass it down where you can.
- Hooks rules apply: call `usePrototypeOption()` unconditionally at the top of the component.

## Other frameworks

The pattern is four parts; keep them, change the syntax:

1. A dev-only constant that is statically false in production builds (`import.meta.env.DEV`, `process.env.NODE_ENV !== "production"`, `isDevMode()`), so the prototype code tree-shakes away.
2. A `current`-first option list and the choice in `localStorage`, read once at startup, with the read and write in try/catch.
3. A tiny reactive store the framework subscribes to (Vue: a `ref` in a module; Svelte: a `writable`; Angular: a signal in a root service; Solid: a signal).
4. A small floating pill rendered once at the screen root, with `data-testid="prototype-option-<id>"` and an `aria-pressed` state on each button.
