# Sindri UI: type, rows, grid, press and hover

The Sindri app uses Inter on a dark theme at Linear's density. Every number below has a source. "Measured" means it was read with getComputedStyle from a public Linear or Raycast page on 2026-11-10 (`~/.agentic-workflow/design/measured-linear-raycast-2026-11-10.md`, second pass). "Proposal" means we chose it and did not measure it.

## Type scale

Tokens live in `src/Sindri/Aw.Sindri.Ui/Theme/Tokens.axaml` as `Type.<Step>.Size`, `.LineHeight` and `.Weight`. `Theme/Typography.axaml` turns each step into a `TextBlock` class. It holds no literal size, line height or weight. The text scale in Settings (0.9 to 1.3) multiplies the sizes and line heights.

| Class | Size / weight / line height | Letter spacing | Source |
|---|---|---|---|
| `display` | 40 / SemiBold / 44 | -0.8 | inspora hero number (measured, inspora north star note 2026-11-11); one per page |
| `page-title` | 24 / SemiBold / 32 | -0.24 | Linear H2 24 / 590 / 31.92 (measured, /changelog) |
| `section-title` | 15 / SemiBold / 22 | 0 | Linear H3 15 / 510 / 24 (measured, /docs) |
| `detail-title` | 20 / SemiBold / 28 | -0.24 | Linear H2 20 / 590 / 26.6 (measured, /docs) |
| `body` | 13 / Normal / 20 | -0.13 | Linear UI text 13 / 400 / 19.5 (measured, home) |
| `body-strong` | 13 / Medium / 20 | 0 | Linear 13 / 510 / 19.5 (measured) |
| `caption` | 12 / Normal / 18 | -0.24 | Linear 12 / 400 / 16 (measured, home) |
| `eyebrow` | 11 / Normal / 16 | 0.4 | Linear micro 10 to 11 px (measured); tracking and capitals are a proposal |
| `button` | 13 / Medium / 15.6 | 0 | Linear button 13 / 510 (measured); line height kept from before |
| `mono` | 12 / Normal / 18 | 0 | Raycast 12 / 300 / 16, Linear 12.25 / 400 / 15.9 (measured) |
| `kbd` | 11 / Medium / 16 | 0 | Raycast keycap 11 / 500 (measured, /store) |

Avalonia cannot draw Linear's variable weights 510 and 590. We use Medium (500) and SemiBold (600) instead.

Write a text size as a class, never as an inline `FontSize`. Use a local `FontWeight` only for a one-off such as a medium caption.

## Row heights

| Token | Value | Source |
|---|---|---|
| `Row.Height.Compact` | 28 | Linear list and nav rows (measured, /changelog, /method) |
| `Row.Height.Default` | 32 | Linear docs nav row, radius 8 (measured, /docs) |
| `Row.Height.Comfortable` | 40 | Proposal. Nearest measured rows are Raycast's 36 and 41 to 42. |

Use these as `MinHeight` on a list row. Two-line rows grow past them. A panel header is not a row: it has its own token, `Size.PanelHeader` (36). `Size.Row` and `Size.RowComfortable` are gone.

## Grid

4 px is the base unit and 8 px is the main step. Use 2, 4, 8, 12, 16, 24, 32, 48. Source: multiples of 4 cover 54 to 99 percent of spacing values on the six measured pages, and 8 is the top single value on five of them.

Use 6 only inside chips and small buttons, as in `Padding="6,0"`. Do not use 6 for a gap, a stack spacing or a margin. Do not use 7, 10, 11, 13, 14 or 15. Both sites carry them by accident.

## Pressed and hover

- Hover fills the background with one tertiary token over 150 ms with ease. Source: Linear 0.15 s and Raycast 0.15 s (measured).
- Pressed moves the element 1 px down (#3190): a `Border.clickable` and every `Button` and `ToggleButton` (the Fluent theme scales them to 0.98). A 0.98 scale (Linear 0.97, Raycast 0.98) put 1 px borders on fractional pixels and the drawn border fell below 3:1; a whole-pixel push keeps them sharp.
- Pressed does not change the colour of the label.

The `clickable` class applies hover and pressed to any element. It lives in `Theme/ClickableStyles.axaml`, added by the hover states fix (#2535). Put `Classes="clickable"` on a row or card instead of writing a trigger.

## Chips

A chip is 20 px high with padding `0,6`, radius 6 (`Radius.SM`) and the `caption` font in Medium. Its text is `Ink Muted` (#d0d6e0) on a fill of white at 5 percent, with a white border at 8 percent. Source: Linear code chip (measured, home); Raycast chips are 20 px high with radius 4 to 6.

The `status-chip` class has these variants: neutral, primary, success, attention and error. Issue #2545 owns their definitions. This page does not repeat their colours.

## Source checks

`tests/Aw.Sindri.Ui.Tests/Theme/TokenTests.cs` pins every value in the first two tables and checks that `Typography.axaml` reads the tokens.
