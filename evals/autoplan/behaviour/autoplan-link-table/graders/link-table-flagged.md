---
type: regex
pattern: 'order_note_link[\s\S]{0,400}(?:orders\.note_id|note_id\s+(?:column\s+)?on\s+(?:the\s+)?orders|column on (?:the )?orders)|(?:orders\.note_id|note_id\s+(?:column\s+)?on\s+(?:the\s+)?orders|column on (?:the )?orders)[\s\S]{0,400}order_note_link'
flags: i
---

Planted: the review names the order_note_link table and proposes a foreign-key column (orders.note_id) instead.
