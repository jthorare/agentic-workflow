"""Build the page stylesheet from the project's design-tokens.json (W3C DTCG) plus templates/doc-base.css.
Usage: tokens_to_css.py <design-tokens.json> <out.css>
Maps token names onto the semantic variables doc-base.css uses; anything it cannot map keeps the neutral fallback in
doc-base.css, and is printed so the run can ask the user (or run /design-language) instead of guessing. Exit 0 on success, 1 with a message on bad arguments or an unreadable tokens file."""
import json, os, sys

if len(sys.argv) != 3:
    sys.exit("usage: tokens_to_css.py <design-tokens.json> <out.css>")
tokens_path, out = sys.argv[1], sys.argv[2]
try:
    tokens = json.load(open(tokens_path, encoding="utf-8"))
except (OSError, ValueError) as e:
    sys.exit(f"{tokens_path}: {e}")  # exit 1: no tokens file, or not JSON; nothing is written

def flat(node, prefix=""):
    for k, v in node.items():
        if k.startswith("$"):
            continue
        name = f"{prefix}{k}"
        if isinstance(v, dict) and "$value" in v:
            yield name, v["$value"]
        elif isinstance(v, dict):
            yield from flat(v, name + ".")

def css_value(v, seen=()):
    """A CSS-ready string, or None for values a variable cannot hold (composite objects)."""
    if isinstance(v, list):  # DTCG fontFamily may be a list of families
        return ", ".join(f'"{f}"' if " " in f and not f.startswith(('"', "'")) else f for f in map(str, v))
    if isinstance(v, dict):
        return None
    v = str(v)
    if v.startswith("{") and v.endswith("}"):  # alias like {color.brand.500}
        target = v[1:-1]
        return css_value(flat_tokens[target], seen + (target,)) if target in flat_tokens and target not in seen else None
    return v

flat_tokens = dict(flat(tokens))
# semantic variable -> token names to try, first hit wins
WANT = {
    "--bg": ["color.background", "color.bg", "color.surface-muted"],
    "--surface": ["color.surface", "color.card", "color.background-elevated"],
    "--ink": ["color.text", "color.foreground", "color.ink", "color.text-primary"],
    "--muted": ["color.text-muted", "color.muted", "color.text-secondary"],
    "--line": ["color.border", "color.line", "color.divider"],
    "--accent": ["color.accent", "color.primary", "color.brand"],
    "--secondary": ["color.secondary", "color.accent-secondary"],
    "--good": ["color.success", "color.positive", "color.green"],
    "--warn": ["color.warning", "color.caution", "color.yellow"],
    "--bad": ["color.danger", "color.error", "color.destructive", "color.red"],
    "--f-body": ["font.family-body", "font.family-sans", "font.family", "typography.body"],
    "--f-mono": ["font.family-mono", "typography.mono"],
    "--radius": ["radius.md", "radius.base", "radius.default"],
}
lines, missing = [], []
for var, names in WANT.items():
    value = next((css_value(flat_tokens[n]) for n in names if n in flat_tokens and css_value(flat_tokens[n]) is not None), None)
    if value is not None:
        lines.append(f"  {var}: {value};")
    else:
        missing.append(var)
# every other color is exposed too, for diagram-adjacent callouts: --color-<name>
for n, v in flat_tokens.items():
    if n.startswith("color.") and css_value(v) is not None:
        lines.append(f"  --{n.replace('.', '-')}: {css_value(v)};")
base = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "templates", "doc-base.css"), encoding="utf-8").read()
# project tokens go last so they win over the neutral fallbacks at the top of doc-base.css
open(out, "w", encoding="utf-8").write(f"/* generated from {os.path.basename(tokens_path)} by tokens_to_css.py */\n" + base + "\n:root {\n" + "\n".join(lines) + "\n}\n")
print("wrote", out, "| unmapped (neutral fallback used):", ", ".join(missing) or "none")
