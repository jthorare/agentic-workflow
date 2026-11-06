const IMAGE_REF = /!\[([^\]]*)\]\((\.\/[^)\s]+\.png)\)/g;
const VIDEO_LINE = /^\.\/\S+\.webm$/;
const BARE_UPLOAD_URL = /^https:\/\/github\.com\/user-attachments\/assets\/\S+$/;

/** The `--attach` values a body.md needs: images as `path#alt`, videos as bare paths, each once. */
export const attachPlan = (body) => {
  const images = new Map();
  for (const [, alt, path] of body.matchAll(IMAGE_REF)) if (!images.has(path)) images.set(path, alt);
  const videos = [...new Set(body.split("\n").map((line) => line.trim()).filter((line) => VIDEO_LINE.test(line)))];
  return { images: [...images].map(([path, alt]) => `${path}#${alt}`), videos };
};

/**
 * `gh pr edit --attach` rewrites image refs but appends each video's url at the end of the body in
 * attach order. Moves those urls onto the lines that name the videos.
 */
export const placeVideos = (uploadedBody, videoRefs) => {
  const lines = uploadedBody.split("\n");
  const bare = lines.flatMap((line, index) => (BARE_UPLOAD_URL.test(line) ? [index] : []));
  if (bare.length < videoRefs.length) {
    throw new Error(`attached ${videoRefs.length} videos but only ${bare.length} urls were appended`);
  }
  const appended = new Set(bare.slice(bare.length - videoRefs.length));
  const urls = [...appended].map((index) => lines[index]);
  const kept = lines.filter((_, index) => !appended.has(index));
  videoRefs.forEach((ref, i) => {
    const at = kept.findIndex((line) => line.trim() === ref);
    if (at === -1) throw new Error(`${ref} is not in the body`);
    kept[at] = urls[i];
  });
  while (kept.length > 0 && kept[kept.length - 1] === "") kept.pop();
  return { body: `${kept.join("\n")}\n`, urls };
};
