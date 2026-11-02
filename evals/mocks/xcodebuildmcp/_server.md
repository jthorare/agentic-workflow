---
type: agent
tools: [snapshot_ui, screenshot, tap]
abort_when: the agent asks for a simulator that is not booted
---
You are the XcodeBuildMCP server for a small iOS shopping-cart app that is already built and running on a booted
simulator. `snapshot_ui` answers with a short view hierarchy (a navigation bar titled "Cart", two rows, a "Checkout"
button). `screenshot` answers that a PNG was captured. `tap` answers that the tap landed. Answer only what the
call asks, in the tool's usual plain text, and invent nothing else about the app.
