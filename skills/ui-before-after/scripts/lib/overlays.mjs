const LOCAL_DB_BANNER = "You are connected to your local test DB";
const FEATURE_UPDATE_GOT_IT = "feature-update-dialog-got-it-button";

const AFTER_LOAD_WAIT_MS = 8_000;
const BETWEEN_STATES_WAIT_MS = 500;

/**
 * Closes the "what's new" dialog a fresh browser gets on top of the app. It opens a few seconds
 * after load and hides the page from role queries, so wait for it rather than checking once.
 */
export const closeFeatureUpdate = async (page, waitMs = AFTER_LOAD_WAIT_MS) => {
  const gotIt = page.getByTestId(FEATURE_UPDATE_GOT_IT);
  const appeared = await gotIt.waitFor({ timeout: waitMs }).then(
    () => true,
    () => false,
  );
  if (appeared) await gotIt.click();
};

// The banner can re-render while the app settles; it is cosmetic, so a missed dismiss is fine.
const closeLocalDbBanner = async (page) => {
  const banner = page.getByText(LOCAL_DB_BANNER);
  if (!(await banner.isVisible().catch(() => false))) return;
  await banner
    .locator("xpath=..")
    .getByRole("button", { name: "Dismiss" })
    .click({ timeout: 3_000 })
    .catch(() => {});
};

/** Clears what sits over the screen on a local build: wait for it after a load, glance between states. */
export const closeOverlays = async (page, { afterLoad = false } = {}) => {
  await closeFeatureUpdate(page, afterLoad ? AFTER_LOAD_WAIT_MS : BETWEEN_STATES_WAIT_MS);
  await closeLocalDbBanner(page);
};
