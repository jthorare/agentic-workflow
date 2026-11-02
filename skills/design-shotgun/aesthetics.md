# Aesthetic Direction Templates — Fallback Prompts

This file provides fallback prompt templates used by `/design-shotgun` when the external `frontend-design` skill is not available. Each template is injected into a single subagent's brief to bias the generated mockup toward one distinct aesthetic.

Each subagent receives: the feature brief, the project's `design-tokens.json`, the project's `.impeccable.md`, and ONE of the templates below. The subagent must produce a single self-contained HTML file at `$AW_DIR/design/shotgun/variant-{i}.html`.

All templates respect the project's design tokens (colors, spacing scale, font families) as the source of truth. The aesthetic direction shapes *how* tokens are composed — not which values to use.

---

## minimalist

Restrained typography with a single dominant weight per hierarchy level. Generous whitespace (target 1.5–2× the base spacing scale between sections). Monochrome palette anchored by the project's `--color-surface` and `--color-text-primary`; reserve the accent color for a single primary action per screen. No decorative elements, no gradients, no shadows beyond a subtle 1px border or hairline divider. Motion is sparing — only easing-in fades on initial load (200–300ms) and hover state transitions (150ms). Typography uses generous line-height (1.6+) and tight tracking on headlines.

## brutalist

Raw monospace typography throughout (use the project's `--font-family-mono` token or fall back to system mono). Hard edges only — zero border-radius, zero shadows, zero gradients. System default fonts when mono is unavailable. Layout is grid-aligned but deliberately asymmetric, with overlapping or offset blocks creating tension. Color is utilitarian: black on white (or the project's surface/text-primary tokens), with one bold accent for emphasis. No animation beyond hard cuts on state changes. Borders are 1–2px solid; rules and dividers are heavy. Embrace exposed structure — show the grid, show alignment markers, label sections in monospace.

## editorial

Serif headlines with strong hierarchy contrast (display sizes 4–6× body text). Magazine-style column layouts where appropriate, with pull-quotes, drop caps, and asymmetric image placement. Typography is the protagonist — body copy uses a refined sans-serif at comfortable measure (60–75 characters per line). Color palette is muted and warm; use the project's tokens but bias toward lower-saturation surfaces and high-contrast text. Decorative flourishes are restrained: hairline rules, small caps for metadata, italic emphasis. Motion is slow and considered (400–600ms eases), reserved for entrances and reading-flow transitions.

## glassmorphism

Translucent surfaces with backdrop-filter blur (12–24px). Layered depth — at least 3 z-axis tiers (background, mid-ground content cards, foreground actions). Surfaces use the project's tokens at reduced opacity (typically 70–85% alpha) over a soft gradient or atmospheric backdrop. Border-radius is generous (12–20px). Subtle outer glows or 1px translucent borders define edges. Motion uses spring physics for surface entry/exit (stiffness 200, damping 25). Text remains fully opaque and high-contrast on top of translucent surfaces. Accent color appears as a soft inner glow or focus ring rather than solid fill.

## neo-skeuomorphic

Soft inset and outset shadows create tactile, button-like surfaces (typical values: 8px 8px 16px rgba(0,0,0,0.1) outer + -8px -8px 16px rgba(255,255,255,0.7) inner, scaled to the project's palette). Subtle gradients (5–10% lightness variation) suggest physical materials. Generous border-radius (16–24px) for primary surfaces, smaller (6–8px) for inline elements. Color stays close to the surface token with low-saturation tonal shifts. Buttons feel pressable — depressed states use inverted shadow direction. Motion is satisfying and tactile (200–300ms ease-out on press, slight scale-down to 0.97). Avoid skeuomorphic mimicry of real objects; focus on tactile *surface* quality.

## swiss-grid

Strict 12-column grid with mathematical precision — every element snaps to column boundaries with no exceptions. Helvetica or the project's nearest geometric sans-serif. Asymmetric balance achieved through weighted whitespace and intentional negative space; never centered without strong justification. Type hierarchy uses a clear modular scale (1.25 or 1.333 ratio). Color is functional, not decorative: project's accent for action, neutrals for content, semantic colors only for status. Information design takes precedence — labels are precise, units are explicit, alignment is sacred. Motion is minimal and informational (linear or ease-out, 150–250ms). Grid lines may be subtly visible as part of the visual language.
