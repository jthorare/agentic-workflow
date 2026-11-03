-- The size Jev was sent for each image that was downscaled (images spec 3.3a, 'judge why': "image 1 -> sent to jev as 1568x1018"): a JSON array with
-- one entry per image of the call, {"width":w,"height":h} for a scaled one and null for one sent as read. NULL on a row written before this migration,
-- for a call without images, or when nothing was scaled.
ALTER TABLE decision_details ADD COLUMN sent_images TEXT;
