---
aw-doc: 1
id: harbor-epic-ph1-verification
kind: verification
title: "Verification plan: seat list and booking form"
status: draft
parent: harbor-epic-stages
repos: [acme/harbor-web]
---

# Verification Plan: seat list and booking form
created: 2026-10-02T09:00:00Z
app_entry: npm run dev, http://localhost:3000

## Journeys

| row | action | target | assertion | verifies |
|---|---|---|---|---|
| TP-1 | open a sailing | /sailings/1 | free seats show | harbor-epic-stages#Ph1, doc:repo:harbor-web/harbor-spec#REQ-1 |
| TP-2 | book seat 4A | seat map | 4A shows held | harbor-epic-stages#Ph1, doc:repo:harbor-web/harbor-spec#REQ-2 |

## Lenses

| lens | what will be checked | pass condition |
|---|---|---|
| functional | the two journeys | both assertions hold |

## Out of scope

Payments.
