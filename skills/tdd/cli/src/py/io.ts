// File I/O with Python's text-mode semantics: strict UTF-8 (CPython's error text), universal newlines on read
// (\r\n and \r become \n; a BOM is kept as U+FEFF), no translation on write, and shutil.copy2 / sorted listdir.
import { createHash } from "node:crypto";
import { chmodSync, copyFileSync, readdirSync, readFileSync, statSync, utimesSync, writeFileSync } from "node:fs";
import { PyError } from "./errors";
import { join } from "./path";
import { strRepr } from "./repr";
import { cmp } from "./str";

export type Errors = "strict" | "ignore" | "replace";

/** bytes.decode("utf-8", errors): the WHATWG/CPython maximal-subpart rule, with CPython's UnicodeDecodeError text. */
export function decodeUtf8(b: Uint8Array, errors: Errors = "strict"): string {
  const parts: string[] = [];
  let run: number[] = [];
  const flush = () => {
    if (run.length) {
      parts.push(String.fromCodePoint(...run));
      run = [];
    }
  };
  const n = b.length;
  let i = 0;
  while (i < n) {
    const c = b[i]!;
    if (c < 0x80) {
      run.push(c);
      i++;
      if (run.length > 4096) flush();
      continue;
    }
    let need = 0;
    let lo = 0x80;
    let hi = 0xbf;
    let cp = 0;
    if (c >= 0xc2 && c <= 0xdf) {
      need = 1;
      cp = c & 0x1f;
    } else if (c >= 0xe0 && c <= 0xef) {
      need = 2;
      cp = c & 0x0f;
      if (c === 0xe0) lo = 0xa0;
      if (c === 0xed) hi = 0x9f;
    } else if (c >= 0xf0 && c <= 0xf4) {
      need = 3;
      cp = c & 0x07;
      if (c === 0xf0) lo = 0x90;
      if (c === 0xf4) hi = 0x8f;
    }
    let reason = "invalid start byte";
    let end = i + 1;
    let ok = need > 0;
    if (ok) {
      for (let k = 1; k <= need; k++) {
        if (i + k >= n) {
          ok = false;
          reason = "unexpected end of data";
          end = i + k;
          break;
        }
        const d = b[i + k]!;
        if (d < (k === 1 ? lo : 0x80) || d > (k === 1 ? hi : 0xbf)) {
          ok = false;
          reason = "invalid continuation byte";
          end = i + k;
          break;
        }
        cp = (cp << 6) | (d & 0x3f);
      }
    }
    if (ok) {
      run.push(cp);
      i += need + 1;
      continue;
    }
    if (errors === "strict") {
      const span = end - i > 1 ? `bytes in position ${i}-${end - 1}` : `byte 0x${c.toString(16).padStart(2, "0")} in position ${i}`;
      throw new PyError("UnicodeDecodeError", `'utf-8' codec can't decode ${span}: ${reason}`);
    }
    if (errors === "replace") run.push(0xfffd);
    i = end;
  }
  flush();
  return parts.join("");
}

/** Python text mode's universal newlines: \r\n and lone \r become \n. */
export function universalNewlines(s: string): string {
  return s.includes("\r") ? s.replace(/\r\n?/g, "\n") : s;
}

/** open(path, encoding="utf-8").read() */
export function readText(path: string): string {
  return universalNewlines(decodeUtf8(readBytes(path)));
}

/** open(path, "rb").read(); a missing file is CPython's FileNotFoundError. */
export function readBytes(path: string): Uint8Array {
  try {
    return readFileSync(path);
  } catch (e) {
    if ((e as { code?: string }).code === "ENOENT") throw new PyError("FileNotFoundError", `[Errno 2] No such file or directory: ${strRepr(path)}`);
    throw e;
  }
}

/** open(path, "w", encoding="utf-8").write(s): no newline translation on POSIX; a lone surrogate is an error. */
export function writeText(path: string, s: string): void {
  const lone = s.search(/[\ud800-\udfff]/u);
  if (lone >= 0) {
    const pos = [...s.slice(0, lone)].length;
    throw new PyError("UnicodeEncodeError", `'utf-8' codec can't encode character '\\u${s.charCodeAt(lone).toString(16)}' in position ${pos}: surrogates not allowed`);
  }
  writeFileSync(path, s);
}

/** shutil.copy2(src, dst): the bytes, the permission bits and the access and modification times. */
export function copy2(src: string, dst: string): void {
  copyFileSync(src, dst);
  const st = statSync(src);
  chmodSync(dst, st.mode & 0o7777);
  utimesSync(dst, st.atime, st.mtime);
}

/** sorted(os.listdir(d)): code point order. */
export function listdirSorted(d: string): string[] {
  return readdirSync(d).sort(cmp);
}

/** os.listdir(d), in the file system's order. */
export function listdir(d: string): string[] {
  return readdirSync(d);
}

/** sorted(glob.glob(os.path.join(d, "*" + suffix))): names not starting with "."; [] when d is missing. */
export function globSorted(d: string, suffix: string): string[] {
  let names: string[];
  try {
    names = readdirSync(d);
  } catch {
    return [];
  }
  return names.filter((n) => !n.startsWith(".") && n.endsWith(suffix)).sort(cmp).map((n) => join(d, n));
}

/** hashlib.sha256(b).hexdigest(); a str is hashed as UTF-8. */
export function sha256Hex(b: Uint8Array | string): string {
  return createHash("sha256").update(b).digest("hex");
}

/** len(s.encode("utf-8")); a lone surrogate is CPython's UnicodeEncodeError. */
export function utf8Len(s: string): number {
  const lone = s.search(/[\ud800-\udfff]/u);
  if (lone >= 0) {
    const pos = [...s.slice(0, lone)].length;
    throw new PyError("UnicodeEncodeError", `'utf-8' codec can't encode character '\\u${s.charCodeAt(lone).toString(16)}' in position ${pos}: surrogates not allowed`);
  }
  return Buffer.byteLength(s, "utf8");
}

/** os.path.isfile(p) (follows symlinks; false on any error). */
export function isfile(p: string): boolean {
  try {
    return statSync(p).isFile();
  } catch {
    return false;
  }
}

/** os.path.isdir(p) */
export function isdir(p: string): boolean {
  try {
    return statSync(p).isDirectory();
  } catch {
    return false;
  }
}

/** os.path.exists(p) */
export function exists(p: string): boolean {
  try {
    statSync(p);
    return true;
  } catch {
    return false;
  }
}

/** os.path.getsize(p) */
export function getsize(p: string): number {
  return statSync(p).size;
}

/** os.path.getmtime(p), seconds as a float. */
export function getmtime(p: string): number {
  return statSync(p).mtimeMs / 1000;
}
