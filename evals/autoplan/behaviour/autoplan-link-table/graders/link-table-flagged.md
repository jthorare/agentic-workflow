---
type: regex
pattern: 'order_note_link[\s\S]{0,400}(?:note_id|foreign[- ]key|\bFK\b)|(?:note_id|foreign[- ]key|\bFK\b)[\s\S]{0,400}order_note_link'
flags: i
---

Planted: the review names the order_note_link table and proposes a foreign-key column (orders.note_id) instead.
