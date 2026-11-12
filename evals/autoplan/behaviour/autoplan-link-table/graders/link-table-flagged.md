---
type: regex
pattern: '(?:link|join)[- ]table[\s\S]{0,300}(?:foreign[- ]key|\bFK\b|column on|column)|(?:foreign[- ]key|\bFK\b|column)[\s\S]{0,300}(?:link|join)[- ]table|order_note_link[\s\S]{0,300}column'
flags: i
---

Planted: the review flags the order_note_link table as replaceable by a foreign-key column on an existing table.
