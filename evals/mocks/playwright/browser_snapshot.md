---
tool: mcp__plugin_playwright_playwright__browser_snapshot
response: |
  ### Page state
  - Page URL: http://localhost:3000/recipes
  - Page Title: Recipes - Pantry Notes
  ### Snapshot
  - heading "Recipes" [level=1] [ref=e2]
  - button "Add recipe" [ref=e3]
  - form "New recipe" [ref=e5]:
    - textbox "Title" [ref=e6]
    - textbox "Ingredient 1" [ref=e7]
    - textbox "Ingredient 2" [ref=e8]
    - button [ref=e14]
    - button "Save recipe" [ref=e9]
  - list "Saved recipes" [ref=e10]:
    - listitem [ref=e11]: Lentil soup
    - listitem [ref=e12]: Sourdough
---
Accessibility tree of /recipes after the Add recipe form is open. Planted: the icon-only button [ref=e14] has no accessible name; the list still shows 2 recipes (the new one is not rendered).
