// The Python exception a kernel function raises, by name, with CPython's message text: callers that print
// `str(e)` (warnings, lint findings) print `err.message`, and callers that branch on the type read `err.type`.
export type PyErrorType =
  | "ValueError" | "TypeError" | "OverflowError" | "ZeroDivisionError" | "KeyError" | "IndexError" | "AttributeError" | "StopIteration"
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

const VALUE_ERRORS: ReadonlySet<PyErrorType> = new Set(["ValueError", "UnicodeDecodeError", "UnicodeEncodeError", "JSONDecodeError"]);
const OS_ERRORS: ReadonlySet<PyErrorType> = new Set(["OSError", "FileNotFoundError"]);

/** isinstance(e, OSError): a PyError of that family, or a file-system error from node (an E... code and an errno). */
export function isOSError(e: unknown): boolean {
  if (e instanceof PyError) return OS_ERRORS.has(e.type);
  const sys = e as { code?: unknown; errno?: unknown } | null;
  return !!sys && typeof sys === "object" && typeof sys.code === "string" && /^E[A-Z0-9]+$/.test(sys.code) && typeof sys.errno === "number";
}

/** isinstance(e, ValueError) (JSONDecodeError and the Unicode errors are ValueErrors). */
export const isValueError = (e: unknown): boolean => e instanceof PyError && VALUE_ERRORS.has(e.type);

/** Whether `except (OSError, ValueError):` catches e; anything else propagates, as in the Python. */
export const isOsOrValueError = (e: unknown): boolean => isOSError(e) || isValueError(e);

export const valueError = (message: string): PyError => new PyError("ValueError", message);
export const typeError = (message: string): PyError => new PyError("TypeError", message);
export const keyError = (message: string): PyError => new PyError("KeyError", message);
