// tdd strip: strip_page.py in TypeScript (same argv, usage text, outputs, summary line and exit codes).
import { stripFile } from "../src/agents/strip";
import { abspath, basename, dirname, isfile, pjoin, splitext } from "../src/agents/_py";
import { STRIP_PAGE_DOC } from "../src/agents/texts";

function fail(msg: string): number {
  process.stderr.write(msg + "\n");
  return 1;
}

export async function main(argv: string[]): Promise<number> {
  const pos: string[] = [];
  let outDir: string | null = null;
  let i = 0;
  while (i < argv.length) {
    const x = argv[i]!;
    if (x === "--out-dir") {
      if (i + 1 >= argv.length) return fail("--out-dir needs a value");
      outDir = argv[i + 1]!;
      i += 2;
    } else if (x.startsWith("--")) return fail(`unknown option ${x}\n${STRIP_PAGE_DOC}`);
    else {
      pos.push(x);
      i += 1;
    }
  }
  if (pos.length !== 1) return fail(STRIP_PAGE_DOC);
  const path = pos[0]!;
  if (!isfile(path)) return fail(`${path}: no such file`);
  outDir = outDir || pjoin(dirname(abspath(path)), splitext(basename(path))[0] + ".stripped");
  const r = stripFile(path, outDir);
  const rm = [...r.removed].sort((a, b) => b[1] - a[1]).map(([k, v]) => `${v} ${k}`).join(", ");
  process.stdout.write(`wrote ${outDir}: plain.html ${r.html_bytes} bytes (from ${r.in_bytes}), plain.txt ${r.txt_bytes} bytes ` +
    `(~${r.txt_tokens} tokens, chars/4), outline.json ${r.headings} headings; removed ${rm || "nothing"}\n`);
  return 0;
}

if (import.meta.main) process.exit(await main(Bun.argv.slice(2)));
