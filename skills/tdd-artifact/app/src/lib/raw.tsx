import { createElement } from "react";
/** An element whose children are a trusted inline-HTML string from content.json. Never give it React children too. */
export function Raw({ as = "span", html, ...rest }: { as?: string; html: string; [k: string]: any }) {
  return createElement(as, { ...rest, dangerouslySetInnerHTML: { __html: html || "" } });
}
// `true` is accepted because an attribute written bare in the source reaches a node as `true` (AttrText in types.ts);
// it joins as the word "true", as it always has.
export const cx = (...xs: (string | boolean | null | undefined)[]) => xs.filter(Boolean).join(" ");
