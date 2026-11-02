"""Make a page shareable: inline the style and every local image (png, jpg, svg) as data: URIs.
Usage: build_page.py <src.html> <out.html> <style.css>
The source marks the style spot with <!-- DOC-STYLE --> and references images by relative path
(<img src="diagrams/model.svg">), resolved against the source's folder. Output is one self-contained file. Exit 0 ok; exits 1 with a message on a missing image, a missing style marker or bad arguments."""
import base64, mimetypes, os, re, sys

if len(sys.argv) != 4:
    sys.exit("usage: build_page.py <src.html> <out.html> <style.css>")
src, out, css = sys.argv[1], sys.argv[2], sys.argv[3]
base = os.path.dirname(os.path.abspath(src))
html = open(src, encoding="utf-8").read()
if "<!-- DOC-STYLE -->" not in html:
    sys.exit(f"{src}: no <!-- DOC-STYLE --> marker; the page would ship unstyled")
html = html.replace("<!-- DOC-STYLE -->", f"<style>\n{open(css, encoding='utf-8').read()}</style>", 1)

def inline(m):
    ref = m.group(2)
    if ref.startswith(("data:", "http")):
        return m.group(0)
    path = os.path.join(base, ref)
    if not os.path.isfile(path):
        sys.exit(f"{src}: missing image {ref}")
    mime = mimetypes.guess_type(path)[0] or "application/octet-stream"
    return f'{m.group(1)}"data:{mime};base64,{base64.b64encode(open(path, "rb").read()).decode()}"'

html = re.sub(r'(<img[^>]*?\ssrc=)"([^"]+)"', inline, html)
open(out, "w", encoding="utf-8").write(html)
print("wrote", out, f"{len(html)/1e6:.2f} MB")
