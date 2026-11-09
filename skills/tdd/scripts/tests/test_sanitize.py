#!/usr/bin/env python3
"""Regression test for the compiler's HTML guard (review of 2026-11-09, B2). Run: python3 -I scripts/tests/test_sanitize.py"""
import importlib.util, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("build_tdd", os.path.join(HERE, "..", "build_tdd.py"))
bt = importlib.util.module_from_spec(spec)
sys.argv = ["build_tdd.py"]
try:
    spec.loader.exec_module(bt)
except SystemExit:
    pass
S = bt.sanitize_html
VECTORS = [
    '<img src="z.png" alt="x"onerror="alert(1)">',                       # handler glued to a closing quote (alt breakout)
    '<td><img alt="x" src="z.png"onload="alert(1)"></td>',                 # in a table cell
    '<a href="https://e.x"onmouseover="alert(1)">t</a>',                   # href breakout in a PR link
    '<a href="\x01javascript:alert(1)">b</a>',                             # control char before the scheme
    '<a href="JAVA\tSCRIPT:alert(1)">b</a>',                               # tab inside the scheme
    '<a href="&#106;avascript:alert(1)">b</a>',                            # entity-encoded scheme
    '<a href="data:text/html,<script>alert(1)</script>">b</a>',            # data URL
    '<img src=x onerror=alert(1)>',                                        # unquoted attributes
    '<svg onload=alert(1)><circle r=1 /></svg>',                            # svg handler
    '<p>ok</p><script>alert(1)</script><iframe src="x"></iframe>',         # tags
    '<b style="background:url(javascript:1)">x</b>',                       # style attribute
    '<a href="vbscript:msgbox(1)">b</a>',
]
bad = re.compile(r"\bon[a-z]+\s*=|javascript:|vbscript:|data:text/html|<\s*(script|iframe|object|embed)", re.I)
fails = []
for v in VECTORS:
    out = S(v)
    low = bt.html.unescape(out).lower()
    if bad.search(low) or "\x01" in out:
        fails.append((v, out))
keep = [S('<a href="https://example.org/x?y=1#z" title="ok">t</a>'), S('<img src="mockups/a.png" alt="plain">'), S('<a href="#d7">D7</a>')]
for k in keep:
    assert "blocked" not in k and "href" in k or "src" in k, k
if fails:
    for v, o in fails:
        print("FAIL", repr(v), "->", repr(o))
    sys.exit(1)
print(f"sanitize: {len(VECTORS)} vectors neutralised, {len(keep)} benign samples kept")
