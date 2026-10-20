import { Report } from "./report";

export function run(inputs: number[]): string {
  const report = new Report();
  for (const n of inputs) report.push(n);
  return report.render();
}
