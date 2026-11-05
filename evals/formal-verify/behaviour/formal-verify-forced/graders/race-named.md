---
type: regex
pattern: '\b(?:both|two)\b[^.]{0,200}?\b(?:read|reads|select\w*|see|sees|saw|check\w*|find|finds)\b[^.]{0,200}?\b(?:null|none|empty|free|unclaimed|no owner)\b'
flags: i
---

Planted: both workers read owner null before either update lands (claim.py select, then update).
