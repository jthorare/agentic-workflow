#!/usr/bin/env python3
"""Create a new TDD packet from the house template.

Usage:
  new_packet.py <slug> [--title T] [--ticket P-XXX-NNN] [--repo <path>] [--root <dir>]

Creates <root>/<repo-slug>/system-design/<slug>/ with:
  <slug>.md       templates/tdd-template.md, with the title, ticket, sha and date filled in
  meta.json       templates/meta.template.json without its _doc block, filled: title (the tab title, from --title the
                  way build_doc.page_title derives it, else from the slug), project (the ticket), Author (git user.name),
                  updated (today), rev 1, repo_url (GitHub or GitLab origin only), sha, sha_date, repo_path
  status.md       the one living doc: artifact_url placeholder, Reference, Surfaces, Counts, Decisions, Open
                  questions, Lead defaults, Revisions, Tracker
  diagrams/*.svg  templates/packet/ copied as it is: a placeholder image, marked PLACEHOLDER and REPLACE ME, for
  mockups/*.svg   every diagram and shot the template references, so a fresh packet builds and renders; it lints
                  with FILES/placeholder and FILES/placeholder-text errors until the author replaces each one (the
                  "todo:" line lists the images)
  prs/ diagrams/ mockups/ schema/ queries/ shots/   working folders (prs/ schema/ queries/ shots/ start empty)

<root> is $SYSTEM_DESIGN_OUT, else ~/.system-design. <repo-slug> is derived the way system-design's scripts/env.sh does
it: owner-repo from the origin remote of --repo (default: the current folder), else the repo root's folder name, else
the folder's own name. The sha is origin/HEAD when that ref exists, else HEAD; sha_date is its commit date. Nothing is
fetched: run `git fetch` first if the remote default branch should be current.

Refuses to write into a packet folder that already has files in it. Standard library only; run with python3 -I.
Exit 0 on success; 1 on bad arguments, an existing packet, or a missing template."""
import argparse, datetime, json, os, re, subprocess, sys
sys.dont_write_bytecode = True

HERE = os.path.dirname(os.path.abspath(__file__))
SKILL = os.path.normpath(os.path.join(HERE, ".."))
TEMPLATE_MD = os.path.join(SKILL, "templates", "tdd-template.md")
TEMPLATE_META = os.path.join(SKILL, "templates", "meta.template.json")
TEMPLATE_PACKET = os.path.join(SKILL, "templates", "packet")
DIRS = ("prs", "diagrams", "mockups", "schema", "queries", "shots")

def git(repo, *args):
    """Output of a git command in repo, or None when git fails (not a repo, no such ref, no remote)."""
    try:
        r = subprocess.run(["git", "-C", repo, *args], capture_output=True, text=True, timeout=20)
    except (OSError, subprocess.SubprocessError):
        return None
    out = r.stdout.strip()
    return out if r.returncode == 0 and out else None

def repo_slug(repo, origin):
    """system-design/scripts/env.sh: the two sed substitutions on the origin URL, then '/' -> '-'."""
    if origin:
        s = re.sub(r".*[:/]([^/]*/[^/]*)\.git$", r"\1", origin, count=1)
        s = re.sub(r".*[:/]([^/]*/[^/]*)$", r"\1", s, count=1)
        return s.replace("/", "-")
    top = git(repo, "rev-parse", "--show-toplevel")
    return os.path.basename(top or os.path.abspath(repo))

def web_url(origin):
    """https://<host>/<owner>/<repo> for a github.com or gitlab.com origin; None for anything else."""
    if not origin:
        return None
    m = re.match(r"^(?:https?://(?:[^@/]+@)?|ssh://(?:[^@/]+@)?|[^@/]+@)(github\.com|gitlab\.com)[:/](.+?)(?:\.git)?/?$", origin)
    return f"https://{m.group(1)}/{m.group(2)}" if m else None

GENERIC_TAILS = [(r"\s+technical\s+spec(ification)?$", " Spec"), (r"\s+technical\s+design(\s+doc(ument)?)?$", " TDD"),
                 (r"\s+design\s+doc(ument)?$", " Design Doc"), (r"\s+system\s+design$", " Design")]

def tab_title(h1, slug):
    """The browser-tab title (meta.title, which the linter requires): system-design's build_doc.page_title applied to
    the H1, or the slug's first three words plus TDD when no title was given."""
    if not h1:
        return " ".join(w.capitalize() for w in slug.split("-")[:3]) + " TDD"
    t = re.split(r"\s+[—–|:]\s+|:\s+", h1, maxsplit=1)[0].strip()
    if len(t.split()) > 4:
        for pat, rep in GENERIC_TAILS:
            t2 = re.sub(pat, rep, t, flags=re.I)
            if t2 != t:
                t = t2
                break
    words = t.split()
    return " ".join(words[:4]) if len(words) > 4 else t

def packet_files():
    """Every file under templates/packet/ as a '/'-separated path relative to it, sorted; names starting with '.' are
    skipped (an editor's or Finder's droppings never reach a packet)."""
    out = []
    for d, dirs, files in os.walk(TEMPLATE_PACKET):
        dirs[:] = [x for x in dirs if not x.startswith(".")]
        rel = os.path.relpath(d, TEMPLATE_PACKET)
        for f in files:
            if not f.startswith("."):
                out.append(f if rel == "." else rel.replace(os.sep, "/") + "/" + f)
    return sorted(out)

def tilde(path):
    home = os.path.expanduser("~")
    return "~" + path[len(home):] if path == home or path.startswith(home + os.sep) else path

def main(argv):
    ap = argparse.ArgumentParser(prog="new_packet.py", description=__doc__.split("\n\n")[0])
    ap.add_argument("slug", help="packet folder name: lower-case letters, digits and hyphens")
    ap.add_argument("--title", help="the page title (the H1): the finding, not the topic")
    ap.add_argument("--ticket", help="ticket or project id, e.g. P-XXX-000")
    ap.add_argument("--repo", default=os.getcwd(), help="the code repo the design is about (default: current folder)")
    ap.add_argument("--root", help="deliverables root (default: $SYSTEM_DESIGN_OUT, else ~/.system-design)")
    a = ap.parse_args(argv)

    if not re.fullmatch(r"[a-z0-9][a-z0-9-]{0,62}", a.slug):
        sys.exit(f"slug {a.slug!r}: use lower-case letters, digits and hyphens (it names the folder and <slug>.md)")
    if a.ticket is not None and not a.ticket.strip():
        sys.exit("--ticket is empty")
    for t in (TEMPLATE_MD, TEMPLATE_META):
        if not os.path.isfile(t):
            sys.exit(f"{t}: template missing; is the tdd skill complete?")
    if not os.path.isdir(TEMPLATE_PACKET):
        sys.exit(f"{TEMPLATE_PACKET}: template missing; is the tdd skill complete?")
    repo = os.path.abspath(os.path.expanduser(a.repo))
    if not os.path.isdir(repo):
        sys.exit(f"--repo {repo}: no such folder")

    origin = git(repo, "remote", "get-url", "origin")
    rslug = repo_slug(repo, origin)
    root = os.path.abspath(os.path.expanduser(a.root or os.environ.get("SYSTEM_DESIGN_OUT") or "~/.system-design"))
    packet = os.path.join(root, rslug, "system-design", a.slug)
    if os.path.isdir(packet) and os.listdir(packet):
        sys.exit(f"{packet}: a packet already exists there; refusing to overwrite it (pick another slug, or edit that packet)")
    if os.path.exists(packet) and not os.path.isdir(packet):
        sys.exit(f"{packet}: exists and is not a folder")

    ref = "origin/HEAD" if git(repo, "rev-parse", "--verify", "-q", "origin/HEAD") else "HEAD"
    full = git(repo, "rev-parse", "--verify", "-q", ref + "^{commit}")
    sha = full[:10] if full else None
    sha_date = git(repo, "show", "-s", "--format=%cs", full) if full else None
    author = git(repo, "config", "user.name")
    today = os.environ.get("TDD_TODAY") or datetime.date.today().isoformat()
    title = (a.title or "").strip() or "<Title: the finding, not the topic>"
    ticket = (a.ticket or "").strip()

    meta = json.load(open(TEMPLATE_META, encoding="utf-8"))
    meta.pop("_doc", None)
    meta.update({
        "title": tab_title((a.title or "").strip(), a.slug), "h1": None,
        "project": {"label": ticket, "href": None} if ticket else None,
        "people": [{"role": "Author", "name": author or "<author>"}],
        "updated": today, "rev": 1, "rev_note": "first draft", "status": "Draft for review", "status_tone": None, "footer": None,
        "repo_url": web_url(origin), "sha": sha, "sha_date": sha_date, "repo_path": tilde(repo),
        "path_aliases": {}, "refs": {}, "artifact_url": None, "notion_url": None,
    })

    fill = {"{{title}}": title, "{{ticket}}": ticket or "(none named)", "{{sha}}": sha or "(no commit recorded)",
            "{{sha_date}}": sha_date or "no date", "{{slug}}": a.slug, "{{date}}": today}
    md = open(TEMPLATE_MD, encoding="utf-8").read()
    for k, v in fill.items():
        md = md.replace(k, v)

    evidence = (f"evidence read at `{sha}` ({ref} of `{tilde(repo)}`, committed {sha_date})" if sha
                else f"no commit recorded (`{tilde(repo)}` is not a git repo with a commit); record the sha in meta.json before citing code")
    status = f"""# {title}: status (the one living doc)

artifact_url: (not published yet)
notion_url: (not mirrored yet)

Slug `{a.slug}`. Ticket {ticket or "(none named)"}. Started {today}{" by " + author if author else ""}. Packet `{tilde(packet)}` (tdd skill). Repo `{tilde(repo)}` (repo slug `{rslug}`); {evidence}. meta.json carries the same sha and date.

## Reference
- (the ticket, the PRD, threads and earlier designs this TDD answers to: each with its link and the date read)

## Surfaces
- (readers, writers and screens: what the tools found and what the hand map found, reconciled, as path:line at the sha above)

## Counts
- (each count: the number, what it counts, `queries/<name>.sql`, where it ran, the date it ran; or "not measured")

## Decisions (the owner's words)
- (none yet. Each answer gets the next D-number: D<n> (YYYY-MM-DD, who): "<the owner's words>", then one line on what it means for the design. Never re-ask a recorded one.)

## Open questions
- (Q<n>: one line each, asked one at a time; renamed to the next D-number when answered)

## Lead defaults (not rulings)
- (defaults and tool picks made without the owner, labelled as such; the owner can overrule any)

## Revisions
- rev 1 ({today}): first draft, not published.

## Tracker
- none yet
"""

    os.makedirs(packet, exist_ok=True)
    for d in DIRS:
        os.makedirs(os.path.join(packet, d), exist_ok=True)
    placeholders = packet_files()
    for rel in placeholders:
        dest = os.path.join(packet, *rel.split("/"))
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(os.path.join(TEMPLATE_PACKET, *rel.split("/")), "rb") as src, open(dest, "wb") as f:
            f.write(src.read())
    with open(os.path.join(packet, f"{a.slug}.md"), "w", encoding="utf-8") as f:
        f.write(md)
    with open(os.path.join(packet, "meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")
    with open(os.path.join(packet, "status.md"), "w", encoding="utf-8") as f:
        f.write(status)

    print(f"packet: {packet}")
    print(f"repo:   {tilde(repo)} (repo slug {rslug}; origin {origin or 'none'}; repo_url {meta['repo_url'] or 'none'})")
    print(f"sha:    {sha + ' (' + ref + ', committed ' + str(sha_date) + ')' if sha else 'none recorded'}")
    print(f"wrote:  {a.slug}.md, meta.json, status.md; folders {', '.join(d + '/' for d in DIRS)}")
    if placeholders:
        print(f"todo:   replace the placeholder images before publishing: {', '.join(placeholders)}")
    print(f"next:   python3 -I {tilde(os.path.join(HERE, 'build_tdd.py'))} {tilde(os.path.join(packet, a.slug + '.md'))} {tilde(os.path.join(packet, 'build'))}")

if __name__ == "__main__":
    main(sys.argv[1:])
