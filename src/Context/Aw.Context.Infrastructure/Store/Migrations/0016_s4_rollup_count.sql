-- Feature roll-up (spec 2026-10-27 §3): a roll-up edge keeps how many file pairs (or files) it sums. NULL on every other edge.
ALTER TABLE edge ADD COLUMN rollup_count INTEGER;
