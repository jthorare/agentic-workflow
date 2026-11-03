-- The sizes of a call's images as read and as the deciding provider sent them (images spec 3.3a, 'judge why': "-> sent to jev as 1568x1018"):
-- a JSON object {"read":[{"width":w,"height":h}],"sent":[...]}. NULL on a row written before this migration or for a call without images.
ALTER TABLE decision_details ADD COLUMN images TEXT;
