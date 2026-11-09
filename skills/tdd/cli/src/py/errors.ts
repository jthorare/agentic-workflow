// The Python exception a kernel function raises, by name, with CPython's message text: callers that print
// `str(e)` (warnings, lint findings) print `err.message`, and callers that branch on the type read `err.type`.
export type PyErrorType =
  | "ValueError" | "TypeError" | "OverflowError" | "KeyError" | "IndexError"
  | "UnicodeDecodeError" | "UnicodeEncodeError" | "JSONDecodeError" | "re.error" | "OSError" | "FileNotFoundError";

export class PyError extends Error {
  readonly type: PyErrorType;
  constructor(type: PyErrorType, message: string) {
    super(message);
    this.type = type;
    this.name = type;
  }
}
