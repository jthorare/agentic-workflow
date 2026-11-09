// sys.exit(message) in the compiler: the bin prints the message on stderr and exits with the code (1), as CPython
// does for a str argument. Any error whose name is "SystemExit" is treated the same way (the system-design surface
// raises its own from load_json).
export class SystemExit extends Error {
  override readonly name = "SystemExit";
  constructor(readonly code: number, msg = "") {
    super(msg);
  }
}

export const isSystemExit = (e: unknown): e is { code?: number; message: string } =>
  typeof e === "object" && e !== null && (e as { name?: unknown }).name === "SystemExit";
