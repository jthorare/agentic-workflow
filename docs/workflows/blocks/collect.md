# `collect`

Ends a [`for_each`](for_each.md) and gathers what each item produced.

| Config | Type | Required | Meaning |
|---|---|---|---|
| `value` | reference | yes | What one item contributes, evaluated once per item, usually one whole reference such as `{{summary.line}}`; it keeps its type (a number stays a number, an object an object) |

Outputs (the node's own row, written after the last item): `items`, every item's value in item order, and `count`, how many values it holds.

- An item whose branch never reached the `collect` (a `condition` sent it elsewhere) adds nothing, so `count` can be lower than the `for_each`'s.
- A value is stored like any node output and cut at 64 KB. A cut value fails the `collect` (`the value of item N was cut at 64 KB`); collect a smaller field.
- A `collect` that no `for_each` reaches is `GRAPH-LOOP-002`.

## Example

See the [`for_each` page](for_each.md#example): the `gather` node collects each item's `summary.line`, and `tell` reads `{{gather.items}}`.
