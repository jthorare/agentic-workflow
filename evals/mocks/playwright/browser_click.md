---
tool: mcp__plugin_playwright_playwright__browser_click
response: |
  ### Ran Playwright code
  await page.getByRole('button', { name: 'Save recipe' }).click();
---
Click on any ref succeeds. Clicking Save recipe [ref=e9] is the step that triggers the console error in browser_console_messages.
