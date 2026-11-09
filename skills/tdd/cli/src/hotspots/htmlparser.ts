// A port of CPython 3.13's html.parser.HTMLParser event loop (convert_charrefs=True, scripting=False), so a
// subclass sees the same handle_* calls with the same arguments as its Python twin (R19). Indices are UTF-16 offsets;
// every delimiter the parser looks for is ASCII, so the substrings it hands out are the same as Python's. The one
// place Python counts code points (the 34-character look-back for a split charref) does so here too.
import { strip, unescape, unescapeAttr } from "./_py";

const STARTTAGOPEN = /<[a-zA-Z]/y;
const ENDTAGOPEN = /<\/[a-zA-Z]/y;
const COMMENTCLOSE = /--!?>/g;
const COMMENTABRUPTCLOSE = /-?>/y;
const TAGFIND = /([a-zA-Z][^\t\n\r\f />]*)(?:[\t\n\r\f ]|\/(?!>))*/uy;
const ATTRFIND = new RegExp(
  String.raw`((?<=['"\t\n\r\f /])[^\t\n\r\f />][^\t\n\r\f /=>]*)` +
  String.raw`([\t\n\r\f ]*=[\t\n\r\f ]*('[^']*'|"[^"]*"|(?!['"])[^>\t\n\r\f ]*))?` +
  String.raw`(?:[\t\n\r\f ]|\/(?!>))*`, "uy");
const LOCATETAGEND = new RegExp(
  String.raw`[a-zA-Z][^\t\n\r\f />]*[\t\n\r\f /]*` +
  String.raw`(?:(?<=['"\t\n\r\f /])[^\t\n\r\f />][^\t\n\r\f /=>]*` +
  String.raw`(?:[\t\n\r\f ]*=[\t\n\r\f ]*(?:'[^']*'|"[^"]*"|(?!['"])[^>\t\n\r\f ]*))?` +
  String.raw`[\t\n\r\f /]*)*>?`, "uy");
const CHARREF_TAIL = /[\t\n\r\f ;]/;
const CDATA_CONTENT_ELEMENTS = new Set(["script", "style", "xmp", "iframe", "noembed", "noframes"]);
const RCDATA_CONTENT_ELEMENTS = new Set(["textarea", "title"]);

function matchAt(re: RegExp, s: string, pos: number): RegExpExecArray | null {
  re.lastIndex = pos;
  return re.exec(s);
}
function searchFrom(re: RegExp, s: string, pos: number): RegExpExecArray | null {
  re.lastIndex = pos;
  return re.exec(s);
}
/** max(i, n - 34) with n - 34 counted in code points, as a UTF-16 offset. */
function lookbackStart(s: string, i: number, cps: number): number {
  let j = s.length;
  for (let k = 0; k < cps; k++) {
    if (j <= 0) return i;
    j -= j >= 2 && /[\udc00-\udfff]/.test(s[j - 1]!) && /[\ud800-\udbff]/.test(s[j - 2]!) ? 2 : 1;
  }
  return Math.max(i, j);
}

export type Attrs = [string, string | null][];

export class HTMLParser {
  rawdata = "";
  lasttag = "???";
  private interesting: RegExp | null = null;
  private cdataElem: string | null = null;
  private escapable = true;
  private supportCdata = true;
  private pending: string[] = [];
  private pendingLen = 0;
  private parseThreshold = 1;

  feed(data: string): void {
    this.pendingLen += data.length;
    if (this.pendingLen < this.parseThreshold) { this.pending.push(data); return; }
    if (!this.pending.length) this.rawdata += data;
    else { this.pending.push(data); this.rawdata += this.pending.join(""); this.pending = []; }
    this.pendingLen = 0;
    const n = this.rawdata.length;
    this.goahead(false);
    this.parseThreshold = this.rawdata.length < n ? 1 : this.rawdata.length;
  }
  close(): void {
    if (this.pending.length) { this.rawdata += this.pending.join(""); this.pending = []; this.pendingLen = 0; }
    this.goahead(true);
  }

  // Overridable handlers (html.parser defaults).
  handle_startendtag(tag: string, attrs: Attrs): void { this.handle_starttag(tag, attrs); this.handle_endtag(tag); }
  handle_starttag(_tag: string, _attrs: Attrs): void {}
  handle_endtag(_tag: string): void {}
  handle_data(_data: string): void {}
  handle_comment(_data: string): void {}
  handle_decl(_decl: string): void {}
  handle_pi(_data: string): void {}
  unknown_decl(_data: string): void {}

  private setCdataMode(elem: string, escapable: boolean): void {
    this.cdataElem = elem.toLowerCase();
    this.escapable = escapable;
    // convert_charrefs is True: the escapable '&' alternative never applies.
    this.interesting = this.cdataElem === "plaintext" ? /$(?![\s\S])/g
      : new RegExp(`</${this.cdataElem.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?=[\\t\\n\\r\\f />])`, "gi");
  }
  private clearCdataMode(): void { this.interesting = null; this.cdataElem = null; this.escapable = true; }

  private goahead(end: boolean): void {
    const rawdata = this.rawdata;
    let i = 0;
    const n = rawdata.length;
    while (i < n) {
      let j: number;
      if (!this.cdataElem) {
        j = rawdata.indexOf("<", i);
        if (j < 0) {
          const from = lookbackStart(rawdata, i, 34);
          const amppos = rawdata.slice(from).lastIndexOf("&");
          if (amppos >= 0 && !CHARREF_TAIL.test(rawdata.slice(from + amppos))) break;
          j = n;
        }
      } else {
        const m = searchFrom(this.interesting!, rawdata, i);
        if (m) j = m.index;
        else { if (this.cdataElem) break; j = n; }
      }
      if (i < j) this.handle_data(this.escapable ? unescape(rawdata.slice(i, j)) : rawdata.slice(i, j));
      i = j;
      if (i === n) break;
      if (!rawdata.startsWith("<", i)) throw new Error("interesting.search() lied");
      let k: number;
      if (matchAt(STARTTAGOPEN, rawdata, i)) k = this.parseStarttag(i);
      else if (rawdata.startsWith("</", i)) k = this.parseEndtag(i);
      else if (rawdata.startsWith("<!--", i)) k = this.parseComment(i);
      else if (rawdata.startsWith("<?", i)) k = this.parsePi(i);
      else if (rawdata.startsWith("<!", i)) k = this.parseHtmlDeclaration(i);
      else if (i + 1 < n || end) { this.handle_data("<"); k = i + 1; }
      else break;
      if (k < 0) {
        if (!end) break;
        if (matchAt(STARTTAGOPEN, rawdata, i)) { /* incomplete start tag: dropped */ }
        else if (rawdata.startsWith("</", i)) {
          if (i + 2 === n) this.handle_data("</");
          else if (matchAt(ENDTAGOPEN, rawdata, i)) { /* incomplete end tag: dropped */ }
          else this.handle_comment(rawdata.slice(i + 2));
        } else if (rawdata.startsWith("<!--", i)) {
          let jj = n;
          for (const suffix of ["--!", "--", "-"]) {
            if (rawdata.length - suffix.length >= i + 4 && rawdata.endsWith(suffix)) { jj -= suffix.length; break; }
          }
          this.handle_comment(rawdata.slice(i + 4, jj));
        } else if (rawdata.startsWith("<![CDATA[", i) && this.supportCdata) this.unknown_decl(rawdata.slice(i + 3));
        else if (rawdata.slice(i, i + 9).toLowerCase() === "<!doctype") this.handle_decl(rawdata.slice(i + 2));
        else if (rawdata.startsWith("<!", i)) this.handle_comment(rawdata.slice(i + 2));
        else if (rawdata.startsWith("<?", i)) this.handle_pi(rawdata.slice(i + 2));
        else throw new Error("we should not get here!");
        k = n;
      }
      i = k;
    }
    if (end && i < n) {
      this.handle_data(this.escapable ? unescape(rawdata.slice(i, n)) : rawdata.slice(i, n));
      i = n;
    }
    this.rawdata = rawdata.slice(i);
  }

  private parseHtmlDeclaration(i: number): number {
    const rawdata = this.rawdata;
    if (rawdata.startsWith("<!--", i)) return this.parseComment(i);
    if (rawdata.startsWith("<![CDATA[", i) && this.supportCdata) {
      const j = rawdata.indexOf("]]>", i + 9);
      if (j < 0) return -1;
      this.unknown_decl(rawdata.slice(i + 3, j));
      return j + 3;
    }
    if (rawdata.slice(i, i + 9).toLowerCase() === "<!doctype") {
      const gt = rawdata.indexOf(">", i + 9);
      if (gt < 0) return -1;
      this.handle_decl(rawdata.slice(i + 2, gt));
      return gt + 1;
    }
    return this.parseBogusComment(i);
  }
  private parseComment(i: number): number {
    const rawdata = this.rawdata;
    let start: number, endIdx: number;
    const abrupt = matchAt(COMMENTABRUPTCLOSE, rawdata, i + 4);
    if (abrupt) { start = abrupt.index; endIdx = COMMENTABRUPTCLOSE.lastIndex; }
    else {
      const m = searchFrom(COMMENTCLOSE, rawdata, i + 4);
      if (!m) return -1;
      start = m.index; endIdx = COMMENTCLOSE.lastIndex;
    }
    this.handle_comment(rawdata.slice(i + 4, start));
    return endIdx;
  }
  private parseBogusComment(i: number): number {
    const pos = this.rawdata.indexOf(">", i + 2);
    if (pos < 0) return -1;
    this.handle_comment(this.rawdata.slice(i + 2, pos));
    return pos + 1;
  }
  private parsePi(i: number): number {
    const j = this.rawdata.indexOf(">", i + 2);
    if (j < 0) return -1;
    this.handle_pi(this.rawdata.slice(i + 2, j));
    return j + 1;
  }
  private checkForWholeStartTag(i: number): number {
    matchAt(LOCATETAGEND, this.rawdata, i + 1);
    const j = LOCATETAGEND.lastIndex;
    return this.rawdata[j - 1] !== ">" ? -1 : j;
  }
  private parseStarttag(i: number): number {
    const endpos = this.checkForWholeStartTag(i);
    if (endpos < 0) return endpos;
    const rawdata = this.rawdata;
    const attrs: Attrs = [];
    const tm = matchAt(TAGFIND, rawdata, i + 1)!;
    let k = TAGFIND.lastIndex;
    const tag = (this.lasttag = tm[1]!.toLowerCase());
    while (k < endpos) {
      const m = matchAt(ATTRFIND, rawdata, k);
      if (!m) break;
      const attrname = m[1]!, rest = m[2];
      let value: string | null = m[3] ?? null;
      if (!rest) value = null;
      else if (value !== null && value.length >= 1 &&
        ((value[0] === "'" && value[value.length - 1] === "'") || (value[0] === '"' && value[value.length - 1] === '"'))) {
        value = value.slice(1, -1);
      }
      if (value) value = unescapeAttr(value);
      attrs.push([attrname.toLowerCase(), value]);
      k = ATTRFIND.lastIndex;
    }
    const endText = strip(rawdata.slice(k, endpos));
    if (endText !== ">" && endText !== "/>") {
      this.handle_data(rawdata.slice(i, endpos));
      return endpos;
    }
    if (endText.endsWith("/>")) this.handle_startendtag(tag, attrs);
    else {
      this.handle_starttag(tag, attrs);
      if (CDATA_CONTENT_ELEMENTS.has(tag) || tag === "plaintext") this.setCdataMode(tag, false);
      else if (RCDATA_CONTENT_ELEMENTS.has(tag)) this.setCdataMode(tag, true);
    }
    return endpos;
  }
  private parseEndtag(i: number): number {
    const rawdata = this.rawdata;
    if (rawdata.indexOf(">", i + 2) < 0) return -1;
    if (!matchAt(ENDTAGOPEN, rawdata, i)) {
      if (rawdata.slice(i + 2, i + 3) === ">") return i + 3;
      return this.parseBogusComment(i);
    }
    matchAt(LOCATETAGEND, rawdata, i + 2);
    const j = LOCATETAGEND.lastIndex;
    if (rawdata[j - 1] !== ">") return -1;
    const tm = matchAt(TAGFIND, rawdata, i + 2)!;
    this.handle_endtag(tm[1]!.toLowerCase());
    this.clearCdataMode();
    return j;
  }
}
