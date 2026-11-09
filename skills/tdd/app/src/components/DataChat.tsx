// DataChat: the drawer's docked "Ask about this table" chat (D14c, D15). It asks Claude through the artifact runtime's
// `sample` capability, on the viewer's own Claude account, only when the viewer sends a question; nothing is called on
// load. `claude.use("sample")` resolving null (the server render, the harness, any copy outside claude.ai) leaves one
// quiet line. Each send is memoryless on Claude's side, so the page rebuilds the input every time: a rules turn with this
// table's JSON, the last few turns it keeps, the question. Page tools: get_table, get_defs, get_section (content island)
// and, when `claude.use("mcp")` resolves and content.meta.dataSources names a connector the viewer has, query_source.
// The page never embeds production data and holds no credentials. Contract: the artifact-capabilities type definitions
// (claude.d.ts, sample.d.ts, mcp.d.ts, runtime 0.2.96); the shapes this file uses are declared locally below.
import { useCallback, useEffect, useRef, useState } from "react";
import type { FormEvent, KeyboardEvent as ReactKeyboardEvent } from "react";
import { useContent } from "../lib/context";
import { cx } from "../lib/raw";
import type { Content, Node, SchemaTable } from "../types";
import { Ticks, sourceTag } from "./DataGrid";

/* ------------------------------------------------------------------------------------------------ runtime shapes */

type SampleMessage = { role: "user" | "assistant"; content: string };
interface SampleTool {
  name: string; description: string;
  inputSchema?: { type: "object"; properties?: Record<string, unknown>; required?: string[] };
  execute(input: { [k: string]: unknown }, context: { signal: AbortSignal }): unknown;
}
interface SampleOptions { onText?: (u: { text: string; delta: string }) => void; signal?: AbortSignal; tools?: SampleTool[]; cache?: boolean }
type SampleFn = ((input: string | SampleMessage[], options?: SampleOptions) => Promise<{ text: string; truncated: boolean }>) & {
  limits?: () => Promise<{ maxPromptBytes: number; tools?: { maxCount: number } }>;
};
type ServerHandle = Readonly<Record<string, (input?: unknown, options?: { signal?: AbortSignal }) => Promise<unknown>>>;
interface McpNs {
  listTools(server?: string): Promise<{ servers: { server: string; authStatus?: string; tools?: { name: string }[] }[] }>;
  server(name: string): Promise<ServerHandle>;
}
interface ClaudeRuntime { use(name: string): Promise<unknown> }

/** One entry of content.meta.dataSources (references/data-view.md). */
export interface DataSource { name: string; server: string; tool: string; arg?: string; kind?: "prod" | "local" | "render" | "seed"; note?: string }

interface Caps { sample: SampleFn | null; tools: boolean; mcp: McpNs | null; servers: Map<string, string> }
const NONE: Caps = { sample: null, tools: false, mcp: null, servers: new Map() };

let capsOnce: Promise<Caps> | null = null;
/** Ask the runtime once per page view which capabilities this view can run. `use()` and `limits()` never prompt. */
function loadCaps(wantMcp: boolean): Promise<Caps> {
  if (capsOnce) return capsOnce;
  capsOnce = (async () => {
    const rt = (window as unknown as { claude?: ClaudeRuntime }).claude;
    if (!rt || typeof rt.use !== "function") return NONE;
    const sample = ((await rt.use("sample").catch(() => null)) || null) as SampleFn | null;
    if (!sample) return NONE;
    let tools = false;
    if (typeof sample.limits === "function") tools = !!(await sample.limits().catch(() => null))?.tools;
    let mcp: McpNs | null = null;
    const servers = new Map<string, string>();
    if (wantMcp && tools) {
      mcp = ((await rt.use("mcp").catch(() => null)) || null) as McpNs | null;
      if (mcp) {
        const listed = await mcp.listTools().catch(() => null);
        for (const s of listed?.servers || []) servers.set(s.server, s.authStatus || "unknown");
      }
    }
    return { sample, tools, mcp, servers };
  })();
  return capsOnce;
}

export function dataSources(content: Content | null): DataSource[] {
  const m = (content?.meta || {}) as unknown as { dataSources?: unknown; data_sources?: unknown };
  const raw = Array.isArray(m.dataSources) ? m.dataSources : Array.isArray(m.data_sources) ? m.data_sources : [];
  return raw.filter((s: any) => s && typeof s.name === "string" && typeof s.server === "string" && typeof s.tool === "string") as DataSource[];
}

/* ------------------------------------------------------------------------------------------------ page data for Claude */

const HISTORY = 6;
const TABLE_MAX = 120_000;
const SECTION_MAX = 12_000;
const WRITE_SQL = /\b(insert|update|delete|merge|upsert|drop|alter|truncate|create|grant|revoke|copy|call|vacuum|reindex|cluster|lock|comment\s+on|set\s+role)\b/i;
const SKIP = new Set(["id", "src", "href", "image", "key", "type", "cls", "tone", "x", "y", "w", "h", "width", "height", "fit"]);

function tableJson(t: SchemaTable): string {
  const pick = (rows: unknown[] | undefined) => ({
    table: t.table, state: t.state, summary: t.summary, columns: t.columns, sample_source: t.sample_source,
    sample_rows: rows, measured: t.measured || {}, expected: t.expected || {}, ddl: t.ddl, indexes: t.indexes, notes: t.notes,
    chat_context: (t as unknown as { chat_context?: unknown }).chat_context,
  });
  const full = JSON.stringify(pick(t.sample_rows));
  if (full.length <= TABLE_MAX) return full;
  return JSON.stringify({ ...pick((t.sample_rows || []).slice(0, 20)), note: "sample_rows cut to the first 20 to fit" }).slice(0, TABLE_MAX);
}

function plain(s: string) {
  return s.replace(/<[^>]+>/g, " ").replace(/&nbsp;/g, " ").replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&quot;/g, '"')
    .replace(/&#39;|&apos;/g, "'").replace(/&amp;/g, "&").replace(/[ \t]+/g, " ").trim();
}

/** The plain text of a content node (a section, a block, an anchored row), in document order. */
function nodeText(n: Node, out: string[]) {
  if (n == null) return;
  if (typeof n === "string") { const p = plain(n); if (p) out.push(p); return; }
  if (Array.isArray(n)) { for (const x of n) nodeText(x, out); return; }
  if (typeof n !== "object") return;
  for (const k in n) if (!SKIP.has(k)) nodeText(n[k], out);
}

function findNode(n: Node, id: string): Node | null {
  if (!n || typeof n !== "object") return null;
  if (Array.isArray(n)) { for (const x of n) { const r = findNode(x, id); if (r) return r; } return null; }
  if (n.id === id) return n;
  for (const k in n) { const r = findNode(n[k], id); if (r) return r; }
  return null;
}

function rules(content: Content, t: SchemaTable, tools: boolean, live: DataSource[]) {
  const others = (content.schema || []).filter((x) => x.id !== t.id).map((x) => x.table);
  const lines = [
    `You answer questions about one database table described in a technical design document (TDD): "${content.titleText}".`,
    "Rules:",
    "- Use only the data below and what the tools return. When the answer is not there, say so. Never invent values, counts, rates or measurements.",
    `- The sample rows are ${sourceTag(t.sample_source)} (${t.sample_source || "source not stated"}). Say so when an answer uses them; statistics computed from them describe the sample, not the real table.`,
    '- "measured" values carry a source and a date; "expected" values are estimates with a basis; a missing measure is "not measured".',
    "- Answer in a few short sentences or a small plain-text table. Use column names exactly, in backticks.",
  ];
  if (tools) {
    lines.push(`- Tools: get_table(name) returns another table's JSON from this page (${others.join(", ") || "none"}); get_defs(query) searches the page's anchors (decisions, findings, PRs, glossary); get_section(id) returns the text of a section or anchored block. Call them only when the question needs them.`);
    if (live.length) {
      lines.push("- query_source(name, sql_or_args) runs ONE read-only query on a connected source and returns its rows. SELECT only, always with LIMIT 50 or less. Say which source an answer came from and that it is live data.");
      for (const s of live) lines.push(`  - ${s.name} (${s.kind || "source"}, via ${s.server}): ${s.note || "no note"}`);
    }
  } else {
    lines.push("- No tools are available in this view: answer from the table below only.");
  }
  lines.push("", `The table "${t.table}" as JSON:`, tableJson(t));
  return lines.join("\n");
}

function pageTools(content: Content, live: DataSource[], mcp: McpNs | null, progress: (s: string) => void, onSource: (server: string, note: string) => void): SampleTool[] {
  const tools: SampleTool[] = [
    {
      name: "get_table",
      description: "Returns one table's JSON from this TDD page (columns with change flags, synthetic sample rows, measured and expected load, DDL, indexes, notes). Use for questions that involve another table.",
      inputSchema: { type: "object", properties: { name: { type: "string", description: "table name, e.g. hold_request" } }, required: ["name"] },
      execute(input) {
        const name = String(input.name || "");
        const hit = (content.schema || []).find((x) => x.table === name || x.id === name);
        if (!hit) throw new Error(`no table "${name}" on this page; tables: ${(content.schema || []).map((x) => x.table).join(", ")}`);
        progress(`Read table ${hit.table}`);
        return tableJson(hit);
      },
    },
    {
      name: "get_defs",
      description: "Searches the page's anchors (sections, decisions such as D1, findings, PRs, glossary terms, tables) by id or words; returns up to 12 {id, kind, title, text}.",
      inputSchema: { type: "object", properties: { query: { type: "string" } }, required: ["query"] },
      execute(input) {
        const q = String(input.query || "").trim().toLowerCase();
        if (!q) throw new Error("query is empty");
        const words = q.split(/\s+/).filter(Boolean);
        const hits = Object.entries(content.defs || {})
          .map(([id, d]) => {
            const hay = `${id} ${d.title} ${d.text}`.toLowerCase();
            const score = (id.toLowerCase() === q ? 100 : 0) + words.filter((w) => hay.includes(w)).length;
            return { id, d, score };
          })
          .filter((x) => x.score >= Math.max(1, words.length))
          .sort((a, b) => b.score - a.score)
          .slice(0, 12)
          .map(({ id, d }) => ({ id, kind: d.kind, title: plain(d.title || ""), text: plain(d.text || "").slice(0, 400) }));
        progress(`Searched the page for "${q.slice(0, 40)}" (${hits.length})`);
        return hits;
      },
    },
    {
      name: "get_section",
      description: "Returns the plain text of one section (or any anchored block) of this TDD page by its id, e.g. sizing or d1; cut at 12 KB.",
      inputSchema: { type: "object", properties: { id: { type: "string" } }, required: ["id"] },
      execute(input) {
        const id = String(input.id || "").replace(/^#/, "");
        const node = (content.sections || []).find((s) => s.id === id) || findNode(content.sections, id) || findNode(content.lede, id);
        if (!node) throw new Error(`no section or anchored block "${id}"; sections: ${(content.sections || []).map((s) => s.id).join(", ")}`);
        const out: string[] = [];
        nodeText(node, out);
        progress(`Read section ${id}`);
        const txt = out.join("\n");
        return txt.length > SECTION_MAX ? txt.slice(0, SECTION_MAX) + "\n[cut at 12 KB]" : txt;
      },
    },
  ];
  if (mcp && live.length) {
    tools.push({
      name: "query_source",
      description: `Runs one read-only query on a connected data source and returns the result (cut at 30 KB). Sources: ${live.map((s) => s.name).join(", ")}. Pass SQL text, or an object of the tool's arguments when the source takes no SQL.`,
      inputSchema: {
        type: "object",
        properties: { name: { type: "string", enum: live.map((s) => s.name) }, sql_or_args: { description: "SELECT … LIMIT 50, or an arguments object" } },
        required: ["name", "sql_or_args"],
      },
      async execute(input, ctx) {
        const src = live.find((s) => s.name === String(input.name));
        if (!src) throw new Error(`unknown source "${String(input.name)}"; sources: ${live.map((s) => s.name).join(", ")}`);
        const raw = input.sql_or_args;
        let args: unknown;
        const writes = (s: string) => WRITE_SQL.test(s.replace(/'[^']*'/g, "''"));
        if (typeof raw === "string") {
          if (writes(raw)) throw new Error("this page runs read-only queries only (SELECT)");
          if (src.arg) args = { [src.arg]: raw };
          else {
            try { args = JSON.parse(raw); } catch { throw new Error(`${src.name} takes an object of arguments for ${src.tool}, not SQL text`); }
          }
        } else if (raw && typeof raw === "object") {
          if (Object.values(raw as Record<string, unknown>).some((v) => typeof v === "string" && writes(v))) throw new Error("this page runs read-only queries only (SELECT)");
          args = raw;
        } else throw new Error("sql_or_args is empty");
        progress(`Querying ${src.name} via ${src.server}`);
        try {
          const h = await mcp.server(src.server);
          const fn = h[src.tool];
          if (typeof fn !== "function") throw { code: "tool_error", message: `${src.server} offers no ${src.tool} in this view` };
          const out = await fn(args, { signal: ctx.signal });
          const s = typeof out === "string" ? out : JSON.stringify(out);
          return s.length > 30_000 ? s.slice(0, 30_000) + "\n[cut at 30 KB]" : s;
        } catch (e: any) {
          const note = mcpCopy(e?.code, src.server, e?.message);
          onSource(src.server, note);
          throw new Error(`${e?.code || "upstream_error"}: ${note}`);
        }
      },
    });
  }
  return tools;
}

function mcpCopy(code: string | undefined, server: string, message?: string) {
  switch (code) {
    case "needs_reauth": return `Reconnect ${server} in claude.ai Settings → Connectors.`;
    case "server_not_connected": return server.startsWith("host:") ? `${server} answers only in the Claude desktop app with that local server running.` : `Add ${server} in claude.ai Settings → Connectors.`;
    case "selection_required": return `Choose which ${server} connector to use when claude.ai asks.`;
    case "not_in_manifest": return `${server} is not allowed for this page in this view.`;
    case "blocked_by_policy": return `Your organization's policy blocks ${server} here.`;
    case "approval_required": return `${server} needs your approval; ask again to be prompted.`;
    case "cancelled": return `The ${server} call was stopped.`;
    case "tool_error": return `${server} reported: ${String(message || "an error").slice(0, 200)}`;
    case "server_unavailable": return `${server} is unreachable just now.`;
    default: return `${server} is unavailable just now.`;
  }
}

/* ------------------------------------------------------------------------------------------------ the panel */

type Msg = { id: number; role: "user" | "assistant"; text: string; status?: "thinking" | "streaming" | "done" | "stopped" | "failed"; note?: string; steps?: string[] };
type Mode = "off" | "checking" | "ready" | "hidden";

const HIDE = new Set(["not_granted", "sampling_disabled", "not_declared", "capability_disabled", "capability_removed"]);
function sampleCopy(code: string) {
  switch (code) {
    case "rate_limited": return "Too many questions just now, or your usage limit was reached. Try again in a little while.";
    case "session_expired": return "Your claude.ai session expired. Sign in again, then ask.";
    case "refused": return "Claude declined this question. Ask it differently.";
    case "empty_completion": return "No answer came back. Ask for less at a time.";
    case "prompt_too_large": return "This table is too large to send whole. Ask about fewer columns.";
    case "tools_unavailable": return "Page tools are not available in this view. Ask again for an answer from this table only.";
    case "invalid_request": case "transform_error": case "queue_overflow": return "The page sent a malformed request (a page bug).";
    default: return "Interrupted. You can ask again.";
  }
}

let seq = 0;

export function DataChat({ t }: { t: SchemaTable }) {
  const content = useContent();
  const [mode, setMode] = useState<Mode>("off");
  const [why, setWhy] = useState("");
  const [caps, setCaps] = useState<Caps>(NONE);
  const [toolsOff, setToolsOff] = useState(false);
  const [msgs, setMsgs] = useState<Msg[]>([]);
  const [draft, setDraft] = useState("");
  const [busy, setBusy] = useState(false);
  const [srcNote, setSrcNote] = useState<Record<string, string>>({});
  const ctl = useRef<AbortController | null>(null);
  const log = useRef<HTMLDivElement>(null);
  const sources = dataSources(content);

  // Feature-detect after mount (never during render): the first render is the same quiet line everywhere.
  useEffect(() => {
    let live = true;
    const rt = (window as unknown as { claude?: ClaudeRuntime }).claude;
    if (!rt || typeof rt.use !== "function") return;
    setMode("checking");
    loadCaps(sources.length > 0).then((c) => {
      if (!live) return;
      setCaps(c);
      setMode(c.sample ? "ready" : "off");
    });
    return () => { live = false; ctl.current?.abort(); };
    // sources come from the page's content, which never changes after load
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const el = log.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [msgs]);

  const live = sources.filter((s) => caps.mcp && caps.servers.has(s.server) && caps.servers.get(s.server) !== "needs_reauth");
  const reauth = sources.filter((s) => caps.servers.get(s.server) === "needs_reauth");
  const useTools = caps.tools && !toolsOff;

  const patch = useCallback((id: number, p: Partial<Msg> | ((m: Msg) => Partial<Msg>)) => {
    setMsgs((ms) => ms.map((m) => (m.id === id ? { ...m, ...(typeof p === "function" ? p(m) : p) } : m)));
  }, []);

  const send = async (e?: FormEvent) => {
    e?.preventDefault();
    const q = draft.trim();
    const sample = caps.sample;
    if (!q || busy || !sample) return;
    const history: SampleMessage[] = msgs
      .filter((m) => m.text.trim() && (m.role === "user" || m.status === "done" || m.status === "stopped"))
      .slice(-HISTORY)
      .map((m) => ({ role: m.role, content: m.text }));
    const input: SampleMessage[] = [{ role: "user", content: rules(content, t, useTools, useTools ? live : []) }, ...history, { role: "user", content: q }];
    const uid = ++seq, aid = ++seq;
    setMsgs((ms) => [...ms, { id: uid, role: "user", text: q }, { id: aid, role: "assistant", text: "", status: "thinking", steps: [] }]);
    setDraft("");
    setBusy(true);
    const c = new AbortController();
    ctl.current = c;
    const opts: SampleOptions = { signal: c.signal, cache: false, onText: ({ text }) => patch(aid, { text, status: "streaming" }) };
    if (useTools) {
      opts.tools = pageTools(content, live, caps.mcp, (s) => patch(aid, (m) => ({ steps: [...(m.steps || []), s] })),
        (server, note) => setSrcNote((n) => ({ ...n, [server]: note })));
    }
    try {
      const res = await sample(input, opts);
      patch(aid, { text: res.text, status: "done", note: res.truncated ? "Cut short. Ask for less at a time." : undefined });
    } catch (err: any) {
      const code: string = err?.code || "upstream_error";
      const kept: string = typeof err?.text === "string" ? err.text : "";
      if (code === "cancelled") patch(aid, { text: kept, status: "stopped", note: "Stopped." });
      else if (HIDE.has(code)) {
        patch(aid, { text: "", status: "failed", note: "Chat is off for this view." });
        setWhy(code === "not_granted" ? "Claude was not allowed for this page. Allow it from the artifact's Permissions menu to use the chat." : "Claude is not available for this account or view.");
        setMode("hidden");
      } else {
        if (code === "tools_unavailable") setToolsOff(true);
        patch(aid, { text: code === "refused" ? "" : kept, status: "failed", note: sampleCopy(code) });
      }
    } finally {
      if (ctl.current === c) ctl.current = null;
      setBusy(false);
    }
  };

  const stop = () => ctl.current?.abort();
  const onKey = (e: ReactKeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) { e.preventDefault(); void send(); }
  };

  if (mode === "off" || mode === "hidden") {
    return (
      <footer className="dchat off" aria-label="Ask about this table">
        <p className="dchat-off">
          <b>Ask about this table</b>
          <span>{mode === "hidden" ? why : "Chat is available when this page runs on claude.ai with the sample capability."}</span>
        </p>
      </footer>
    );
  }

  const via = live.map((s) => s.server).filter((s, i, a) => a.indexOf(s) === i);
  return (
    <footer className="dchat" aria-label="Ask about this table">
      {msgs.length ? (
        <div className="dchat-log" ref={log} aria-live="polite">
          {msgs.map((m) => (
            <div key={m.id} className={cx("dchat-msg", m.role, m.status)}>
              {m.role === "assistant" && m.steps?.length ? <ul className="dchat-steps">{m.steps.map((s, i) => <li key={i}>{s}</li>)}</ul> : null}
              {m.text ? <div className="dchat-text"><Ticks text={m.text} /></div> : m.status === "thinking" || m.status === "streaming" ? <div className="dchat-text muted">Thinking…</div> : null}
              {m.note ? <div className="dchat-note">{m.note}</div> : null}
            </div>
          ))}
        </div>
      ) : null}
      <form className="dchat-form" onSubmit={send}>
        <label className="sr-only" htmlFor={`dchat-q-${t.id}`}>Ask about {t.table}</label>
        <textarea id={`dchat-q-${t.id}`} rows={1} value={draft} placeholder={mode === "checking" ? "Connecting…" : `Ask about ${t.table}`}
          disabled={mode !== "ready"} onChange={(e) => setDraft(e.target.value)} onKeyDown={onKey} />
        {busy ? <button type="button" className="dchat-btn stop" onClick={stop}>Stop</button>
          : <button type="submit" className="dchat-btn" disabled={mode !== "ready" || !draft.trim()}>Ask</button>}
      </form>
      <p className="dchat-src">
        <b>Ask about this table</b>
        <span>{via.length ? <>connected via {via.map((s, i) => <code key={s}>{i ? ", " : ""}{s}</code>)}{Object.keys(srcNote).length ? ` · ${Object.values(srcNote)[0]}` : ""}</>
          : "Answers come from the sample rows on this page only."}</span>
        {reauth.length ? <span className="warn">{mcpCopy("needs_reauth", reauth[0].server)}</span> : null}
        {msgs.length && !busy ? <button type="button" className="linkish" onClick={() => setMsgs([])}>New chat</button> : null}
      </p>
    </footer>
  );
}
