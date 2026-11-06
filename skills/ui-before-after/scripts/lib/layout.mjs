/** Places panels left to right (main, PR, then any Figma frames), top-aligned under a label strip. */
export const rowLayout = (panels, { gap = 24, labelHeight = 32 } = {}) => {
  const at = [];
  let left = 0;
  for (const panel of panels) {
    at.push({ left, top: labelHeight });
    left += panel.width + gap;
  }
  return {
    width: left - gap,
    height: labelHeight + Math.max(...panels.map((panel) => panel.height)),
    at,
    labelHeight,
  };
};
