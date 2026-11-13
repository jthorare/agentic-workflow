-- A failed prompt step keeps what the model said (scrubbed by Aw.Redaction and cut at 64 KB by the engine) so the run page can show its Raw answer tab (Workflows page spec §6.2, #3196).
ALTER TABLE workflow_node_run ADD COLUMN raw TEXT;
