-- A decision's reasons (#1248): the model's explanation, a JSON array of strings, redacted and capped like input_json.
-- NULL on a row written before this migration ("not recorded"), which is different from '[]' (the model gave none).
ALTER TABLE decision_details ADD COLUMN reasons TEXT;
