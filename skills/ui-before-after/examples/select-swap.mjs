// ui-before-after scenario template for moving pickers from the ui-deprecated combobox onto the DS Select.
// Copy to <out>/pr-<n>/scenario.mjs and fill in ROUTE, READY_TEST_ID and PICKERS.

const ROUTE = "/manager-schedule"; // reached through redirects; seed ids differ per database
const READY_TEST_ID = "schedule-sticky-column-smart-controls";

// One entry per moved picker. `scope` narrows the page to the picker's area (a test id, or null).
// main shows the old trigger (usually a test id); the PR shows the Select's button, named by its label.
const PICKERS = [
  {
    name: "staff-search",
    scope: "schedule-sticky-column-smart-controls",
    oldTrigger: "nm-search-bar",
    newLabel: /Search staff$/,
    pick: "NurseManager TestUser3",
  },
];

const area = (page, picker) => (picker.scope ? page.getByTestId(picker.scope) : page);

// `.first()`: when the PR keeps the old test id on the Select's wrapper, both halves of the `.or()`
// match (the wrapper and its button) and an ambiguous click fails.
const trigger = (page, picker) =>
  area(page, picker)
    .getByTestId(picker.oldTrigger)
    .or(area(page, picker).getByRole("button", { name: picker.newLabel }))
    .first();

// main: the cmdk popover; PR: the Select menu (its search box and list together).
const openMenu = (page) =>
  page
    .locator("[cmdk-root]")
    .or(page.getByRole("listbox").locator("xpath=ancestor::*[.//input or .//*[@role='listbox']][1]"));

const pickerStates = (picker) => [
  {
    name: `${picker.name}-field`,
    claim: `${picker.name}: field with nothing picked`,
    crop: (page) => trigger(page, picker),
    aria: true,
  },
  {
    name: `${picker.name}-menu-open`,
    claim: `${picker.name}: menu open`,
    act: async (page) => trigger(page, picker).click(),
    crop: openMenu,
  },
  {
    name: `${picker.name}-picked`,
    claim: `${picker.name}: one item picked`,
    act: async (page) => {
      await page.getByRole("option", { name: picker.pick }).click();
      await page.keyboard.press("Escape");
    },
    crop: (page) => trigger(page, picker),
    aria: true,
  },
];

export default {
  route: ROUTE,
  ready: { testId: READY_TEST_ID },
  states: PICKERS.flatMap(pickerStates),
};
