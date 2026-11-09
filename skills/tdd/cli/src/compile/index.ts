// The tdd compiler in TypeScript (PR 11, D19): build_tdd.py ported to plain synchronous code on src/py, byte for byte.
// Module map: args.ts (argv, usage, exit codes), attrs.ts ({attrs}, ::: containers), refs.ts (reference linking),
// sanitize.ts (the HTML guard), prs.ts (prs/pr-NN.md), compiler.ts (the Compiler: blocks, figures, containers, page).
// The vocabulary (TOKEN, CITE, LEGEND_TONES, ...) is src/vocab.ts; the system-design surface is src/sd.
export { DOC, parse_args, type Args } from "./args";
export { ATTRS, BOX, parse_attrs, parse_ext, split_title, type Attrs, type Block, type BoxBlock } from "./attrs";
export { Compiler, compile, type Ctx, type Node, type Summary } from "./compiler";
export { PyOSError, SystemExit, isSystemExit, makedirs, osError } from "./errors";
export { PR_FILE, load_prs, type Pr } from "./prs";
export { link_refs, type Link, type Stats } from "./refs";
export { sanitize_html } from "./sanitize";

/**
 * sha256 of the scripts/build_tdd.py this port reproduces (the twin rule, research/bun-port-2026.md section 5.3): a
 * change to the Python lands with the same change here and a new pin, or test/compile/pin.test.ts fails.
 */
export const BUILD_TDD_SHA256 = "630ce50128deb1e0bdb89681c07242e1f0e42aa2ab385aca8bb17af0f38cf9aa";
