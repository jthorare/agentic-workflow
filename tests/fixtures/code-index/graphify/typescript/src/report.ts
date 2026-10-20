import { add, square } from "./math";

export class Report {
  private readonly values: number[] = [];

  push(value: number): void {
    this.values.push(square(value));
  }

  total(): number {
    return this.values.reduce((sum, v) => add(sum, v), 0);
  }

  render(): string {
    return format(this.total());
  }
}

function format(n: number): string {
  return `total=${n}`;
}
