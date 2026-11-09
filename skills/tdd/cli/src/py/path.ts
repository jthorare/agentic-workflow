// Python's posixpath (os.path on macOS and Linux): join resets at an absolute part, normpath keeps a leading "//",
// relpath gives "." for the same directory, basename("a/b/") is "", dirname("x") is "", splitext skips leading dots.
import { homedir, userInfo } from "node:os";
import { PyError } from "./errors";

export const sep = "/";

export const isabs = (p: string): boolean => p.startsWith("/");

/** os.path.join(a, *p) */
export function join(a: string, ...rest: string[]): string {
  let path = a;
  for (const b of rest) {
    if (b.startsWith("/")) path = b;
    else if (path === "" || path.endsWith("/")) path += b;
    else path += "/" + b;
  }
  return path;
}

/** os.path.normpath(p) */
export function normpath(p: string): string {
  if (p === "") return ".";
  let initial = p.startsWith("/") ? 1 : 0;
  if (initial && p.startsWith("//") && !p.startsWith("///")) initial = 2;
  const out: string[] = [];
  for (const c of p.split("/")) {
    if (c === "" || c === ".") continue;
    if (c !== ".." || (!initial && out.length === 0) || (out.length > 0 && out[out.length - 1] === "..")) out.push(c);
    else if (out.length > 0) out.pop();
  }
  const s = "/".repeat(initial) + out.join("/");
  return s || ".";
}

/** os.getcwd() */
export const getcwd = (): string => process.cwd();

/** os.path.abspath(p) */
export function abspath(p: string): string {
  return normpath(isabs(p) ? p : join(getcwd(), p));
}

/** os.path.relpath(path, start=os.curdir) */
export function relpath(path: string, start = "."): string {
  if (path === "") throw new PyError("ValueError", "no path specified");
  const s = abspath(start).split("/").filter(Boolean);
  const p = abspath(path).split("/").filter(Boolean);
  let i = 0;
  while (i < s.length && i < p.length && s[i] === p[i]) i++;
  const rel = [...Array<string>(s.length - i).fill(".."), ...p.slice(i)];
  return rel.length ? join(rel[0]!, ...rel.slice(1)) : ".";
}

/** os.path.basename(p) */
export function basename(p: string): string {
  return p.slice(p.lastIndexOf("/") + 1);
}

/** os.path.dirname(p) */
export function dirname(p: string): string {
  let head = p.slice(0, p.lastIndexOf("/") + 1);
  if (head && head !== "/".repeat(head.length)) head = head.replace(/\/+$/, "");
  return head;
}

/** os.path.split(p) -> [dirname, basename] with the same trailing-slash rule as dirname. */
export function split(p: string): [string, string] {
  return [dirname(p), basename(p)];
}

/** os.path.splitext(p) -> [root, ext] */
export function splitext(p: string): [string, string] {
  const sepIndex = p.lastIndexOf("/");
  const dotIndex = p.lastIndexOf(".");
  if (dotIndex > sepIndex) {
    let f = sepIndex + 1;
    while (f < dotIndex) {
      if (p[f] !== ".") return [p.slice(0, dotIndex), p.slice(dotIndex)];
      f++;
    }
  }
  return [p, ""];
}

function home(): string {
  return process.env.HOME !== undefined ? process.env.HOME : homedir();
}

/** os.path.expanduser(p): "~" and "~/..." from $HOME; "~name" only for the current user, else unchanged. */
export function expanduser(p: string): string {
  if (!p.startsWith("~")) return p;
  let i = p.indexOf("/", 1);
  if (i < 0) i = p.length;
  let userhome: string;
  if (i === 1) userhome = home();
  else {
    const name = p.slice(1, i);
    let me: string | null = null;
    try {
      me = userInfo().username;
    } catch {
      me = null;
    }
    if (name !== me) return p;
    userhome = homedir();
  }
  userhome = userhome.replace(/\/+$/, "");
  return userhome + p.slice(i) || "/";
}

/** build_tdd.home_rel: "~/..." instead of an absolute home path; anything else passes through unchanged. */
export function homeRel<T>(p: T): T | string {
  if (typeof p !== "string") return p;
  const h = expanduser("~");
  return p.startsWith(h + "/") || p === h ? "~" + p.slice(h.length) : p;
}
