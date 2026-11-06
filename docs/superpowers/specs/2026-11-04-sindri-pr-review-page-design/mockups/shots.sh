#!/bin/bash
# Renders every mockup PNG (list, review page, sheets) with headless Chrome. usage: bash shots.sh
cd "$(dirname "$0")" || exit 1
for s in shots-list.sh shots-review.sh shots-sheets.sh; do bash "$s" || exit 1; done
