// The client's view of GET /orders/{id}: matches the API's openapi.json.
export interface Order {
  id: string;
  total: number;
  currency: string;
}
