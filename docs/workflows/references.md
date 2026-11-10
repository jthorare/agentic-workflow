# References

A reference reads a value from earlier in the run. Write it as `{{node.field}}` inside any string in a node's `config`.

## What you can read

- `{{daily.scheduledFor}}`: the field `scheduledFor` of the node `daily`. The node must be upstream of the one that reads it.
- `{{trigger.scheduledFor}}`: a field of what started the run. Which fields exist depends on the trigger block.
- `{{trigger}}`: the whole trigger payload.
- `item` and `workflow`: the item the run works for, and the workflow's own facts.

Each block page lists the fields it hands on.

## Whole value or part of a string

- A string that is exactly one reference keeps the value's type. `"{{check.urgent}}"` stays a boolean.
- A reference inside other text becomes text. Numbers and booleans print as themselves. Objects and lists print as compact JSON.
- A missing value becomes nothing: the empty string inside text, and null when it is the whole string.

## What the validator refuses

1. A reference to a node that does not exist.
2. A reference to a node that is not upstream.
3. A reference to a field the node's block does not hand on.
4. A whole-value reference whose type does not fit the field (a text where a number is needed).
5. A `{{` that is never closed by `}}`.

The fix is in each finding: connect the node earlier, or read a field that exists.
