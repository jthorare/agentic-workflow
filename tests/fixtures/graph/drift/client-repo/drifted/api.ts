// The client's view of GET /orders/{id}: the API now also returns `currency`, and this file does not know it.
export interface Order {
  id: string;
  total: number;
}
