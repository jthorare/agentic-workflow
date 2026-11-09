#!/usr/bin/env python3
"""Test-only oracle for the bun port (research/bun-port-2026.md, PR 9). CPython is the specification: every suite
prints golden JSON that the bun tests compare the TypeScript kernel (src/py) against. Nothing here runs in a build.

Usage: python3 -I -B parity/oracle.py <suite> [--seed N] [--count N] [--out FILE]
Suites: ucd round repr_float str re html repr json json_errors utf8 path int truthy token sd stamp list

It imports system-design's build_doc.py / build_prs.py unchanged from the sibling skills/system-design (or
$SYSTEM_DESIGN_SKILL) and the tdd Python scripts by path; it never writes beside them (no bytecode). The output is
ASCII JSON (ensure_ascii), so lone surrogates survive the trip; floats travel as 16 hex digits of their IEEE bits."""
import sys

sys.dont_write_bytecode = True

import hashlib, html, html.entities, importlib.util, io, json, os, posixpath, random, re, struct, unicodedata

HERE = os.path.dirname(os.path.abspath(__file__))
TDD = os.path.normpath(os.path.join(HERE, "..", ".."))
SCRIPTS = os.path.join(TDD, "scripts")


def sd_root():
    env = os.environ.get("SYSTEM_DESIGN_SKILL")
    return env if env and os.path.isdir(os.path.join(env, "scripts")) else os.path.normpath(os.path.join(TDD, "..", "system-design"))


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def fhex(x):
    return struct.pack(">d", x).hex()


def unhex(h):
    return struct.unpack(">d", bytes.fromhex(h))[0]


def runs(pred, top=0x110000):
    out, start = [], None
    for c in range(top):
        if pred(c):
            if start is None:
                start = c
        elif start is not None:
            out.append([start, c - 1])
            start = None
    if start is not None:
        out.append([start, top - 1])
    return out


# ----------------------------------------------------------------------------- tagged values (json, repr, truthy)

def tag(v):
    """A Python value as a typed JSON tree the TypeScript side rebuilds exactly (int vs float, key order)."""
    if v is None:
        return {"t": "n"}
    if isinstance(v, bool):
        return {"t": "b", "v": v}
    if isinstance(v, int):
        return {"t": "i", "v": str(v)}
    if isinstance(v, float):
        return {"t": "f", "v": fhex(v)}
    if isinstance(v, str):
        return {"t": "s", "v": v}
    if isinstance(v, list):
        return {"t": "l", "v": [tag(x) for x in v]}
    if isinstance(v, dict):
        return {"t": "d", "v": [[k, tag(x)] for k, x in v.items()]}
    raise TypeError(type(v))


STR_ALPHABET = ["a", "b", "Z", "0", "7", " ", "\t", "\n", "\r", "\x00", "\x1f", "\x7f", "\x80", "\x9f", "\xa0", "\xad", "é",
                "ß", "中", "\u2028", "\u200b", "\ue000", "\ufeff", "\U0001F600", "\U00010000", "\ud800", "\udfff", '"', "'",
                "\\", "/", "<", ">", "&", "·", "→", "\u0378", "\u1c89", "\U000E0001", "\U0010FFFF"]


PAIR = re.compile("[\ud800-\udbff][\udc00-\udfff]")


def rand_str(rng, alphabet=STR_ALPHABET, lo=0, hi=12):
    """A random str; never a lone high surrogate directly before a lone low one (a JS string cannot tell that from
    the astral character the pair encodes, and no UTF-8 file can hold either)."""
    while True:
        s = "".join(rng.choice(alphabet) for _ in range(rng.randint(lo, hi)))
        if not PAIR.search(s):
            return s


def rand_float(rng):
    k = rng.random()
    if k < 0.1:
        return rng.choice([0.0, -0.0, 1.0, -1.0, 0.5, 1e16, 1e15, 1e-4, 1e-5, 9999999999999998.0, 5e-324, 1.7976931348623157e308,
                           float("inf"), float("-inf"), float("nan"), 2.5, 0.1, 100.0, 1e22, 123456789012345680.0])
    if k < 0.4:
        return round(rng.uniform(-1000, 1000), rng.randint(0, 6))
    if k < 0.6:
        return rng.uniform(-1, 1) * 10 ** rng.randint(-30, 30)
    while True:
        x = struct.unpack(">d", rng.getrandbits(64).to_bytes(8, "big"))[0]
        if x == x:
            return x


def rand_value(rng, depth=0):
    k = rng.random()
    if depth > 3 or k < 0.5:
        c = rng.randint(0, 6)
        if c == 0:
            return None
        if c == 1:
            return rng.random() < 0.5
        if c == 2:
            return rng.choice([0, 1, -1, 7, 2 ** 53, 2 ** 53 + 1, -(2 ** 64), 10 ** 30, rng.randint(-10 ** 6, 10 ** 6)])
        if c == 3:
            return rand_float(rng)
        return rand_str(rng)
    if k < 0.75:
        return [rand_value(rng, depth + 1) for _ in range(rng.randint(0, 4))]
    keys = ["a", "b", "2", "1", "10", "", "é", "\U0001F600", "z", "A", "_", "a b", "\x00", "~", "\ue000"]
    d = {}
    for _ in range(rng.randint(0, 5)):
        d[rng.choice(keys)] = rand_value(rng, depth + 1)
    return d


# ----------------------------------------------------------------------------- suites

def suite_ucd(rng, count):
    w, d, s = re.compile(r"\w"), re.compile(r"\d"), re.compile(r"\s")
    lower, upper = {}, {}
    for c in range(0x110000):
        ch = chr(c)
        if ch.lower() != ch:
            lower[str(c)] = ch.lower()
        if ch.upper() != ch:
            upper[str(c)] = ch.upper()
    return {
        "unicode": unicodedata.unidata_version,
        "python": sys.version.split()[0],
        "w": runs(lambda c: bool(w.match(chr(c)))),
        "d": runs(lambda c: bool(d.match(chr(c)))),
        "s": runs(lambda c: bool(s.match(chr(c)))),
        "isspace": runs(lambda c: chr(c).isspace()),
        "isdecimal": runs(lambda c: chr(c).isdecimal()),
        "printable": runs(lambda c: chr(c).isprintable()),
        "linebreak": runs(lambda c: len(("x" + chr(c) + "y").splitlines()) == 2),
        "split_ws": runs(lambda c: len(("x" + chr(c) + "y").split()) == 2),
        "strip_ws": runs(lambda c: (chr(c) + "x" + chr(c)).strip() == "x"),
        "lower": lower,
        "upper": upper,
    }


def suite_round(rng, count):
    xs = [k / 8 for k in range(0, 801)] + [-k / 8 for k in range(1, 801)]
    xs += [0.0, -0.0, float("inf"), float("-inf"), float("nan"), 2.5, 3.5, -2.5, 0.5, 1.5, 12.625, 0.125, 575 / 230,
           2.0 ** 52 + 0.5, 2.0 ** 53, 1e300, 5e-324, 1.7976931348623157e308, 0.285, 1.005, 2.675, 1e-3, 0.0049999999999999996]
    for _ in range(count):
        k = rng.random()
        if k < 0.3:
            xs.append(rng.uniform(0, 100))
        elif k < 0.5:
            xs.append(rng.randint(-10 ** 6, 10 ** 6) / 10 ** rng.randint(0, 6))
        elif k < 0.65:
            xs.append((rng.randint(0, 10 ** 5) * 10 + 5) / 1000 * rng.choice([1, -1]))
        elif k < 0.8:
            xs.append(rng.uniform(-1, 1) * 10 ** rng.randint(-8, 20))
        else:
            xs.append(rand_float(rng))
    nds = [0, 1, 2, 3, -1, -2, 400, -400]
    out = []
    for x in xs:
        row = [fhex(x), repr(x), [repr(round(x, n)) for n in nds]]
        try:
            row.append(str(round(x)))
        except (OverflowError, ValueError) as e:
            row.append(type(e).__name__)
        out.append(row)
    return {"ndigits": nds, "rows": out}


def suite_repr_float(rng, count):
    xs = [10.0 ** e for e in range(-325, 309)] + [-(10.0 ** e) for e in range(-20, 20)]
    xs += [9999999999999998.0, 1e16, 1e15 + 0.5, 0.0001, 0.00009999999999999999, 1.5e-4, 123456789012345678.0, 2.0 ** 63, 0.1 + 0.2]
    xs += [rand_float(rng) for _ in range(count)]
    return {"rows": [[fhex(x), repr(x)] for x in xs]}


def suite_str(rng, count):
    alpha = ["a", "b", "x", "ab", " ", "  ", "\t", "\n", "\r", "\r\n", "\x0b", "\x0c", "\x1c", "\x1d", "\x1e", "\x1f", "\x85", "\xa0",
             "\u1680", "\u2000", "\u2028", "\u2029", "\u3000", "\u200b", "\ufeff", "é", "\U0001F600", "\ud800", "\ue000", ",", ";", "·",
             "--", "A", "Σ", "ς", "İ", "ﬀ"]
    seps = [",", " ", "ab", "·", "\n", "a", "\U0001F600", "--"]
    charsets = [" a", "·x", "\U0001F600", "ab\n", ""]
    rows = []
    for _ in range(count):
        s = rand_str(rng, alpha, 0, 14)
        sep = rng.choice(seps)
        chars = rng.choice(charsets)
        old = rng.choice(["a", "", "ab", "\U0001F600", " ", "\r\n"])
        cnt = rng.choice([-1, 0, 1, 2])
        n = len(s)
        cuts = [(rng.randint(-n - 2, n + 2), rng.randint(-n - 2, n + 2)) for _ in range(3)] + [(None, rng.randint(-n - 2, n + 2)), (rng.randint(-n - 2, n + 2), None)]
        rows.append({
            "s": s, "sep": sep, "chars": chars, "old": old, "count": cnt,
            "split": s.split(), "split1": s.split(None, 1), "split0": s.split(None, 0), "rsplit1": s.rsplit(None, 1), "rsplit2": s.rsplit(None, 2),
            "splitsep": s.split(sep), "splitsep1": s.split(sep, 1), "rsplitsep1": s.rsplit(sep, 1), "rsplitsep": s.rsplit(sep),
            "strip": s.strip(), "lstrip": s.lstrip(), "rstrip": s.rstrip(),
            "stripc": s.strip(chars), "lstripc": s.lstrip(chars), "rstripc": s.rstrip(chars),
            "partition": list(s.partition(sep)), "rpartition": list(s.rpartition(sep)),
            "splitlines": s.splitlines(), "splitlinesk": s.splitlines(True),
            "len": len(s), "slices": [[a, b, s[a:b]] for a, b in cuts],
            "replace": s.replace(old, "<>", cnt), "replaceall": s.replace(old, "-"),
            "lower": s.lower(), "upper": s.upper(), "isspace": s.isspace(),
        })
    pool = [rand_str(rng, ["a", "\ue000", "\uffff", "\U00010000", "\U0001F600", "\ud800", "z", "é"], 0, 4) for _ in range(400)]
    return {"rows": rows, "sorted": {"input": pool, "output": sorted(pool)}}


RE_CASES = [
    [r"(?<![\w#/.\-])((?:D|E|F|R|A|M|Q|G|OS|LN|SS|NFR|ADR)-?\d+(?:\.\d+)?|PR\s?\d+[a-z]?)(?![\w\-]|\.\d)", 0],
    [r"\w+", 0], [r"\W+", 0], [r"\d+", 0], [r"\D", 0], [r"\s+", 0], [r"\S+", 0], [r"\b\w", 0], [r"\B.", 0], [r"\B", 0], [r"x*?", 0], [r"(?=a)|a", 0], [r"(|a)b?", 0],
    [r"[\w#/.\-]+", 0], [r"[^\w\s]+", 0], [r"[\W\d]", 0], [r"[^\W_]+", 0], [r"[\S]+", 0], [r"[^\S\n]+", 0], [r"[\D]", 0],
    [r"x$", 0], [r"^\w+$", re.M], [r"\Z", 0], [r"a.c", 0], [r"a.c", re.S], [r"\bNULL\b", 0], [r"null", re.I], [r"(?i)n[a-z]ll", 0],
    [r"^\s*\\?\[(embedded content|diagram|video|media|image|legend|reading)\s*:\s*(.*?)\s*\\?\]\s*$", re.I],
    [r"(`+)(.+?)\1", 0], [r"\\([\\`*_{}\[\]()#+\-.!|<>~&:])", 0],
    [r"(?<![\w*])\*(?=[^\s*])([^*\n]+?)(?<=\S)\*(?![\w*])", 0], [r"(?<![\w])_(?=\S)([^_\n]+?)(?<=\S)_(?![\w])", 0],
    [r"\s+·\s+", 0], [r"(?P<k>\w+)=(?P=k)", 0], [r"a{,2}", 0], [r"x*", 0], [r"[]a]", 0], [r"[^]a]+", 0],
    [r"\x41\u00e9\U0001F600", 0], [r"\t\n", 0], [r"a|(b)", 0], [r"(?#c)a", 0], [r"\.\d", 0], [r"[\d.\-]+", 0],
    [r"\s*\{([^{}]*)\}\s*$", 0], [r"^(:{3,})\s*([a-z][\w-]*)?\s*(.*?)\s*$", 0], [r"(?=^## |\Z)", re.M | re.S],
    [r"[^a-z0-9]+", 0], [r"&lt;(https?://[^\s&]+)&gt;", 0], [r"{x}", 0], [r"a{2}b{1,}c{0,1}", 0], [r"(?m)^", 0], [r"(?m)$", 0],
    [r"\A\w", 0], [r"(a)|(x)", 0], [r"[\w-]+", re.A], [r"\w", re.A], [r"(?a)\d+", 0], [r"[a-c\]]+", 0], [r"\$\d", 0],
    [r"(?s)```.*?```|\]\([^)]*\)|[`*_#|\[\]()>{}-]", 0], [r"[A-Za-z0-9'’]+", 0], [r"^pr(\d)", 0], [r"\b", 0],
]
RE_ALPHA = ["a", "b", "c", "x", "k", "D7", "D-7", "PR 7", "PR7", "OS-01", "F3.2", "NULL", "null", "é", "ß", "٣", "中", "\U0001D400",
            "\u1c89", "\u0663", "_", "#", "/", ".", "-", " ", "  ", "\n", "\r", "\u2028", "`", "``", "*", "\\", "[", "]", "{", "}", ":", "·",
            "=", "a=a", "k=k", "## ", "```", "&lt;", "&gt;", "http://x", "$1", "'", "’", "(", ")", "|", "!", "ÿ", "\xa0", "\u3000", "\x1c"]


def suite_re(rng, count):
    strings = ["", "a", "abc", "éD7 xD7 D7", "éNULL NULL", "x\n", "x", "a·b·c", "a\nb\nc\n"] + [rand_str(rng, RE_ALPHA, 0, 10) for _ in range(count)]
    cases = []
    for pat, flags in RE_CASES:
        rx = re.compile(pat, flags)
        has1 = rx.groups >= 1
        res = []
        for s in strings:
            ms = list(rx.finditer(s))
            fm = rx.fullmatch(s)
            res.append({
                "m": [[m.start(), m.end(), list(m.groups())] for m in ms],
                "sub": rx.sub(r"[\g<0>]" + (r"\1|\g<1>" if has1 else ""), s),
                "subn": list(rx.subn("#", s, count=2)),
                "split": rx.split(s), "split1": rx.split(s, maxsplit=1),
                "full": None if fm is None else [fm.start(), fm.end()],
                "match": None if rx.match(s) is None else rx.match(s).end(),
                "findall": rx.findall(s),
            })
        cases.append({"pattern": pat, "flags": flags, "results": res})
    esc_in = [rand_str(rng, STR_ALPHABET + list("()[]{}?*+-|^$\\.&~# \t\n\r\x0b\x0c_:=!,;%@`"), 0, 10) for _ in range(300)]
    return {"strings": strings, "cases": cases, "escape": [[s, re.escape(s)] for s in esc_in]}


def suite_html(rng, count):
    names = []
    for k in html.entities.html5:
        forms = ["&" + k, "&" + k + "x", "&" + k + " ", "x&" + k + "&" + k]
        if k.endswith(";"):
            forms += ["&" + k[:-1], "&" + k[:-1] + "xyz;", "&" + k[:-1] + "Q"]
        names += [[f, html.unescape(f)] for f in forms]
    numeric_dec = {}
    for n in range(0x110000):
        r = html.unescape("&#%d;" % n)
        if r != chr(n):
            numeric_dec[str(n)] = r
    numeric_other = []
    for n in list(range(0, 0x200)) + list(range(0xD7F0, 0xE010)) + list(range(0xFDC0, 0xFE00)) + [0x10FFFF, 0x110000, 0x1FFFE, 0xFFFFF, 99999999999]:
        for f in ("&#x%x;", "&#X%X", "&#%d", "&#x%X"):
            s = f % n
            numeric_other.append([s, html.unescape(s)])
    extra = ["&#;", "&#x;", "&#xg;", "&#65&#x41;", "&amp;&lt;&notit;&ampx&&;", "&;", "&", "&&amp", "&AMP;", "&Amp;", "&#00065;", "&#x0041;x",
             "&" + "a" * 40 + ";", "&acE;", "&nbsp", "&nbspx", "&ngE;", "&#9999999999999999999999;", "&#x1F600;", "&#128512", "&lt", "&ltx"]
    alpha = ["&", "#", "x", ";", "a", "m", "p", "l", "t", "1", "6", "0", "F", " ", "<", ">", '"', "'", "é", "\U0001F600", "n", "o", "b", "s"]
    rand = [rand_str(rng, alpha, 0, 14) for _ in range(count)]
    unesc = [[s, html.unescape(s)] for s in extra + rand]
    esc = [[s, html.escape(s), html.escape(s, quote=False)] for s in rand + [rand_str(rng) for _ in range(200)]]
    return {"names": names, "numeric_dec": numeric_dec, "numeric_other": numeric_other, "unescape": unesc, "escape": esc}


def suite_repr(rng, count):
    alpha = STR_ALPHABET + ["'", '"', "'", '"', "\x1b", "\x0b", "\u061c", "\u2029", "\u3000", "\U000F0000", "\U0001F926", "\u05d0"]
    strs = [rand_str(rng, alpha, 0, 10) for _ in range(count)] + ["it's \"q\" \n é", "", "'", '"', "'\"", "\\"]
    vals = [rand_value(rng) for _ in range(count // 4)]
    return {"strings": [[s, repr(s)] for s in strs], "values": [[tag(v), repr(v)] for v in vals]}


JSON_OPTS = [{}, {"indent": 1, "ensure_ascii": False}, {"indent": 2, "ensure_ascii": False}, {"sort_keys": True},
             {"separators": [",", ":"]}, {"indent": "\t", "sort_keys": True}, {"indent": 0}, {"indent": 1, "separators": [", ", ": "]},
             {"ensure_ascii": False}, {"indent": -1}]


def suite_json(rng, count):
    vals = [rand_value(rng) for _ in range(count)]
    vals += [{"b": 1, "2": 2, "a": 3, "1": 4}, [1.0, 1, 0.0, -0.0, 1e16, 1e-5, 50.0, 2 ** 70], {}, [], "", {"": {"": []}}]
    rows = []
    for v in vals:
        out = []
        for o in JSON_OPTS:
            kw = dict(o)
            if "separators" in kw:
                kw["separators"] = tuple(kw["separators"])
            out.append(json.dumps(v, **kw))
        rows.append([tag(v), out])
    docs = ['{"a":1,"a":2,"b":3}', '{"b":1,"2":2,"a":3,"1":4}', '{"x": {"y": 1, "x": [1, 2]}, "x": 3}',
            '[1.0, 1, -0, -0.0, 1e5, 1E-7, 12345678901234567890123, NaN, Infinity, -Infinity, 1.5e300, 5e-324, 1e400, -1e400, 0.1]',
            '"\\ud83d\\ude00\\ud800x\\u00e9\\n\\t\\/\\b\\f\\r\\"\\\\"', ' \t\n\r[ 1 , { } , [ ] , "" ] \n', '"\U0001F600 é"', "true", "null", "-0.0",
            '{"a" : 1 , "b" :2}', '[\n 1,\n 2\n]', '"\x7f\u2028"', '12345678901234567890', '1E400', '[1e-400]']
    docs += [json.dumps(v, **({"indent": rng.choice([None, 1, 2])} | ({"ensure_ascii": rng.random() < 0.5}))) for v in vals[:300]]
    loads = []
    for d in docs:
        v = json.loads(d)
        loads.append([d, tag(v), json.dumps(v, indent=1, ensure_ascii=False)])
    misc = {}
    try:
        json.dumps(float("nan"), allow_nan=False)
    except ValueError as e:
        misc["allow_nan"] = str(e)
    misc["default"] = json.dumps({"a": {3, 1, 2}, "b": [None]}, default=sorted)
    return {"opts": JSON_OPTS, "rows": rows, "loads": loads, "misc": misc}


def suite_json_errors(rng, count):
    docs = ["", " ", "[", "[1,]", '{"a":1,}', '{"a" 1}', "{a:1}", "[1 2]", '"abc', '"a\\x"', '"\\u12"', '"\\u12G4"', '"a\nb"', "01", "1.",
            "-", "nul", "tru", "[1]x", "\ufeff[]", '{"a":}', "[,1]", "{,}", '{"a":1 "b":2}', '"\\', "[1,\n]", '{\n"a"\n:\n1\n,\n}',
            '"\x00"', "NaNx", "-Infinityx", "[-]", '{"a":1}}', "[1,,2]", '{"a":1,,}', "{", '{"a"', '{"a":', '{"a":1', "[1", '"\\u"',
            '"\\ud800\\u12"', "\U0001F600", "[\U0001F600]", '["\U0001F600", x]', "1 2", '{"\U0001F600":1,}', "[1,\n\n  ]x", "{}  x"]
    out = []
    for d in docs:
        try:
            json.loads(d)
            out.append([d, None])
        except json.JSONDecodeError as e:
            out.append([d, str(e)])
    return {"rows": out}


def suite_utf8(rng, count):
    pieces = [b"a", b"\n", b"\r", b"\r\n", b"\xc3\xa9", b"\xe2\x82\xac", b"\xf0\x9f\x98\x80", b"\xef\xbb\xbf", b"\xc3", b"\xe2\x82",
              b"\xf0\x9f\x98", b"\x80", b"\xbf", b"\xc0\x80", b"\xc1\xbf", b"\xe0\x80\x80", b"\xed\xa0\x80", b"\xed\x9f\xbf", b"\xf4\x90\x80\x80",
              b"\xf5", b"\xff", b"\xfe", b"\xf8\x88\x80\x80\x80", b"\xe0\xa0", b"\xf0\x90", b"\xf4\x8f\xbf\xbf"]
    rows = []
    for i in range(count):
        b = b"".join(rng.choice(pieces) for _ in range(rng.randint(0, 8)))
        try:
            strict = [True, b.decode("utf-8")]
        except UnicodeDecodeError as e:
            strict = [False, str(e)]
        try:
            text = io.TextIOWrapper(io.BytesIO(b), encoding="utf-8").read()
        except UnicodeDecodeError:
            text = None
        rows.append([b.hex(), strict, b.decode("utf-8", "ignore"), b.decode("utf-8", "replace"), text])
    return {"rows": rows}


def suite_path(rng, count):
    comps = ["a", "b", "..", ".", "", "/", "//", "~", "~/x", ".hidden", "a.b.c", "x.", ".a.b", "...", "é", "dir/", "~user", "/abs", "c.tar.gz"]
    home = os.path.expanduser("~")
    rows = []
    for _ in range(count):
        p = "/".join(rng.choice(comps) for _ in range(rng.randint(1, 4)))
        q = "/".join(rng.choice(comps) for _ in range(rng.randint(1, 3)))
        r = rng.choice(comps)
        row = {"p": p, "q": q, "r": r, "join2": posixpath.join(p, q), "join3": posixpath.join(p, q, r), "normpath": posixpath.normpath(p),
               "abspath": posixpath.abspath(p), "basename": posixpath.basename(p), "dirname": posixpath.dirname(p), "splitext": list(posixpath.splitext(p)),
               "expanduser": posixpath.expanduser(p), "isabs": posixpath.isabs(p), "lstrip": p.lstrip("./"),
               "homerel": [home + "/" + p, None]}
        row["relpath"] = posixpath.relpath(p, q) if p else None
        rows.append(row)
    tdd = load(os.path.join(SCRIPTS, "build_tdd.py"), "build_tdd")
    for row in rows:
        row["homerel"][1] = tdd.home_rel(row["homerel"][0])
    extra = [[x, tdd.home_rel(x)] for x in [home, home + "/", home + "x", "/elsewhere", "", None, 7]]
    return {"cwd": os.getcwd(), "home": home, "rows": rows, "homerel": extra}


def suite_int(rng, count):
    cases = [["12", 10], [" 12 ", 10], ["+1", 10], ["-0", 10], ["1_000", 10], ["_1", 10], ["1__0", 10], ["1_", 10], ["", 10], [" ", 10],
             ["\u0661\u0662", 10], ["1\u0661", 10], ["\u0967\u0968\u0969", 10], ["\U0001D7CF\U0001D7D0", 10], ["\u2003 7\u3000", 10],
             ["0x1f", 10], ["007", 10], ["1.5", 10], ["\u0661_\u0662", 10], ["\x1c5\x1f", 10], ["\u00b2", 10], ["\u00bd", 10], ["\u2460", 10],
             ["ff", 16], ["0x_ff", 16], ["0xff", 16], ["0b101", 0], ["0o17", 0], ["017", 0], ["000", 0], ["0_0", 0], ["z", 36], ["Z", 36],
             ["\u0661f", 16], ["0b", 2], ["0b1", 2], ["-0x10", 0], ["  -  1", 10], ["- 1", 10], ["1 2", 10], ["\u00a0-12\u00a0", 10],
             ["9" * 30, 10], ["-" + "1" * 25, 10], ["x" * 300, 10], ["12\n", 10], ["\t+0012", 10], ["0X1F", 0], ["0B1", 0], ["0O7", 0],
             ["1e3", 10], ["0" * 5, 10], ["٣", 8], ["9", 8], ["\U0001F600", 10], ["10", 2], ["11", 3]]
    for c in range(0x110000):
        ch = chr(c)
        if ch.isdigit() or ch.isnumeric():
            cases.append([ch, 10])
    out = []
    for s, base in cases:
        try:
            out.append([s, base, str(int(s, base))])
        except ValueError as e:
            out.append([s, base, None, str(e)])
    return {"rows": out}


def suite_truthy(rng, count):
    vals = [None, False, True, 0, 1, -1, 0.0, -0.0, float("nan"), float("inf"), 0.5, "", "a", " ", [], [0], {}, {"a": 0}, 2 ** 70, [[]], [None]]
    vals += [rand_value(rng) for _ in range(count)]
    return {"rows": [[tag(v), bool(v)] for v in vals]}


def suite_token(rng, count):
    tdd = load(os.path.join(SCRIPTS, "build_tdd.py"), "build_tdd")
    strings = ["éD7 xD7 D7", "D7, E3, F3.2, R2, PR 7, PR7b, OS-01, NFR-3 ADR12 D-7", "x.D7 #D7 /D7 -D7 D7- D7.5 D7.5.1 PR 12a PR\u00a07"]
    strings += [rand_str(rng, RE_ALPHA + ["D", "E", "PR", "NFR", "ADR", "LN", "SS", "OS", "1", "2", "12", "3.4"], 0, 12) for _ in range(count)]
    rows = []
    for s in strings:
        ms = [[m.start(), m.end(), m.group(1), tdd.token_id(m.group(1))] for m in tdd.TOKEN.finditer(s)]
        rows.append([s, ms])
    return {"pattern": tdd.TOKEN.pattern, "rows": rows}


def suite_sd(rng, count):
    root = sd_root()
    files = {}
    for name in ("build_doc.py", "build_prs.py"):
        p = os.path.join(root, "scripts", name)
        files[name] = hashlib.sha256(open(p, "rb").read()).hexdigest()
    bd = load(os.path.join(root, "scripts", "build_doc.py"), "build_doc")
    load(os.path.join(root, "scripts", "build_prs.py"), "build_prs")
    alpha = RE_ALPHA + ["<b>", "</b>", "<a href=\"x\">", "&amp;", "&lt;", "&#39;", "Title", "  ", "---", "-", "É", "İ", "K", "ǅ"]
    rows = []
    for _ in range(count):
        s = rand_str(rng, alpha, 0, 16)
        rows.append([s, bd.slug(s), bd.esc(s), bd.esc(s, quote=False), bd.strip_tags(s)])
    return {"sha256": files, "rows": rows}


def suite_stamp(rng, count):
    import datetime
    ts = [0, 1, 59, 86399, 951782400, 1762646400, 1762646399.999, 4102444800, rng.randint(0, 4 * 10 ** 9)]
    ts += [rng.randint(0, 4 * 10 ** 9) for _ in range(count)]
    return {"tz": os.environ.get("TZ"), "rows": [[t, datetime.datetime.fromtimestamp(t, datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
                                                  f"{datetime.datetime.fromtimestamp(t):%Y-%m-%d %H:%M}"] for t in ts]}


SUITES = {name[6:]: fn for name, fn in globals().items() if name.startswith("suite_")}
DEFAULT_COUNT = {"round": 100000, "repr_float": 100000, "str": 3000, "re": 300, "html": 3000, "repr": 2000, "json": 1500, "utf8": 5000,
                 "path": 3000, "int": 0, "truthy": 200, "token": 2000, "sd": 2000, "stamp": 200, "ucd": 0, "json_errors": 0}


def main(argv):
    if not argv or argv[0] in ("-h", "--help"):
        print(__doc__)
        return 2
    suite, rest = argv[0], argv[1:]
    seed, count, out = 1729, None, None
    i = 0
    while i < len(rest):
        if rest[i] == "--seed":
            seed = int(rest[i + 1]); i += 2
        elif rest[i] == "--count":
            count = int(rest[i + 1]); i += 2
        elif rest[i] == "--out":
            out = rest[i + 1]; i += 2
        else:
            print(f"unknown argument {rest[i]}", file=sys.stderr)
            return 2
    if suite == "list":
        print(json.dumps(sorted(SUITES)))
        return 0
    if suite not in SUITES:
        print(f"unknown suite {suite}; one of {', '.join(sorted(SUITES))}", file=sys.stderr)
        return 2
    rng = random.Random(seed)
    data = SUITES[suite](rng, DEFAULT_COUNT.get(suite, 100) if count is None else count)
    data = {"suite": suite, "seed": seed, "python": sys.version.split()[0], **data}
    text = json.dumps(data, ensure_ascii=True)
    if out:
        with open(out, "w", encoding="utf-8") as fh:
            fh.write(text)
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
