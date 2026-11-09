// build_doc.py:26 and 410-483: FILE_LIMIT, svg_size, png_size, img_size, Assets, published_for, on the kernel.
// build_tdd.py builds Assets with inline=False, so the data: URL branch never runs in a compile; it is kept (with
// Python's default mimetypes table, see mimeOf) so the class has the same API.
import { closeSync, mkdirSync, openSync, readSync, statSync } from "node:fs";
import * as py from "../py";
import { PyError } from "../py/errors";
import { float } from "../py/num";
import { SameFileError, StructError } from "./errors";

const re = py.re;
const { abspath, basename, dirname, isabs, join } = py.path;

/** Artifact publish limit per binary file (build_doc.py:26). */
export const FILE_LIMIT = 15 * 1024 * 1024;

/** An OSError from the file system (Node's errors carry a string `code`); anything else is a bug and propagates. */
const isOSError = (e: unknown): boolean => (e instanceof PyError && (e.type === "OSError" || e.type === "FileNotFoundError"))
  || (typeof e === "object" && e !== null && typeof (e as { code?: unknown }).code === "string" && "syscall" in e);

/** open(path, "rb").read(n); a NUL in the path is CPython's ValueError (not an OSError, so svg_size lets it through). */
function readHead(path: string, n: number): Uint8Array {
  if (path.includes("\0")) throw new PyError("ValueError", "embedded null byte");
  const fd = openSync(path, "r");
  try {
    const buf = new Uint8Array(n);
    let got = 0;
    while (got < n) {
      const k = readSync(fd, buf, got, n - got, null);
      if (k === 0) break;
      got += k;
    }
    return buf.subarray(0, got);
  } finally {
    closeSync(fd);
  }
}

const SVG_TAG = re.compile(String.raw`<svg\b[^>]*>`, re.S);
const WIDTH = re.compile(String.raw`\bwidth="([\d.]+)(px)?"`);
const HEIGHT = re.compile(String.raw`\bheight="([\d.]+)(px)?"`);
const VIEWBOX = re.compile(String.raw`viewBox="[\d.\-]+[ ,]+[\d.\-]+[ ,]+([\d.]+)[ ,]+([\d.]+)"`);

/** round(float(s)): float() takes Unicode digits and raises ValueError on "1.2.3"; round() is half-even. */
const roundFloat = (s: string): number => py.roundInt(float(s));

/** svg_size(path): [w, h] from the root <svg>'s width/height, else its viewBox; null when there is neither. */
export function svgSize(path: string): [number, number] | null {
  let head: string;
  try {
    head = py.io.decodeUtf8(readHead(path, 4096), "ignore");
  } catch (e) {
    if (isOSError(e)) return null;
    throw e;
  }
  const tag = SVG_TAG.search(head);
  if (!tag) return null;
  const t = tag.group(0)!;
  const w = WIDTH.search(t);
  const h = HEIGHT.search(t);
  if (w && h) return [roundFloat(w.group(1)!), roundFloat(h.group(1)!)];
  const vb = VIEWBOX.search(t);
  return vb ? [roundFloat(vb.group(1)!), roundFloat(vb.group(2)!)] : null;
}

const PNG_SIG = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

/** png_size(path): the IHDR width and height of a PNG; null for anything else or an unreadable file. */
export function pngSize(path: string): [number, number] | null {
  let head: Uint8Array;
  try {
    head = readHead(path, 24);
  } catch (e) {
    if (isOSError(e)) return null;
    throw e;
  }
  if (head.length >= 8 && PNG_SIG.every((b, i) => head[i] === b)) {
    if (head.length < 24) throw new StructError("unpack requires a buffer of 8 bytes");
    const v = new DataView(head.buffer, head.byteOffset + 16, 8);
    return [v.getUint32(0, false), v.getUint32(4, false)];
  }
  return null;
}

/** img_size(path): by extension (any case), .svg or .png; null otherwise or for an empty path. */
export function imgSize(path: string | null | undefined): [number, number] | null {
  if (!path) return null;
  const low = py.lower(path);
  if (low.endsWith(".svg")) return svgSize(path);
  if (low.endsWith(".png")) return pngSize(path);
  return null;
}

// mimetypes.guess_type(src)[0] with CPython 3.13's built-in table (mimetypes.MimeTypes(filenames=()).types_map[True]).
// Known limit: CPython also reads the host's mime.types files, and on macOS /etc/apache2/mime.types changes common
// extensions (.ico is image/x-icon there, .xml application/xml; .woff, .woff2, .ttf, .otf, .ogg, .ogv, .m4v and .jxl
// get a type where this table has none). Only Assets(inline=true) reads it, and build_tdd never inlines.
const TYPES: ReadonlyMap<string, string> = new Map(Object.entries({
  ".js": "text/javascript", ".mjs": "text/javascript", ".json": "application/json",
  ".webmanifest": "application/manifest+json", ".doc": "application/msword", ".dot": "application/msword",
  ".wiz": "application/msword", ".nq": "application/n-quads", ".nt": "application/n-triples",
  ".bin": "application/octet-stream", ".a": "application/octet-stream", ".dll": "application/octet-stream",
  ".exe": "application/octet-stream", ".o": "application/octet-stream", ".obj": "application/octet-stream",
  ".so": "application/octet-stream", ".oda": "application/oda", ".pdf": "application/pdf",
  ".p7c": "application/pkcs7-mime", ".ps": "application/postscript", ".ai": "application/postscript",
  ".eps": "application/postscript", ".trig": "application/trig", ".m3u": "application/vnd.apple.mpegurl",
  ".m3u8": "application/vnd.apple.mpegurl", ".xls": "application/vnd.ms-excel", ".xlb": "application/vnd.ms-excel",
  ".ppt": "application/vnd.ms-powerpoint", ".pot": "application/vnd.ms-powerpoint",
  ".ppa": "application/vnd.ms-powerpoint", ".pps": "application/vnd.ms-powerpoint",
  ".pwz": "application/vnd.ms-powerpoint", ".wasm": "application/wasm", ".bcpio": "application/x-bcpio",
  ".cpio": "application/x-cpio", ".csh": "application/x-csh", ".dvi": "application/x-dvi",
  ".gtar": "application/x-gtar", ".hdf": "application/x-hdf", ".h5": "application/x-hdf5",
  ".latex": "application/x-latex", ".mif": "application/x-mif", ".cdf": "application/x-netcdf",
  ".nc": "application/x-netcdf", ".p12": "application/x-pkcs12", ".pfx": "application/x-pkcs12",
  ".ram": "application/x-pn-realaudio", ".pyc": "application/x-python-code", ".pyo": "application/x-python-code",
  ".sh": "application/x-sh", ".shar": "application/x-shar", ".swf": "application/x-shockwave-flash",
  ".sv4cpio": "application/x-sv4cpio", ".sv4crc": "application/x-sv4crc", ".tar": "application/x-tar",
  ".tcl": "application/x-tcl", ".tex": "application/x-tex", ".texi": "application/x-texinfo",
  ".texinfo": "application/x-texinfo", ".roff": "application/x-troff", ".t": "application/x-troff",
  ".tr": "application/x-troff", ".man": "application/x-troff-man", ".me": "application/x-troff-me",
  ".ms": "application/x-troff-ms", ".ustar": "application/x-ustar", ".src": "application/x-wais-source",
  ".xsl": "application/xml", ".rdf": "application/xml", ".wsdl": "application/xml", ".xpdl": "application/xml",
  ".zip": "application/zip", ".3gp": "audio/3gpp", ".3gpp": "audio/3gpp", ".3g2": "audio/3gpp2",
  ".3gpp2": "audio/3gpp2", ".aac": "audio/aac", ".adts": "audio/aac", ".loas": "audio/aac", ".ass": "audio/aac",
  ".au": "audio/basic", ".snd": "audio/basic", ".mp3": "audio/mpeg", ".mp2": "audio/mpeg", ".opus": "audio/opus",
  ".aif": "audio/x-aiff", ".aifc": "audio/x-aiff", ".aiff": "audio/x-aiff", ".ra": "audio/x-pn-realaudio",
  ".wav": "audio/x-wav", ".avif": "image/avif", ".bmp": "image/bmp", ".gif": "image/gif", ".ief": "image/ief",
  ".jpg": "image/jpeg", ".jpe": "image/jpeg", ".jpeg": "image/jpeg", ".heic": "image/heic", ".heif": "image/heif",
  ".png": "image/png", ".svg": "image/svg+xml", ".tiff": "image/tiff", ".tif": "image/tiff",
  ".ico": "image/vnd.microsoft.icon", ".webp": "image/webp", ".ras": "image/x-cmu-raster",
  ".pnm": "image/x-portable-anymap", ".pbm": "image/x-portable-bitmap", ".pgm": "image/x-portable-graymap",
  ".ppm": "image/x-portable-pixmap", ".rgb": "image/x-rgb", ".xbm": "image/x-xbitmap", ".xpm": "image/x-xpixmap",
  ".xwd": "image/x-xwindowdump", ".eml": "message/rfc822", ".mht": "message/rfc822", ".mhtml": "message/rfc822",
  ".nws": "message/rfc822", ".css": "text/css", ".csv": "text/csv", ".html": "text/html", ".htm": "text/html",
  ".md": "text/markdown", ".markdown": "text/markdown", ".n3": "text/n3", ".txt": "text/plain", ".bat": "text/plain",
  ".c": "text/plain", ".h": "text/plain", ".ksh": "text/plain", ".pl": "text/plain", ".srt": "text/plain",
  ".rtx": "text/richtext", ".rtf": "text/rtf", ".tsv": "text/tab-separated-values", ".vtt": "text/vtt",
  ".py": "text/x-python", ".rst": "text/x-rst", ".etx": "text/x-setext", ".sgm": "text/x-sgml", ".sgml": "text/x-sgml",
  ".vcf": "text/x-vcard", ".xml": "text/xml", ".mp4": "video/mp4", ".mpeg": "video/mpeg", ".m1v": "video/mpeg",
  ".mpa": "video/mpeg", ".mpe": "video/mpeg", ".mpg": "video/mpeg", ".mov": "video/quicktime",
  ".qt": "video/quicktime", ".webm": "video/webm", ".avi": "video/x-msvideo", ".movie": "video/x-sgi-movie",
}));
const SUFFIXES: ReadonlyMap<string, string> = new Map([[".svgz", ".svg.gz"], [".tgz", ".tar.gz"], [".taz", ".tar.gz"], [".tz", ".tar.gz"],
  [".tbz2", ".tar.bz2"], [".txz", ".tar.xz"]]);
const ENCODINGS = new Set([".gz", ".Z", ".bz2", ".xz", ".br"]);

/** mimetypes.guess_type(path)[0] on the built-in table (see TYPES). */
export function mimeOf(path: string): string | null {
  let [base, ext] = py.path.splitext(path);
  while (SUFFIXES.has(py.lower(ext))) [base, ext] = py.path.splitext(base + SUFFIXES.get(py.lower(ext))!);
  if (ENCODINGS.has(ext)) [base, ext] = py.path.splitext(base); // case-sensitive, as in CPython
  return TYPES.get(py.lower(ext)) ?? null;
}

/** os.path.samefile(a, b): the same device and inode (symlinks followed); false when either cannot be stat'ed. */
function samefile(a: string, b: string): boolean {
  try {
    const x = statSync(a, { bigint: true });
    const y = statSync(b, { bigint: true });
    return x.ino === y.ino && x.dev === y.dev;
  } catch {
    return false;
  }
}

/** shutil.copy2(src, dst): into dst/basename(src) when dst is a folder; SameFileError when both name one file. */
export function copy2(src: string, dst: string): string {
  if (py.io.isdir(dst)) dst = join(dst, basename(src));
  if (samefile(src, dst)) throw new SameFileError(`${py.strRepr(src)} and ${py.strRepr(dst)} are the same file`);
  py.io.copy2(src, dst);
  return dst;
}

/** Every file the page references: published path -> source path. Copies into the output folder at the end. */
export class Assets {
  out_dir: string;
  inline: boolean;
  copy: boolean;
  /** published -> resolved source (null when no candidate exists), a Python dict. */
  files = new Map<string, string | null>();
  missing: string[] = [];
  order: string[] = [];

  constructor(out_dir: string, inline: boolean, copy: boolean) {
    this.out_dir = out_dir;
    this.inline = inline;
    this.copy = copy;
  }

  /** Record a reference; return the URL to write in the markup and the resolved source (or null). */
  ref(published: string, candidates: readonly (string | null | undefined)[], embed = false): [string, string | null] {
    // published.replace(os.sep, "/") is the identity on POSIX
    if (!published.startsWith("../")) published = py.str.lstrip(published, "./");
    let src: string | null = null;
    for (const c of candidates) {
      if (c && py.io.isfile(c)) {
        src = abspath(c);
        break;
      }
    }
    if (!this.files.has(published)) this.order.push(published);
    this.files.set(published, src);
    if (src === null && !this.missing.includes(published)) this.missing.push(published);
    if (embed && this.inline && src) {
      const mime = src.endsWith(".svg") ? "image/svg+xml" : (mimeOf(src) || "application/octet-stream");
      return ["data:" + mime + ";base64," + Buffer.from(py.io.readBytes(src)).toString("base64"), src];
    }
    return [published, src];
  }

  /** published -> the path to publish from: the copy in out_dir (copy=true) or the source itself. */
  finish(): Map<string, string> {
    const out = new Map<string, string>();
    for (const p of this.order) {
      const src = this.files.get(p) ?? null;
      if (src === null) continue;
      const dest = join(this.out_dir, p);
      if (this.copy && abspath(dest) !== src) {
        mkdirSync(dirname(dest), { recursive: true });
        copy2(src, dest);
        out.set(p, abspath(dest));
      } else out.set(p, src);
    }
    return out;
  }
}

/** published_for(path, prefix): keep a clean relative path, else <prefix>/<basename>. */
export function publishedFor(path: string, prefix: string): string {
  if (isabs(path) || path.startsWith("..")) return `${prefix}/${basename(path)}`;
  return path; // path.replace(os.sep, "/") is the identity on POSIX
}
