// Storybook renders an empty <template> into the root before the story mounts.
const STORY_ROOT = "#storybook-root > :not(template)";
const DOCS_ROOT = "#storybook-docs > :not(template)";
// The body has no height on an error page, so wait on the message itself.
const STORY_ERROR = ".sb-show-errordisplay #error-message";

/** Waits for the story to render; a story this build lacks fails at once with Storybook's error. */
export const waitForStory = async (page, timeout) => {
  const error = page.locator(STORY_ERROR);
  await page.locator(STORY_ROOT).or(page.locator(DOCS_ROOT)).or(error).first().waitFor({ timeout });
  if (await error.isVisible()) throw new Error(await error.textContent());
};
