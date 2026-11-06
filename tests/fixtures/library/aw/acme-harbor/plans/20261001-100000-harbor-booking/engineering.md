---
aw-doc: 1
id: harbor-booking-engineering
kind: engineering
title: "Engineering design: Harbor ferry booking"
status: draft
parent: harbor-booking
repos: [acme/harbor-web]
---

# Engineering Design: Harbor ferry booking

_Owner: Engineering Team_

## Approach

Hold a seat as a row with an expiry time; a sweep releases expired rows.

## Architecture Decisions

### ADR-1: Hold seats in the booking table

**Decision:** one row per held seat, with `expires_at`.

## Technical Requirements

- **TR-1:** The system shall expire a hold after 10 minutes, traces to REQ-E1
- **TR-2:** The system shall list free seats in one query, traces to REQ-U1

## Requirement Coverage

- REQ-U1: served by the sailing query
- REQ-E1: served by the hold row and its expiry
- REQ-W1: served by the payment-failed handler
