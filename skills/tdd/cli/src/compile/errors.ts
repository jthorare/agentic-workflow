// The exceptions the compiler surfaces as CPython would. sys.exit(message): the bin prints the message on stderr and
// exits with the code (1); any error whose name is "SystemExit" (src/sd raises its own from loadJson) is the same.
// OSError: CPython's subclass by errno and its "[Errno N] strerror: 'path'" text, for the out folder and file writes.
import { mkdirSync } from "node:fs";
import * as py from "../py";

export class SystemExit extends Error {
  override readonly name = "SystemExit";
  constructor(readonly code: number, msg = "") {
    super(msg);
  }
}

export const isSystemExit = (e: unknown): e is { code?: number; message: string } =>
  typeof e === "object" && e !== null && (e as { name?: unknown }).name === "SystemExit";

// errno -> [number on this platform, strerror, CPython's OSError subclass]; macOS strerror(3) texts
const ERRNO: Record<string, [number, string, string]> = {
  EPERM: [1, "Operation not permitted", "PermissionError"],
  ENOENT: [2, "No such file or directory", "FileNotFoundError"],
  EIO: [5, "Input/output error", "OSError"],
  EACCES: [13, "Permission denied", "PermissionError"],
  EEXIST: [17, "File exists", "FileExistsError"],
  ENOTDIR: [20, "Not a directory", "NotADirectoryError"],
  EISDIR: [21, "Is a directory", "IsADirectoryError"],
  EMFILE: [24, "Too many open files", "OSError"],
  ENOSPC: [28, "No space left on device", "OSError"],
  EROFS: [30, "Read-only file system", "OSError"],
  ELOOP: [62, "Too many levels of symbolic links", "OSError"],
  ENAMETOOLONG: [63, "File name too long", "OSError"],
  EDQUOT: [69, "Disc quota exceeded", "OSError"],
};

/** An OSError as CPython raises it from open() / os.mkdir(): `type` is the subclass, message "[Errno N] text: 'path'". */
export class PyOSError extends Error {
  constructor(readonly type: string, readonly errno: number, readonly strerror: string, readonly filename: string) {
    super(`[Errno ${errno}] ${strerror}: ${py.repr(filename)}`);
    this.name = type;
  }
}

/** The PyOSError for a node errno error on `path`; null when the error is not one. */
export function osError(e: unknown, path?: string): PyOSError | null {
  const x = e as { code?: unknown; errno?: unknown; path?: unknown; dest?: unknown; syscall?: unknown } | null;
  if (!x || typeof x.code !== "string" || typeof x.errno !== "number") return null;
  let p = path ?? (x.syscall === "copyfile" && typeof x.dest === "string" ? x.dest : typeof x.path === "string" ? x.path : "");
  // a recursive mkdir: os.makedirs creates the shallowest missing component first, and fails there
  if (path === undefined && x.syscall === "mkdir" && x.code !== "EEXIST") p = shallowestMissing(p);
  const known = ERRNO[x.code];
  return new PyOSError(known ? known[2] : "OSError", known ? known[0] : Math.abs(x.errno), known ? known[1] : x.code, p);
}

function shallowestMissing(p: string): string {
  let cur = p;
  for (;;) {
    const [head, tail] = py.path.split(cur);
    if (!head || !tail || py.io.exists(head)) return cur;
    cur = head;
  }
}

/** os.makedirs(name, exist_ok=exist_ok), CPython's algorithm (so the path in an error is the one CPython names). */
export function makedirs(name: string, exist_ok = false): void {
  let [head, tail] = py.path.split(name);
  if (!tail) [head, tail] = py.path.split(head);
  if (head && tail && !py.io.exists(head)) {
    try {
      makedirs(head, exist_ok);
    } catch (e) {
      if (!(e instanceof PyOSError && e.type === "FileExistsError")) throw e;
    }
    if (tail === ".") return;
  }
  try {
    mkdirSync(name, 0o777);
  } catch (e) {
    if (!exist_ok || !py.io.isdir(name)) throw osError(e, name) ?? e;
  }
}
