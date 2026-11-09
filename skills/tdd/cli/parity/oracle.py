#!/usr/bin/env python3
"""Test-only oracle for the bun port (research/bun-port-2026.md, PR 9). CPython is the specification: every suite
prints golden JSON that the bun tests compare the TypeScript kernel (src/py) against. Nothing here runs in a build.

Usage: python3 -I -B parity/oracle.py <suite> [--seed N] [--count N] [--out FILE]
                                         [--packet DIR [--src FILE.md] [--copy]] [--dir DIR]
Suites: ucd round repr_float str re html repr json json_errors utf8 path int truthy token sd stamp list
        sd_calls (--packet: every call a compile makes to system-design, recorded) sd_fuzz sd_files (--dir) sd_constants
        sd_finish (--dir)

It imports system-design's build_doc.py / build_prs.py unchanged from the sibling skills/system-design (or
$SYSTEM_DESIGN_SKILL) and the tdd Python scripts by path; it never writes beside them (no bytecode). The output is
ASCII JSON (ensure_ascii), so lone surrogates survive the trip; floats travel as 16 hex digits of their IEEE bits."""
import sys

sys.dont_write_bytecode = True

import _sre, hashlib, html, html.entities, importlib.util, io, json, os, posixpath, random, re, struct, unicodedata
import re._casefix as _casefix

HERE = os.path.dirname(os.path.abspath(__file__))
TDD = os.path.normpath(os.path.join(HERE, "..", ".."))
SCRIPTS = os.path.join(TDD, "scripts")


def sd_root():
    """The system-design copy build_tdd.py itself loads (its own sd_root(): env, sibling, ~/.claude/skills, vendor)."""
    return load(os.path.join(SCRIPTS, "build_tdd.py"), "build_tdd").sd_root()


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

def sre_upper(c, _f=[]):
    """sre_upper_unicode: CPython's _PyUnicode_ToUppercase (the first code point of a multi-character mapping)."""
    if not _f:
        import ctypes
        f = ctypes.pythonapi._PyUnicode_ToUppercase
        f.argtypes, f.restype = [ctypes.c_uint32], ctypes.c_uint32
        _f.append(f)
    return _f[0](c)


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
        # re.IGNORECASE: sre's simple lower-case map, its "cased" test and the extra equivalences of re._casefix
        "sre_lower": {str(c): _sre.unicode_tolower(c) for c in range(0x110000) if _sre.unicode_tolower(c) != c},
        "sre_upper": {str(c): sre_upper(c) for c in range(0x110000) if sre_upper(c) != c},
        "sre_iscased": runs(lambda c: _sre.unicode_iscased(c)),
        "casefix": {str(k): list(v) for k, v in _casefix._EXTRA_CASES.items()},
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
    # re.IGNORECASE as sre does it (simple lower map plus re._casefix), never Unicode case folding
    [r"(?i)diagram|ſ+", 0], [r"k+", re.I], [r"[^k\W]+", re.I], [r"[ia-c]+", re.I], [r"\w+", re.I], [r"(?i:s)(?-i:s)", 0],
    [r"[\u0370-\u03ff]+", re.I], [r"\u212a|\u0130|\u0345", re.I], [r"(?a)[k-s]+", re.I], [r"[\U00010400-\U0001044f]+", re.I],
]
RE_ALPHA = ["a", "b", "c", "x", "k", "D7", "D-7", "PR 7", "PR7", "OS-01", "F3.2", "NULL", "null", "é", "ß", "٣", "中", "\U0001D400",
            "\u1c89", "\u0663", "_", "#", "/", ".", "-", " ", "  ", "\n", "\r", "\u2028", "`", "``", "*", "\\", "[", "]", "{", "}", ":", "·",
            "=", "a=a", "k=k", "## ", "```", "&lt;", "&gt;", "http://x", "$1", "'", "’", "(", ")", "|", "!", "ÿ", "\xa0", "\u3000", "\x1c",
            "I", "S", "K", "\u0130", "\u0131", "\u017f", "\u212a", "\u0345", "\u03b9", "\u03d0", "\u00b5", "\u1e9e", "\u03c2", "\U00010400", "\U00010428"]

# Single-code-point patterns under re.IGNORECASE: the oracle reports, for each, every code point it matches
RE_CASE = [[p, re.I] for p in [
    "i", "I", "\u0130", "\u0131", "s", "S", "\u017f", "k", "K", "\u212a", "\u0345", "\u03b9", "\u0399", "\u1fbe", "\u03b2", "\u03d0", "\u00b5",
    "\u03bc", "\u2126", "\u03c9", "\u1e9e", "\u00df", "\u03c3", "\u03c2", "\U00010400", "\U00010428", "a", "1", "_", "\u01c5", "\u0398",
    "[a-z]", "[A-Z]", "[^a-z]", "[i]", "[^i]", "[k-l]", "[\\w]", "[\\w\u0345]", "[a\\W]", "[^a\\W]", "[s\\d]", "[\u0100-\u017f]", "[\u0370-\u03ff]",
    "[\U00010400-\U0001044f]", "[x\U00010400-\U00010402]", "[\U00010400x]", "[\U0001d400a]", "[\U0001d400-\U0001d405]", "[A-\U00010400]",
    "[^\U00010428-\U0001042fA]", "[\u00df-\u00ff]", "[\u1e00-\u1eff]", "[\\s\u212a]", "[^\\s\u212a]", "[\\D\u0130]", "[ia]", "[aa]", "[a-aa]",
    "\\w", "\\W", "[^\\W\\d]", "(?i:i)", "(?-i:k)", "(?a:k)", "(?a:[\\w\u212a])", ".",
]] + [[p, re.I | re.A] for p in [
    "i", "k", "\u212a", "\u0130", "[a-z]", "[^k]", "[\\w\u212a]", "[\U00010428-\U0001042f]", "[\U00010400\u0130]", "(?u:k)",
]]



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


def suite_re_case(rng, count):
    """re.IGNORECASE: the code points each single-code-point pattern matches, as inclusive runs over U+0000..U+10FFFF."""
    every = "".join(map(chr, range(0x110000)))  # index == code point
    rows = []
    for pat, flags in RE_CASE:
        rx = re.compile(f"(?:{pat})+", flags)
        rows.append({"pattern": pat, "flags": flags, "runs": [[m.start(), m.end() - 1] for m in rx.finditer(every)]})
    refused = []
    for pat, flags in [(r"(a)\1", re.I), (r"(?P<n>a)(?P=n)", re.I), (r"(a)(?i:\1)", 0)]:
        rx = re.compile(pat, flags)  # CPython compares lower(a) with lower(b); the kernel refuses these
        refused.append({"pattern": pat, "flags": flags, "full": [s for s in ["aa", "aA", "Aa", "a\u0130", "\u017fs"] if rx.fullmatch(s)]})
    return {"rows": rows, "refused": refused}


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


# ----------------------------------------------------------------------------- the D1 surface (PR 10, src/sd)
# Five suites feed test/sd: sd_calls replays a real compile (every call build_tdd.py makes to the 18 system-design
# names, recorded at the boundary), sd_fuzz runs markdown-shaped documents through the text functions, sd_files runs
# img_size / load_json / Assets over files the bun test wrote, sd_finish runs Assets.finish's copying over folders it
# wrote, and sd_constants dumps the patterns and tables. Values travel as vtag() trees (tuples as lists);
# an exception travels as [qualified type name, str(e)] and SystemExit as ["SystemExit", code].

ARGS = {"packet": None, "src": None, "copy": False, "dir": None}


def vtag(v):
    """tag() with tuples (as lists) and nested containers of any depth."""
    if isinstance(v, (list, tuple)):
        return {"t": "l", "v": [vtag(x) for x in v]}
    if isinstance(v, dict):
        return {"t": "d", "v": [[k, vtag(x)] for k, x in v.items()]}
    return tag(v)


def exc_name(e):
    t = type(e)
    if isinstance(e, SystemExit):
        return "SystemExit"
    mod = t.__module__
    return t.__qualname__ if mod == "builtins" else f"{mod}.{t.__qualname__}"


def attempt(fn, *a, **k):
    """{"r": result} or {"x": [type, message]}; SystemExit is caught too (load_json exits)."""
    try:
        return {"r": vtag(fn(*a, **k))}
    except SystemExit as e:
        return {"x": ["SystemExit", e.code if isinstance(e.code, (int, str)) or e.code is None else str(e.code)]}
    except Exception as e:  # noqa: BLE001 - the oracle reports every failure as data
        return {"x": [exc_name(e), str(e)]}


def sd_modules():
    """build_tdd as the compiler loads it (bd.MARK replaced), plus its bd / bp and the files' sha256."""
    tdd = load(os.path.join(SCRIPTS, "build_tdd.py"), "build_tdd")
    root = tdd.sd_root()
    sha = {name: hashlib.sha256(open(os.path.join(root, "scripts", name), "rb").read()).hexdigest() for name in ("build_doc.py", "build_prs.py")}
    return tdd, tdd.bd, tdd.bp, root, sha


def assets_state(a):
    return {"out_dir": a.out_dir, "inline": a.inline, "copy": a.copy, "files": a.files, "missing": a.missing, "order": a.order}


def uri_digest(r):
    """Assets.ref's (url, src) with a data: URL cut to its first 64 characters, its length and its sha256."""
    url, src = r
    if url.startswith("data:"):
        return [[url[:64], len(url), hashlib.sha256(url.encode()).hexdigest()], src]
    return [url, src]


class Recorder:
    """Wraps the names build_tdd reaches through bd / bp (and its esc, slug, strip_tags aliases) and logs each call."""

    def __init__(self, bd, bp):
        self.bd, self.bp, self.calls, self.next_id = bd, bp, [], 0

    def log(self, f, a, k, fn, obj=None, state=None, ctor=False):
        entry = {"f": f, "a": [vtag(x) for x in a], "k": {n: vtag(v) for n, v in k.items()}}
        if obj is not None:
            entry["o"] = obj
        try:
            r = fn(*a, **k)
        except SystemExit as e:
            entry["x"] = ["SystemExit", str(e.code)]
            self.calls.append(entry)
            raise
        except Exception as e:
            entry["x"] = [exc_name(e), str(e)]
            self.calls.append(entry)
            raise
        entry["r"] = vtag(None if ctor else r)
        if state is not None:
            entry["s"] = vtag(state(r))
        self.calls.append(entry)
        return r

    def fn(self, prefix, mod, name):
        target = getattr(mod, name)
        return lambda *a, **k: self.log(f"{prefix}.{name}", a, k, target)

    def inline_cls(self):
        rec, real = self, self.bd.Inline

        class RecInline:
            def __init__(self):
                self._id = rec.next_id
                rec.next_id += 1
                self._i = rec.log("bd.Inline", (), {}, real, obj=self._id, state=lambda r: r.anchor_map, ctor=True)

            def __call__(self, *a, **k):
                return rec.log("bd.Inline.__call__", a, k, self._i, obj=self._id, state=lambda r: self._i.anchor_map)

            def __getattr__(self, n):
                return getattr(self._i, n)
        return RecInline

    def assets_cls(self):
        rec, real = self, self.bd.Assets

        class RecAssets:
            def __init__(self, *a, **k):
                self._id = rec.next_id
                rec.next_id += 1
                self._a = rec.log("bd.Assets", a, k, real, obj=self._id, state=assets_state, ctor=True)

            def ref(self, *a, **k):
                return rec.log("bd.Assets.ref", a, k, self._a.ref, obj=self._id, state=lambda r: assets_state(self._a))

            def finish(self, *a, **k):
                return rec.log("bd.Assets.finish", a, k, self._a.finish, obj=self._id, state=lambda r: assets_state(self._a))

            def __getattr__(self, n):
                return getattr(self._a, n)
        return RecAssets

    def status_words(self):
        rec = self

        class RecDict(dict):
            def get(self, *a):
                return rec.log("bd.STATUS_WORDS.get", a, {}, lambda *x: dict.get(self, *x))
        return RecDict(self.bd.STATUS_WORDS)

    def proxies(self):
        bd, bp, rec = self.bd, self.bp, self
        bd_names = {n: self.fn("bd", bd, n) for n in ("parse_blocks", "split_marker", "slug", "strip_tags", "img_size", "published_for",
                                                     "page_title", "load_json", "esc")}
        bd_names.update(Inline=self.inline_cls(), Assets=self.assets_cls(), STATUS_WORDS=self.status_words())
        bp_names = {n: self.fn("bp", bp, n) for n in ("front", "first_sentence", "inline", "markdown")}

        class Proxy:
            def __init__(self, mod, names, prefix):
                self._mod, self._names, self._prefix = mod, names, prefix

            def __getattr__(self, n):
                if n in self._names:
                    return self._names[n]
                if n == "FILE_LIMIT":
                    return rec.log("bd.FILE_LIMIT", (), {}, lambda: self._mod.FILE_LIMIT)
                if not n.startswith("__"):
                    raise AttributeError(f"build_tdd reached {self._prefix}.{n}, which the D1 surface does not list")
                return getattr(self._mod, n)
        return Proxy(bd, bd_names, "bd"), Proxy(bp, bp_names, "bp")


def packet_source():
    packet = ARGS["packet"]
    if not packet:
        raise SystemExit("--packet <dir> is required")
    packet = os.path.abspath(packet)
    return packet, os.path.abspath(ARGS["src"] or os.path.join(packet, os.path.basename(packet) + ".md"))


def suite_sd_calls(rng, count):
    """Compile --packet with build_tdd.py (--no-copy unless --copy) into a temporary folder, recording every call it makes
    to the D1 surface; the folder is removed afterwards. Also every image file under the packet through img_size and
    through Assets(inline=True).ref(embed=True), the inline branch the compile never takes."""
    import contextlib, shutil, tempfile
    packet, src = packet_source()
    tdd, bd, bp, root, sha = sd_modules()
    rec = Recorder(bd, bp)
    real = (tdd.bd, tdd.bp, tdd.esc, tdd.slug, tdd.strip_tags)
    tdd.bd, tdd.bp = rec.proxies()
    tdd.esc, tdd.slug, tdd.strip_tags = (rec.fn("bd", bd, n) for n in ("esc", "slug", "strip_tags"))
    out_dir = tempfile.mkdtemp(prefix="tdd-sd-oracle-")
    stdout, stderr = io.StringIO(), io.StringIO()
    cwd = os.getcwd()
    try:
        os.chdir(packet)
        argv = [src, out_dir] + ([] if ARGS["copy"] else ["--no-copy"])
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            tdd.Compiler(tdd.parse_args(argv)).build()
    finally:
        os.chdir(cwd)
        tdd.bd, tdd.bp, tdd.esc, tdd.slug, tdd.strip_tags = real
        shutil.rmtree(out_dir, ignore_errors=True)
    images = []
    for d, dirs, files in os.walk(packet):
        dirs[:] = sorted(x for x in dirs if x not in ("node_modules", ".git"))
        for f in sorted(files):
            if os.path.splitext(f)[1].lower() in (".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp"):
                p = os.path.join(d, f)
                emb = bd.Assets(out_dir, True, False)
                images.append([p, attempt(bd.img_size, p), attempt(lambda: uri_digest(emb.ref("x/" + f, [p], embed=True)))])
    return {"sha256": sha, "root": os.path.relpath(root, TDD), "packet": packet, "src": src, "out": out_dir, "copy": ARGS["copy"],
            "mark": [bd.MARK.pattern, bd.MARK.flags], "stdout": stdout.getvalue(), "stderr": stderr.getvalue(),
            "calls": rec.calls, "images": images}


# A markdown-shaped document generator: block lines from the subset's grammar, inline runs from its syntax, and a
# little noise. Every string is valid Unicode (no lone surrogates: the inputs come from UTF-8 files).
MD_WORDS = ["word", "Word", "the", "Rollback:", "D7", "PR 7", "F3.2", "x", "É", "İstanbul", "ß", "中文", "\U0001F600", "١٢", "x_y", "a*b",
            "snake_case", "C++", "q?", "end.", "Done!", "why?", "(paren)", "[br]", "{attr}", "#tag", "a|b", "a\\|b", "·", "—", "–", ":", "|",
            "\xa0", "　", " ", "\x85", "\x1c", "\t", "\x00"]
# Placeholder look-alikes: Inline and build_prs.inline index their saved spans with them, so a stray one raises
# IndexError in both implementations; rare, so most documents still render.
MD_NUL = ["\x00C0\x00", "\x00E0\x00", "\x007\x00", "\x0099\x00", "\x00C٣\x00", "\x00" + "9" * 20 + "\x00"]
MD_INLINE = ["`code`", "``a ` b``", "`", "` x `", "**bold**", "**", "** x**", "__u__", "_e_", "x_e_x", "*e*", "* x *", "*a*b", "~~d~~",
             "[l](#Anchor)", "[l](#a \"t\")", "[l](https://x.y/z \"t\")", "[l](http://a)", "[x\ny](z)", "[a](b c)", "<https://x.y/p>",
             "<http://a&b>", "<ftp://x>", "![i](x.png)", "![a \"q\"](p\"q.png)", "\\*", "\\|", "\\_", "\\\\", "\\`", "\\[", "\\&",
             "<b>", "</b>", "<a href=\"x\">", "<br/>", "<!-- c -->", "&amp;", "&#39;", "&lt;", "&gt;", "&#x1F600;", "&nbsp", "&nbsp;",
             "&notit;", "&#0;", "&#xD800;", "\"", "'", "<", ">", "&", "[chip:new text]", "D7.", "PR 12a", "LEG/File.ts:12-40"]
MD_LANGS = ["", "ts", "python", "c++", "Py-3", "ÉX", "sql", "mermaid", "a b", "{x}"]
MD_MARKERS = ["diagram", "image", "video", "media", "embedded content", "legend", "reading", "DIAGRAM", "Legend", "dİagram", "note"]


def md_inline(rng, lo=1, hi=8):
    parts = []
    for _ in range(rng.randint(lo, hi)):
        k = rng.random()
        parts.append(rng.choice(MD_NUL) if k < 0.002 else rng.choice(MD_WORDS) if k < 0.5 else rng.choice(MD_INLINE) if k < 0.9
                     else rand_str(rng, RE_ALPHA, 0, 4))
    out = ""
    for p in parts:
        out += p if not out or rng.random() < 0.15 else " " + p
    return out


def md_block(rng):
    k = rng.randint(0, 15)
    ind = rng.choice(["", "", "", " ", "  ", "   ", "    ", "\t"])
    if k == 0:
        return [rng.choice(["#", "##", "###", "######", "#######", "#"]) + rng.choice([" ", " ", "  ", "", "\t"]) + md_inline(rng, 0, 4)
                + rng.choice(["", "", " ##", " #", "#"])]
    if k == 1:
        f = rng.choice(["```", "~~~", "````", "``"])
        body = [md_inline(rng, 0, 5) for _ in range(rng.randint(0, 3))]
        close = rng.choice([f, f, "```", "  " + f, ""])
        return [ind + f + rng.choice(["", " "]) + rng.choice(MD_LANGS)] + body + ([close] if close else [])
    if k in (2, 3):
        lines, depth = [], 0
        ordered = rng.random() < 0.4
        for j in range(rng.randint(1, 6)):
            depth = max(0, min(3, depth + rng.choice([-1, 0, 0, 1])))
            pad = " " * (2 * depth) if rng.random() < 0.9 else "\t" * depth
            mark = (rng.choice(["1.", "2)", "10.", "１.", "٣.", "0."]) if ordered else rng.choice(["-", "*", "+"])) if rng.random() < 0.9 else rng.choice(["-", "1."])
            box = rng.choice(["", "", "", "[ ] ", "[x] ", "[X] ", "\\[x\\] ", "[y] "])
            lines.append(pad + mark + rng.choice([" ", " ", "  ", "\t"]) + box + md_inline(rng, 1, 5))
            r = rng.random()
            if r < 0.15:
                lines.append(pad + "  " + md_inline(rng, 1, 4))
            elif r < 0.25:
                lines.append("")
            elif r < 0.3:
                lines.append(rng.choice(["  ```", "    > q", "    | a |", "  # h"]))
        return lines
    if k == 4:
        lines = []
        if rng.random() < 0.4:
            lines.append(rng.choice([">", "> ", " >"]) + rng.choice(["[!NOTE]", "[!tip]", "[!WARNING]", "\\[!caution\\]", "[!constructor]", "[!x y]", "[!]"]))
        for _ in range(rng.randint(1, 4)):
            lines.append(rng.choice([">", "> ", ">  ", " > ", ">> ", "> > ", "> - ", "> 1. ", "> ```"]) + md_inline(rng, 0, 5))
        return lines
    if k == 5:
        cols = rng.randint(1, 4)
        row = lambda: rng.choice(["|", "", " |"]) + "|".join(" " + md_inline(rng, 0, 2) + " " for _ in range(cols)) + rng.choice(["|", "", "\\|", "| "])
        seps = ["|" + "|".join(rng.choice(["---", ":--", "--:", ":-:", "-", " : ", "--- "]) for _ in range(cols)) + "|",
                "|".join("---" for _ in range(cols)), "| - |", "|:|", "| --- | ---", "---"]
        return [row(), rng.choice(seps)] + [row() for _ in range(rng.randint(0, 3))]
    if k == 6:
        return [ind + rng.choice(["---", "***", "___", "* * *", "- - -", "-_-", "----------", "--", "_ _ _ _", "**"])]
    if k == 7:
        return [ind + rng.choice(["<!-- note -->", "<!-- a --> b", "<!---->", "<!-- x", "<!-- PR-CARDS -->"])]
    if k == 8:
        m = rng.choice(MD_MARKERS)
        arg = rng.choice(["key", "key · Caption", "a | b", "key · Title {fit; #id}", "new = green | old = grey", " ", "x · ", md_inline(rng, 0, 3)])
        return [ind + rng.choice(["[", "\\[", "["]) + m + rng.choice([":", " :", ": ", ":"]) + arg + rng.choice(["]", "\\]", "] ", ""])]
    if k == 9:
        return [rng.choice(["![alt](path.png \"Title\")", "![a](b)", "![a](b c)", "![](x.svg)", "![a](b \"t\") x", "![a](b \"t\"", "![a]](b)"])]
    if k == 10:
        return [""] * rng.randint(1, 2)
    if k == 11:
        return [rand_str(rng, RE_ALPHA + MD_INLINE, 0, 10)]
    if k == 12:
        return ["Rollback: " + md_inline(rng, 1, 4) + rng.choice([". ", ".", ""]) + md_inline(rng, 0, 3)]
    return [ind + md_inline(rng) for _ in range(rng.randint(1, 3))]


def md_doc(rng):
    lines = []
    if rng.random() < 0.15:
        lines += ["---", rng.choice(["title: A · B", "milestone: \"M1 · x\"", "k: v # note", "a:b", "x", ": v", "change: 'y'"]),
                  rng.choice(["depends_on: [PR 9]", "", "rollback: Delete it. # c"]), "---"]
    for _ in range(rng.randint(1, 12)):
        lines += md_block(rng)
    return "\n".join(lines) + rng.choice(["", "", "\n", "\n\n"])


def suite_sd_fuzz(rng, count):
    """count documents through parse_blocks (half with build_doc's own MARK, half with build_tdd's), Inline (links on and
    off, the whole document and one line), strip_tags, slug, split_marker, page_title, split_row, is_block_start and
    build_prs's front, inline, markdown, split_row and first_sentence."""
    root = sd_root()
    bd = load(os.path.join(root, "scripts", "build_doc.py"), "build_doc")
    bp = load(os.path.join(root, "scripts", "build_prs.py"), "build_prs")
    tdd = load(os.path.join(SCRIPTS, "build_tdd.py"), "build_tdd")
    marks = [bd.MARK, tdd.bd.MARK]
    rows = []
    for n in range(count):
        doc = md_doc(rng)
        lines = doc.split("\n")
        line = rng.choice(lines)
        bd.MARK = marks[n % 2]
        inl = bd.Inline()
        r_inl = attempt(inl, doc)
        rows.append({
            "doc": doc, "line": line, "mark": n % 2,
            "parse_blocks": attempt(bd.parse_blocks, lines),
            "is_block_start": attempt(bd.is_block_start, line),
            "Inline": r_inl,
            "Inline_line": attempt(inl, line, links=False),
            "strip_tags": attempt(bd.strip_tags, doc),
            "strip_tags_inline": attempt(bd.strip_tags, r_inl["r"]["v"]) if "r" in r_inl else None,
            "slug": [attempt(bd.slug, line), attempt(bd.slug, doc)],
            "split_marker": attempt(bd.split_marker, line),
            "page_title": attempt(bd.page_title, line),
            "split_row": attempt(bd.split_row, line),
            "esc": [attempt(bd.esc, line), attempt(bd.esc, line, quote=False)],
            "bp.front": attempt(bp.front, doc),
            "bp.inline": attempt(bp.inline, doc),
            "bp.markdown": attempt(bp.markdown, doc),
            "bp.split_row": attempt(bp.split_row, line),
            "bp.first_sentence": attempt(bp.first_sentence, doc),
        })
    bd.MARK = marks[0]
    return {"marks": [[m.pattern, m.flags] for m in marks], "rows": rows}


def suite_sd_finish(rng, count):
    """Assets.finish with copying, per case folder in --dir (sorted): case.json holds {"out", "copy", "refs": [[published,
    [candidate, ...]], ...]}, paths relative to the case folder. Records ref results, finish's result or exception, and
    the state; the bun test runs the same cases on a twin tree."""
    d = ARGS["dir"]
    if not d:
        raise SystemExit("--dir <folder> is required")
    bd = load(os.path.join(sd_root(), "scripts", "build_doc.py"), "build_doc")
    rows = []
    for case in sorted(os.listdir(d)):
        c = os.path.join(d, case)
        spec = json.load(open(os.path.join(c, "case.json"), encoding="utf-8"))
        a = bd.Assets(os.path.join(c, spec["out"]), False, spec["copy"])
        refs = [attempt(a.ref, pub, [os.path.join(c, x) for x in cands]) for pub, cands in spec["refs"]]
        rows.append({"case": case, "refs": refs, "finish": attempt(a.finish), "state": vtag(assets_state(a))})
    return {"rows": rows}


def suite_sd_constants(rng, count):
    """The data half of the surface: the patterns (source and flags), STATUS_WORDS, GENERIC_TAILS, FILE_LIMIT, BLOCK and
    build_tdd's replacement MARK, so a changed table is caught even where no packet looks it up."""
    tdd, bd, bp, root, sha = sd_modules()
    pristine = load(os.path.join(root, "scripts", "build_doc.py"), "build_doc")
    pat = lambda p: [p.pattern, p.flags]
    return {"sha256": sha, "patterns": {n: pat(getattr(pristine, n)) for n in ("MARK", "HEAD", "FENCE", "LI", "HR", "TSEP", "COMMENT")},
            "tdd_mark": pat(bd.MARK), "STATUS_WORDS": vtag(bd.STATUS_WORDS), "GENERIC_TAILS": vtag(bd.GENERIC_TAILS),
            "FILE_LIMIT": bd.FILE_LIMIT, "BLOCK": bp.BLOCK}


def json_depth(v):
    """The container nesting of a parsed JSON value, without recursion (vtag would hit Python's recursion limit)."""
    best, stack = 0, [(v, 1)]
    while stack:
        x, d = stack.pop()
        if isinstance(x, (list, dict)):
            best = max(best, d)
            stack.extend((y, d + 1) for y in (x.values() if isinstance(x, dict) else x))
    return best


def suite_sd_files(rng, count):
    """Every file in --dir (sorted) through img_size, svg_size, png_size, published_for, load_json, Assets.ref (embed, inline on).
    For a file named deep-*, load_json reports the parsed value's nesting depth instead of the value."""
    d = ARGS["dir"]
    if not d:
        raise SystemExit("--dir <folder> is required")
    root = sd_root()
    bd = load(os.path.join(root, "scripts", "build_doc.py"), "build_doc")
    rows = []
    for f in sorted(os.listdir(d)):
        p = os.path.join(d, f)
        a = bd.Assets(d, True, False)
        rows.append({"name": f, "img_size": attempt(bd.img_size, p), "svg_size": attempt(bd.svg_size, p), "png_size": attempt(bd.png_size, p),
                     "load_json": attempt(lambda: json_depth(bd.load_json(p, "--meta"))) if f.startswith("deep-") else attempt(bd.load_json, p, "--meta"),
                     "embed": attempt(lambda: uri_digest(a.ref("../" + f if f.startswith("x") else f, [None, "", p], embed=True))),
                     "state": vtag(assets_state(a))})
    extra = [[p, attempt(bd.load_json, p, "schema/x.json")] for p in ["", None, os.path.join(d, "no-such-file.json"), d, os.path.join(d, "a\x00b")]]
    extra += [[p, attempt(bd.img_size, p)] for p in [os.path.join(d, "no-such.svg"), os.path.join(d, "no-such.PNG"), os.path.join(d, "a\x00b.svg")]]
    pub = [[p, x, attempt(bd.published_for, p, x)] for p in ["a/b.png", "/abs/c.svg", "../up/d.png", "..x.png", "./e.png", "f", "", "/"] for x in ["images", ""]]
    return {"rows": rows, "extra": extra, "published_for": pub}


SUITES = {name[6:]: fn for name, fn in globals().items() if name.startswith("suite_")}
DEFAULT_COUNT = {"round": 100000, "repr_float": 100000, "str": 3000, "re": 300, "html": 3000, "repr": 2000, "json": 1500, "utf8": 5000,
                 "path": 3000, "int": 0, "truthy": 200, "token": 2000, "sd": 2000, "stamp": 200, "ucd": 0, "json_errors": 0, "re_case": 0,
                 "sd_calls": 0, "sd_fuzz": 10000, "sd_files": 0, "sd_constants": 0, "sd_finish": 0}


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
        elif rest[i] in ("--packet", "--src", "--dir"):
            ARGS[rest[i][2:]] = rest[i + 1]; i += 2
        elif rest[i] == "--copy":
            ARGS["copy"] = True; i += 1
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
