/** Grows `box` by `padding` on every side, then clamps it to the viewport. */
export const paddedClip = (box, viewport, padding = 16) => {
  const left = Math.max(0, box.x - padding);
  const top = Math.max(0, box.y - padding);
  const right = Math.min(viewport.width, box.x + box.width + padding);
  const bottom = Math.min(viewport.height, box.y + box.height + padding);
  return { x: left, y: top, width: right - left, height: bottom - top };
};
