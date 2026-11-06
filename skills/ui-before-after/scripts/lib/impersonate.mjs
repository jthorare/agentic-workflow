import { execFile } from "node:child_process";
import { promisify } from "node:util";

const BANNER_TIMEOUT_MS = 60_000;
const SEED_PREFIX = /^[a-z][a-z-]*$/;

const psql = async (db, sql) => (await promisify(execFile)("psql", [db, "-tAc", sql])).stdout.trim();

/**
 * Switches the signed-in admin to a seeded profile, found by its seed email prefix
 * ("general-staff-feature-user", "manager", "charge-rn", ...) in the slot's database.
 */
export const impersonate = async (page, base, db, prefix, query = psql, timeout = BANNER_TIMEOUT_MS) => {
  if (!SEED_PREFIX.test(prefix)) throw new Error(`impersonate needs a seed email prefix, got ${prefix}`);
  const id = await query(
    db,
    `SELECT id FROM public.profile WHERE email LIKE '${prefix}-%@vitalize.care' ORDER BY email DESC LIMIT 1`,
  );
  if (!id) throw new Error(`no seeded profile ${prefix}-…@vitalize.care in ${db}`);
  await page.goto(new URL(`/impersonate/${id}`, base).toString());
  await page
    .getByTestId("impersonation-banner")
    .waitFor({ timeout })
    .catch(() => {
      throw new Error(`impersonating ${prefix} did not take: no impersonation banner`);
    });
};
