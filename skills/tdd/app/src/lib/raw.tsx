import { createElement } from "react";
/** An element whose children are a trusted inline-HTML string from content.json. Never give it React children too. */
export function Raw({ as = "span", html, ...rest }: { as?: string; html: string; [k: string]: any }) {
  return createElement(as, { ...rest, dangerouslySetInnerHTML: { __html: html || "" } });
}
export const cx = (...xs: (string | false | null | undefined)[]) => xs.filter(Boolean).join(" ");
