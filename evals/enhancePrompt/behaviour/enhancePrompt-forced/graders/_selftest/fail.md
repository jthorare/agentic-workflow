Done. I added a middleware in api/limits.py that allows 100 requests a minute per key and returns 503 when exceeded. Tests pass.
