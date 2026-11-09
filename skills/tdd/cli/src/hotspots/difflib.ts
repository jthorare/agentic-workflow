// difflib.SequenceMatcher from CPython 3.13 (R20): the b2j index with the junk and popular-element purges (autojunk:
// with len(b) >= 200, an element occurring more than len(b) // 100 + 1 times is dropped from b2j), find_longest_match
// with its junk-extension passes, get_matching_blocks and ratio. A str is compared as a sequence of code points, as
// Python iterates it. Tested against python3 -I on random pairs in test/hotspots/difflib.test.ts.

export interface Match { a: number; b: number; size: number }

const seq = <T>(x: Iterable<T> | string): T[] => (typeof x === "string" ? (Array.from(x) as T[]) : [...x]);

export class SequenceMatcher<T> {
  a: T[] = [];
  b: T[] = [];
  b2j = new Map<T, number[]>();
  bjunk = new Set<T>();
  bpopular = new Set<T>();
  private matchingBlocks: Match[] | null = null;

  constructor(readonly isjunk: ((x: T) => boolean) | null = null, a: Iterable<T> | string = [],
    b: Iterable<T> | string = [], readonly autojunk = true) {
    this.setSeqs(a, b);
  }
  setSeqs(a: Iterable<T> | string, b: Iterable<T> | string): void { this.setSeq1(a); this.setSeq2(b); }
  setSeq1(a: Iterable<T> | string): void { this.a = seq(a); this.matchingBlocks = null; }
  setSeq2(b: Iterable<T> | string): void { this.b = seq(b); this.matchingBlocks = null; this.chainB(); }

  private chainB(): void {
    const b = this.b;
    const b2j = (this.b2j = new Map());
    b.forEach((elt, i) => {
      let idx = b2j.get(elt);
      if (!idx) b2j.set(elt, (idx = []));
      idx.push(i);
    });
    const junk = (this.bjunk = new Set());
    if (this.isjunk) {
      for (const elt of b2j.keys()) if (this.isjunk(elt)) junk.add(elt);
      for (const elt of junk) b2j.delete(elt);
    }
    const popular = (this.bpopular = new Set());
    const n = b.length;
    if (this.autojunk && n >= 200) {
      const ntest = Math.floor(n / 100) + 1;
      for (const [elt, idxs] of b2j) if (idxs.length > ntest) popular.add(elt);
      for (const elt of popular) b2j.delete(elt);
    }
  }

  findLongestMatch(alo = 0, ahi: number = this.a.length, blo = 0, bhi: number = this.b.length): Match {
    const { a, b, b2j, bjunk } = this;
    let besti = alo, bestj = blo, bestsize = 0;
    let j2len = new Map<number, number>();
    for (let i = alo; i < ahi; i++) {
      const newj2len = new Map<number, number>();
      for (const j of b2j.get(a[i]!) ?? []) {
        if (j < blo) continue;
        if (j >= bhi) break;
        const k = (j2len.get(j - 1) ?? 0) + 1;
        newj2len.set(j, k);
        if (k > bestsize) { besti = i - k + 1; bestj = j - k + 1; bestsize = k; }
      }
      j2len = newj2len;
    }
    while (besti > alo && bestj > blo && !bjunk.has(b[bestj - 1]!) && a[besti - 1] === b[bestj - 1]) {
      besti--; bestj--; bestsize++;
    }
    while (besti + bestsize < ahi && bestj + bestsize < bhi && !bjunk.has(b[bestj + bestsize]!) &&
      a[besti + bestsize] === b[bestj + bestsize]) bestsize++;
    while (besti > alo && bestj > blo && bjunk.has(b[bestj - 1]!) && a[besti - 1] === b[bestj - 1]) {
      besti--; bestj--; bestsize++;
    }
    while (besti + bestsize < ahi && bestj + bestsize < bhi && bjunk.has(b[bestj + bestsize]!) &&
      a[besti + bestsize] === b[bestj + bestsize]) bestsize++;
    return { a: besti, b: bestj, size: bestsize };
  }

  getMatchingBlocks(): Match[] {
    if (this.matchingBlocks) return this.matchingBlocks;
    const la = this.a.length, lb = this.b.length;
    const queue: [number, number, number, number][] = [[0, la, 0, lb]];
    const blocks: Match[] = [];
    while (queue.length) {
      const [alo, ahi, blo, bhi] = queue.pop()!;
      const x = this.findLongestMatch(alo, ahi, blo, bhi);
      const { a: i, b: j, size: k } = x;
      if (k) {
        blocks.push(x);
        if (alo < i && blo < j) queue.push([alo, i, blo, j]);
        if (i + k < ahi && j + k < bhi) queue.push([i + k, ahi, j + k, bhi]);
      }
    }
    blocks.sort((p, q) => p.a - q.a || p.b - q.b || p.size - q.size);
    let i1 = 0, j1 = 0, k1 = 0;
    const nonAdjacent: Match[] = [];
    for (const { a: i2, b: j2, size: k2 } of blocks) {
      if (i1 + k1 === i2 && j1 + k1 === j2) k1 += k2;
      else {
        if (k1) nonAdjacent.push({ a: i1, b: j1, size: k1 });
        i1 = i2; j1 = j2; k1 = k2;
      }
    }
    if (k1) nonAdjacent.push({ a: i1, b: j1, size: k1 });
    nonAdjacent.push({ a: la, b: lb, size: 0 });
    return (this.matchingBlocks = nonAdjacent);
  }

  ratio(): number {
    const matches = this.getMatchingBlocks().reduce((n, m) => n + m.size, 0);
    const length = this.a.length + this.b.length;
    return length ? (2.0 * matches) / length : 1.0;
  }
}
