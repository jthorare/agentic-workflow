---
aw-doc: 1
id: harbor-booking-product
kind: product
title: "Product requirements: Harbor ferry booking"
status: draft
parent: harbor-booking
repos: [acme/harbor-web]
issues: [412]
---

# Product Requirements: Harbor ferry booking

_Owner: Product Team_

## Problem

Riders phone the harbor office to learn which sailings have seats.

## Requirements

### Ubiquitous
- REQ-U1: The system shall show the free seats of each sailing.

### Event-driven
- REQ-E1: When a rider books a seat, the system shall hold it for 10 minutes.

### Unwanted
- REQ-W1: If payment fails, the system shall release the held seat.

## Acceptance Criteria

- **AC-1:** When a rider opens a sailing, its free seats show within 2 s.
- **AC-2:** When a rider books a seat, it shows as held for the next 10 minutes.
- **AC-3:** When payment fails, the seat shows free again within 10 s.

## Success Metrics

| Metric | Target |
|--------|--------|
| Phone calls to the harbor office | down 50% |

## Traceability

| Requirement | Acceptance Criteria | Success Metric |
|------------|-------------------|----------------|
| REQ-U1 | AC-1 | calls down 50% |
| REQ-E1 | AC-2 | seats held, not double-booked |
| REQ-W1 | AC-3 | no stuck seats |
