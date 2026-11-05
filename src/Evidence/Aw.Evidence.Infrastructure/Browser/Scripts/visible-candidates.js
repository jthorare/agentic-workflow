// Visible interactive elements (non-zero box, not hidden): DOM-order index, role, accessible name, test id, text.
// A name or text is capped at 200 code points (never splitting a surrogate pair), so a page-wide container's text
// doesn't flood the judge's context.
(selector) => {
  const named = (v) => (v && v.trim()) || null;
  const capped = (v) => {
    if (v == null) return v;
    const points = Array.from(v);
    return points.length > 200 ? points.slice(0, 200).join("") + "\u2026" : v;
  };
  const out = [];
  Array.from(document.querySelectorAll(selector)).forEach((el, index) => {
    const rect = el.getBoundingClientRect();
    const style = getComputedStyle(el);
    if (rect.width === 0 || rect.height === 0 || style.visibility === "hidden" || style.display === "none") return;
    const text = el.textContent?.trim() ?? null;
    out.push({
      index,
      role: el.getAttribute("role") ?? el.tagName.toLowerCase(),
      // A submit input or an icon button has no text: its value or title is what it says (the repair write check reads this).
      // An empty aria-label names nothing, and only a button-like input's value is a label: a text or password field's
      // value is what was typed into it, which must never reach the judge.
      // A blank aria-label, text or title names nothing either: each source counts only when it has non-space text.
      accessibleName: capped(named(el.getAttribute("aria-label")) ?? named(text) ?? named(el.getAttribute("title"))
        ?? (el instanceof HTMLInputElement && ["submit", "button", "reset"].includes(el.type) ? named(el.value) : null) ?? text),
      testId: el.getAttribute("data-testid"),
      text: capped(text),
    });
  });
  return out;
}
