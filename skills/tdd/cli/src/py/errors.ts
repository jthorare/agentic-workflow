// The Python exception a kernel function raises, by name, with CPython's message text: callers that print
// `str(e)` (warnings, lint findings) print `err.message`, and callers that branch on the type read `err.type`.
export type PyErrorType =
  | "ValueError" | "TypeError" | "OverflowError" | "KeyError" | "IndexError" | "AttributeError" | "StopIteration"
  | "UnicodeDecodeError" | "UnicodeEncodeError" | "JSONDecodeError" | "re.error" | "OSError" | "FileNotFoundError";

export class PyError extends Error {
  readonly type: PyErrorType;
  constructor(type: PyErrorType, message: string) {
    super(message);
    this.type = type;
    this.name = type;
  }
}

/** The name a Python traceback's last line gives the exception (the module-qualified one where CPython prints it). */
export function qualifiedName(type: PyErrorType): string {
  return type === "JSONDecodeError" ? "json.decoder.JSONDecodeError" : type === "re.error" ? "re.PatternError" : type;
}

export const valueError = (message: string): PyError => new PyError("ValueError", message);
export const typeError = (message: string): PyError => new PyError("TypeError", message);
export const keyError = (message: string): PyError => new PyError("KeyError", message);
