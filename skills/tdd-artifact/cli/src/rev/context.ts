// The run's fixed inputs as services: where (RevPaths) and how (RevOpts, the parsed argv).
import { Context } from "effect";
import type { RevArgs } from "./args";
import type { Paths } from "./paths";

export class RevPaths extends Context.Service<RevPaths, Paths>()("tdd/rev/Paths") {}
export class RevOpts extends Context.Service<RevOpts, RevArgs>()("tdd/rev/Opts") {}
