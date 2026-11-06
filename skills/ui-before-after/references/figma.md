# Figma panels for ui-before-after

Save frames as `<out>/pr-<n>/figma/<state>.components.png` (the Components file) and `<state>.project.png` (the Linear project's or issue's design). Compose adds each as a labelled panel after the PR, scaled to the PR crop's width.

- **Links:** components from `<side>/<state>.figma.txt` (`--storybook`); project from the issue's "ui before and after", the issue links or the project's resources. No link: "Not checked (no Figma frame)".
- **Pick the variant node** that matches the state. Story links often point at the whole component set; list its children with `mcp: figma/get_metadata` and take the variant.
- **Figma MCP connected:** `mcp: figma/get_screenshot` (fileKey, nodeId) returns a short-lived URL; `curl -f -L -o <path> "<url>"`. Don't ask for base64.
- **No Figma MCP:** `bash "$SCRIPTS/figma-frame.sh" <figma-url> <path>`. Exit 7 = no token: **Ask the user** to make one (it prints the steps), then stop the Figma rows.
