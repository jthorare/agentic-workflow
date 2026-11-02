---
type: regex
target:
  source: file
  path: app/pricing/discount.py
pattern: 'quantity\s*>=\s*BULK_THRESHOLD|quantity\s*>\s*BULK_THRESHOLD\s*-\s*1|BULK_THRESHOLD\s*<=\s*quantity'
---
