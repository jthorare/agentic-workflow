// Python exceptions src/sd raises that are not in the kernel's PyErrorType: each carries `type`, the name the oracle's
// exc_name() prints (module-qualified unless builtin), and CPython's str(e) as its message.
export class SdError extends Error {
  readonly type: string;
  constructor(type: string, message: string) {
    super(message);
    this.type = type;
    this.name = type;
  }
}

/** struct.error: png_size unpacking a PNG signature followed by fewer than 16 bytes. */
export class StructError extends SdError {
  constructor(message: string) {
    super("struct.error", message);
  }
}

/** shutil.SameFileError: Assets.finish copying a file onto itself (a symlink, a hard link, a case-insensitive name). */
export class SameFileError extends SdError {
  constructor(message: string) {
    super("shutil.SameFileError", message);
  }
}

/** RecursionError: json.loads entering a container nested deeper than CPython's C recursion limit allows. */
export class RecursionError extends SdError {
  constructor(message: string) {
    super("RecursionError", message);
  }
}
