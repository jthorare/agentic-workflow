import assert from "node:assert/strict";
import { test } from "node:test";

import { impersonate } from "./impersonate.mjs";

// Stands in for a Playwright page: records where it went and whether the banner shows.
const pageWithBanner = (bannerShows) => {
  const visits = [];
  return {
    visits,
    goto: async (url) => visits.push(url),
    getByTestId: (id) => ({
      waitFor: async () => {
        if (id !== "impersonation-banner" || !bannerShows) throw new Error("timeout");
      },
    }),
  };
};

test("opens the seeded profile's impersonate page and waits for the banner", async () => {
  const page = pageWithBanner(true);
  const queries = [];
  const query = async (db, sql) => {
    queries.push([db, sql]);
    return "4f1c";
  };

  await impersonate(page, "http://localhost:3010", "postgres://slot", "general-staff-feature-user", query);

  assert.equal(queries[0][0], "postgres://slot");
  assert.match(queries[0][1], /email LIKE 'general-staff-feature-user-%@vitalize\.care'/);
  assert.deepEqual(page.visits, ["http://localhost:3010/impersonate/4f1c"]);
});

test("refuses a prefix that is not a plain seed email prefix", async () => {
  await assert.rejects(
    impersonate(pageWithBanner(true), "http://x", "postgres://slot", "x'; drop table profile; --", async () => "1"),
    /seed email prefix/,
  );
});

test("says so when the database has no such seeded profile", async () => {
  await assert.rejects(
    impersonate(pageWithBanner(true), "http://x", "postgres://slot", "charge-rn", async () => ""),
    /no seeded profile charge-rn-/,
  );
});

test("fails when the impersonation banner never shows", async () => {
  await assert.rejects(
    impersonate(pageWithBanner(false), "http://x", "postgres://slot", "manager", async () => "1", 10),
    /impersonating manager did not take/,
  );
});
