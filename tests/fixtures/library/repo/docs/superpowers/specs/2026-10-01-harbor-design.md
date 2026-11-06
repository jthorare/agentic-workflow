---
aw-doc: 1
id: harbor-spec
kind: spec
title: "Harbor web: booking"
status: approved
repos: [acme/harbor-web]
---

# Harbor web: booking

## Goals

- **REQ-1** A rider sees the free seats of a sailing.
- **REQ-2** A rider books a seat.
- **REQ-3** A failed payment releases the seat.
  - AC-1 The seat shows free within 10 s.

## Phases

| Phase | Goal |
|---|---|
| **Ph1** | Seat list and booking form |
| **Ph2** | Payments |

## Proof

| Row | Check | Verifies |
|---|---|---|
| TP-1 | Open a sailing; its seats show. | REQ-1 |
| TP-2 | Fail a payment; the seat frees. | REQ-3, AC-1 |
