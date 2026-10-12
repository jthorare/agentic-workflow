-- The host lease (factory addendum §17.22 D2, design spec §9.1): one row, the holder and its fencing epoch. A release
-- keeps the row (released = 1), so the epoch only ever rises.
CREATE TABLE lease (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    host TEXT NOT NULL,
    pid INTEGER NOT NULL,
    pid_start TEXT NOT NULL,
    boot_id TEXT NOT NULL,
    epoch INTEGER NOT NULL CHECK (epoch >= 1),
    acquired_at TEXT NOT NULL,
    expires_at TEXT NOT NULL,
    released INTEGER NOT NULL DEFAULT 0 CHECK (released IN (0, 1))
);
