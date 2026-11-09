"""Test-only oracle for the TypeScript eval: loads scripts/eval/ev_*.py (and the eval's sibling scripts) by path,
never writing bytecode, and prints the golden JSON a suite asks for. Usage: python3 -I -B oracle_eval.py <suite> [args]."""
import importlib.util, json, os, sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
SKILL = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
SCRIPTS = os.path.join(SKILL, "scripts")
EVAL = os.path.join(SCRIPTS, "eval")


def load(path, name):
    if name in sys.modules:
        return sys.modules[name]
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


def ev(name):
    load(os.path.join(EVAL, "ev_common.py"), "ev_common")
    load(os.path.join(EVAL, "ev_llm.py"), "ev_llm")
    return load(os.path.join(EVAL, name + ".py"), name)


def suite_constants():
    C, L, J, P, A, K = ev("ev_common"), ev("ev_llm"), ev("ev_judge"), ev("ev_probe"), ev("ev_artifact"), ev("ev_packet")
    lint = load(os.path.join(SCRIPTS, "lint_packet.py"), "tdd_eval_lint_packet")
    wa = load(os.path.join(SCRIPTS, "write_agents.py"), "tdd_eval_write_agents")
    ep = load(os.path.join(SCRIPTS, "eval_packet.py"), "tdd_eval_packet_main")
    return {
        "doc": ep.__doc__, "token": C.TOKEN.pattern, "judge_model": L.JUDGE_MODEL, "prices": [[k, v] for k, v in L.PRICES.items()],
        "price_source": L.PRICE_SOURCE, "criteria": [[k, v] for k, v in J.CRITERIA.items()], "judge_system": J.SYSTEM, "judge_schema": J.SCHEMA,
        "probe_system": P.SYSTEM, "probe_schema": P.SCHEMA, "house_slots": [[n, sorted(ids), rx] for n, ids, rx in A.HOUSE_SLOTS],
        "lint_slots": [[n, sorted(ids), rx] for n, ids, rx in lint.Lint.SLOTS], "readme_max": wa.README_MAX, "budget": wa.BUDGET,
        "exempt": sorted(wa.EXEMPT), "value_flags": sorted(ep.VALUE_FLAGS), "bool_flags": sorted(ep.BOOL_FLAGS),
    }


def suite_funcs():
    """Golden values for the small pure functions, over the cases in argv[2] (a JSON file)."""
    K, P, I, L = ev("ev_packet"), ev("ev_probe"), ev("ev_impl"), ev("ev_llm")
    cases = json.load(open(sys.argv[2], encoding="utf-8"))
    out = {}
    out["unicode_escape"] = []
    for s in cases["unicode_escape"]:
        try:
            out["unicode_escape"].append(s.encode().decode("unicode_escape"))
        except Exception as e:
            out["unicode_escape"].append({"error": type(e).__name__})
    out["front_anchors"] = [K.front_anchors(t) for t in cases["front_anchors"]]
    out["format"] = [t.format(**f) for t, f in cases["format"]]
    out["days"] = [I.days_between(a, b) for a, b in cases["days"]]
    out["similar"] = [I.similar(a, b) for a, b in cases["similar"]]
    out["cost"] = [L.cost(m, u) for m, u in cases["cost"]]
    out["base_type"] = [P.base_type(t) for t in cases["base_type"]]
    out["matches"] = [P.matches(q, a) for q, a in cases["matches"]]
    out["readme_links"] = [P.readme_links(t) for t in cases["readme_links"]]
    return out


def suite_glob():
    """The session transcript lookup (ev_tokens.session_files) under $HOME, for the ids in argv[2:]."""
    T = ev("ev_tokens")
    return {sid: list(T.session_files(sid)) for sid in sys.argv[2:]}


def suite_sum():
    """sum() over each list in argv[2] (a JSON file; ints stay ints, floats floats, Infinity/NaN allowed), as
    "<type>:<repr>" or "error:<exception>"; plus the declared test command for each repo path in argv[3:]."""
    I = ev("ev_impl")
    out = []
    for xs in json.load(open(sys.argv[2], encoding="utf-8")):
        try:
            r = sum(xs)
            out.append(f"{type(r).__name__}:{r!r}")
        except Exception as e:
            out.append(f"error:{type(e).__name__}")
    return {"sums": out, "commands": [list(I.declared_test_command(p)) for p in sys.argv[3:]], "executable": sys.executable}


def suite_run():
    """ev_common.run over each [cmd, timeout] in argv[2] (a JSON file): the record, with its wall time beside it."""
    import time
    C = ev("ev_common")
    out = []
    for cmd, timeout in json.load(open(sys.argv[2], encoding="utf-8")):
        t0 = time.time()
        r = C.run(cmd, timeout=timeout)
        out.append({"record": r, "wall": round(time.time() - t0, 2)})
    return out


if __name__ == "__main__":
    print(json.dumps({"constants": suite_constants, "funcs": suite_funcs, "glob": suite_glob, "sum": suite_sum, "run": suite_run}[sys.argv[1]](),
                     ensure_ascii=False))
