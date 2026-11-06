---
aw-doc: 1
id: harbor-booking-tasks
kind: tasks
title: "Tasks: Harbor ferry booking"
status: draft
parent: harbor-booking
repos: [acme/harbor-web]
---

# Tasks: Harbor ferry booking

_Tasks are ordered by dependency._

## TASK-1: Seat list

- domain: engineering
- complexity: small
- reqs: REQ-U1
- depends: none
- status: todo

**What:** List the free seats of a sailing.

## TASK-2: Hold timer

- domain: engineering
- complexity: medium
- reqs: REQ-E1, REQ-W1
- depends: TASK-1
- status: blocked

**What:** Hold a booked seat for 10 minutes and release it on a failed payment.

## TASK-3: Payment banner

- domain: design
- complexity: small
- reqs: REQ-W1
- depends: TASK-1, TASK-2
- status: blocked

**What:** Show the payment-failed banner.

## Task Summary

| Domain | Count |
|--------|-------|
| engineering | 2 |
| design | 1 |
