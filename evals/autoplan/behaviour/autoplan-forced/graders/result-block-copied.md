---
type: regex
pattern: '```aw-result(?=[^`]*"artifacts"\s*:\s*\[\s*"/)(?=[^`]*"feature_dir"\s*:\s*"/)'
---

The aw-result block is the result file copied unchanged: artifacts are absolute paths and feature_dir is the absolute
feature dir. A block retyped from memory drops keys or shortens the paths to file names (#2009).
