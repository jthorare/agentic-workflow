// CPython 3.13's html.parser.HTMLParser tokenizer (convert_charrefs=True, scripting=False), event for event.
// strip_page.py builds its tree from these handle_* events, so the port replays the same tokenizer instead of
// htmlparser2's state machine: the regexes below are html/parser.py's, and goahead/parse_* follow it line by line.
// Only the convert_charrefs=True paths are reachable here (the "&" branches of goahead need convert_charrefs=False).
import { html5, strip as pystrip, unescape } from "./_py";

const STARTTAGOPEN = /<[a-zA-Z]/y;
const ENDTAGOPEN = /<\/[a-zA-Z]/y;
const COMMENTCLOSE = /--!?>/g;
const COMMENTABRUPTCLOSE = /-?>/y;
const TAGFIND_TOLERANT = /([a-zA-Z][^\t\n\r\f />]*)(?:[\t\n\r\f ]|\/(?!>))*/y;
const ATTRFIND_TOLERANT =
  /((?<=['"\t\n\r\f /])[^\t\n\r\f />][^\t\n\r\f /=>]*)([\t\n\r\f ]*=[\t\n\r\f ]*('[^']*'|"[^"]*"|(?!['"])[^>\t\n\r\f ]*))?(?:[\t\n\r\f ]|\/(?!>))*/y;
const LOCATETAGEND =
  /[a-zA-Z][^\t\n\r\f />]*[\t\n\r\f /]*(?:(?<=['"\t\n\r\f /])[^\t\n\r\f />][^\t\n\r\f /=>]*(?:[\t\n\r\f ]*=[\t\n\r\f ]*(?:'[^']*'|"[^"]*"|(?!['"])[^>\t\n\r\f ]*))?[\t\n\r\f /]*)*>?/y;
const ATTR_CHARREF = /&(#[0-9]+|#[xX][0-9a-fA-F]+|[a-zA-Z][a-zA-Z0-9]*)[;=]?/g;
const AMP_END = /[\t\n\r\f ;]/;
const CDATA_CONTENT_ELEMENTS = new Set(["script", "style", "xmp", "iframe", "noembed", "noframes"]);
const RCDATA_CONTENT_ELEMENTS = new Set(["textarea", "title"]);

function matchAt(re: RegExp, s: string, pos: number): RegExpExecArray | null {
  re.lastIndex = pos;
  return re.exec(s);
}

function unescapeAttrvalue(s: string): string {
  return s.replace(ATTR_CHARREF, (ref) => {
    if (ref.startsWith("&#")) return unescape(ref);
    if (!ref.endsWith("=") && html5(ref.slice(1)) !== undefined) return unescape(ref);
    return ref;
  });
}

export type Attr = [string, string | null];

export abstract class HTMLParser {
  private rawdata = "";
  private cdataElem: string | null = null;
  private interesting: RegExp | null = null;   // null: the normal state (find "<")
  private escapable = true;
  private pending: string[] = [];
  private pendingLen = 0;
  private parseThreshold = 1;
  lasttag = "???";

  abstract handle_starttag(tag: string, attrs: Attr[]): void;
  abstract handle_startendtag(tag: string, attrs: Attr[]): void;
  abstract handle_endtag(tag: string): void;
  abstract handle_data(data: string): void;

  feed(data: string): void {
    this.pendingLen += data.length;
    if (this.pendingLen < this.parseThreshold) {
      this.pending.push(data);
      return;
    }
    if (!this.pending.length) this.rawdata += data;
    else {
      this.pending.push(data);
      this.rawdata += this.pending.join("");
      this.pending = [];
    }
    this.pendingLen = 0;
    const n = this.rawdata.length;
    this.goahead(false);
    this.parseThreshold = this.rawdata.length < n ? 1 : this.rawdata.length;
  }

  close(): void {
    if (this.pending.length) {
      this.rawdata += this.pending.join("");
      this.pending = [];
      this.pendingLen = 0;
    }
    this.goahead(true);
  }

  private setCdataMode(elem: string, escapable: boolean): void {
    this.cdataElem = elem.toLowerCase();
    this.escapable = escapable;
    // convert_charrefs is true: escapable or not, only the end tag is interesting
    this.interesting = this.cdataElem === "plaintext" ? /$(?![\s\S])/g : new RegExp(`</${this.cdataElem}(?=[\\t\\n\\r\\f />])`, "gi");
  }

  private clearCdataMode(): void {
    this.interesting = null;
    this.cdataElem = null;
    this.escapable = true;
  }

  private goahead(end: boolean): void {
    const rawdata = this.rawdata;
    let i = 0;
    const n = rawdata.length;
    while (i < n) {
      let j: number;
      if (!this.cdataElem) {
        j = rawdata.indexOf("<", i);
        if (j < 0) {
          const start = Math.max(i, n - 34);
          const last = rawdata.lastIndexOf("&");
          const amppos = last >= start ? last : -1;
          if (amppos >= 0 && !AMP_END.test(rawdata.slice(amppos))) break;
          j = n;
        }
      } else {
        const re = this.interesting!;
        re.lastIndex = i;
        const m = re.exec(rawdata);
        if (m) j = m.index;
        else {
          if (this.cdataElem) break;
          j = n;
        }
      }
      if (i < j) this.handle_data(this.escapable ? unescape(rawdata.slice(i, j)) : rawdata.slice(i, j));
      i = j;
      if (i === n) break;
      if (rawdata.startsWith("<", i)) {
        let k: number;
        if (matchAt(STARTTAGOPEN, rawdata, i)) k = this.parseStarttag(i);
        else if (rawdata.startsWith("</", i)) k = this.parseEndtag(i);
        else if (rawdata.startsWith("<!--", i)) k = this.parseComment(i);
        else if (rawdata.startsWith("<?", i)) k = this.parsePi(i);
        else if (rawdata.startsWith("<!", i)) k = this.parseHtmlDeclaration(i);
        else if (i + 1 < n || end) {
          this.handle_data("<");
          k = i + 1;
        } else break;
        if (k < 0) {
          if (!end) break;
          if (matchAt(STARTTAGOPEN, rawdata, i)) {
            // an unterminated start tag at EOF is dropped
          } else if (rawdata.startsWith("</", i)) {
            if (i + 2 === n) this.handle_data("</");
            // else an end tag or a bogus comment: no data event
          }
          // comments, CDATA, doctype, bogus comments and PIs at EOF produce no data event
          k = n;
        }
        i = k;
      } else {
        throw new Error("interesting.search() lied");
      }
    }
    if (end && i < n) {
      this.handle_data(this.escapable ? unescape(rawdata.slice(i, n)) : rawdata.slice(i, n));
      i = n;
    }
    this.rawdata = rawdata.slice(i);
  }

  private parseHtmlDeclaration(i: number): number {
    const rawdata = this.rawdata;
    if (rawdata.slice(i, i + 4) === "<!--") return this.parseComment(i);
    if (rawdata.slice(i, i + 9) === "<![CDATA[") {
      const j = rawdata.indexOf("]]>", i + 9);
      return j < 0 ? -1 : j + 3;
    }
    if (rawdata.slice(i, i + 9).toLowerCase() === "<!doctype") {
      const gt = rawdata.indexOf(">", i + 9);
      return gt === -1 ? -1 : gt + 1;
    }
    return this.parseBogusComment(i);
  }

  private parseComment(i: number): number {
    const rawdata = this.rawdata;
    let m = matchAt(COMMENTABRUPTCLOSE, rawdata, i + 4);
    if (!m) {
      COMMENTCLOSE.lastIndex = i + 4;
      m = COMMENTCLOSE.exec(rawdata);
      if (!m) return -1;
    }
    return m.index + m[0].length;
  }

  private parseBogusComment(i: number): number {
    const pos = this.rawdata.indexOf(">", i + 2);
    return pos === -1 ? -1 : pos + 1;
  }

  private parsePi(i: number): number {
    const j = this.rawdata.indexOf(">", i + 2);
    return j < 0 ? -1 : j + 1;
  }

  private parseStarttag(i: number): number {
    const endpos = this.checkForWholeStartTag(i);
    if (endpos < 0) return endpos;
    const rawdata = this.rawdata;
    const attrs: Attr[] = [];
    const match = matchAt(TAGFIND_TOLERANT, rawdata, i + 1)!;
    let k = match.index + match[0].length;
    const tag = match[1]!.toLowerCase();
    this.lasttag = tag;
    while (k < endpos) {
      const m = matchAt(ATTRFIND_TOLERANT, rawdata, k);
      if (!m) break;
      const attrname = m[1]!, rest = m[2];
      let attrvalue: string | null = m[3] ?? null;
      if (!rest) attrvalue = null;
      else if (attrvalue !== null && attrvalue.length >= 1 &&
               ((attrvalue[0] === "'" && attrvalue[attrvalue.length - 1] === "'") ||
                (attrvalue[0] === '"' && attrvalue[attrvalue.length - 1] === '"'))) {
        attrvalue = attrvalue.slice(1, -1);
      }
      if (attrvalue) attrvalue = unescapeAttrvalue(attrvalue);
      attrs.push([attrname.toLowerCase(), attrvalue]);
      k = m.index + m[0].length;
    }
    const endText = pystrip(rawdata.slice(k, endpos));
    if (endText !== ">" && endText !== "/>") {
      this.handle_data(rawdata.slice(i, endpos));
      return endpos;
    }
    if (endText.endsWith("/>")) {
      this.handle_startendtag(tag, attrs);
    } else {
      this.handle_starttag(tag, attrs);
      if (CDATA_CONTENT_ELEMENTS.has(tag) || tag === "plaintext") this.setCdataMode(tag, false);
      else if (RCDATA_CONTENT_ELEMENTS.has(tag)) this.setCdataMode(tag, true);
    }
    return endpos;
  }

  private checkForWholeStartTag(i: number): number {
    const m = matchAt(LOCATETAGEND, this.rawdata, i + 1)!;
    const j = m.index + m[0].length;
    return this.rawdata[j - 1] !== ">" ? -1 : j;
  }

  private parseEndtag(i: number): number {
    const rawdata = this.rawdata;
    if (rawdata.indexOf(">", i + 2) < 0) return -1;
    if (!matchAt(ENDTAGOPEN, rawdata, i)) {
      if (rawdata.slice(i + 2, i + 3) === ">") return i + 3;
      return this.parseBogusComment(i);
    }
    const m = matchAt(LOCATETAGEND, rawdata, i + 2)!;
    const j = m.index + m[0].length;
    if (rawdata[j - 1] !== ">") return -1;
    const t = matchAt(TAGFIND_TOLERANT, rawdata, i + 2)!;
    this.handle_endtag(t[1]!.toLowerCase());
    this.clearCdataMode();
    return j;
  }
}
