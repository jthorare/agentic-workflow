#!/usr/bin/env bash
# An empty notebook, so NotebookEdit passes its own read-first check and reaches the permission check.
printf '%s\n' '{"cells":[],"metadata":{},"nbformat":4,"nbformat_minor":5}' > probe.ipynb
