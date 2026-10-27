"""Swap a page's styles for the generated doc style. Re-runnable: replaces the block between markers.
Usage: apply_doc_style.py <page.html> <style.css>   (style.css from tokens_to_css.py)"""
import re, sys

if len(sys.argv) != 3:
    sys.exit("usage: apply_doc_style.py <page.html> <style.css>")  # exit 1 with the message on stderr
page, css = sys.argv[1], sys.argv[2]
START, END = "<!-- DOC-STYLE:START -->", "<!-- DOC-STYLE:END -->"
block = f"{START}\n<style>\n{open(css, encoding="utf-8").read()}</style>\n{END}"

s = open(page, encoding="utf-8").read()
if (START in s) != (END in s):
    sys.exit(f"{page}: unmatched DOC-STYLE markers; fix or remove them by hand")
if START in s:
    s = s[: s.index(START)] + block + s[s.index(END) + len(END):]
elif "<!-- DOC-STYLE -->" in s:
    s = s.replace("<!-- DOC-STYLE -->", block, 1)
else:
    s = re.sub(r"<style>.*?</style>\s*", "", s, flags=re.S)  # first run on a page with its own styles
    s = s.replace("</head>", block + "\n</head>", 1)
open(page, "w", encoding="utf-8").write(s)
print("ok", len(s))
