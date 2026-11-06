---
aw-doc: 1
id: harbor-ferry-tdd
kind: tdd
title: "Harbor ferry booking"
status: draft
generated: write_packet.py
source-sha256: c04172a72b53b44cd4c89504ee8d0e26b8679823439e54d6d92ad2f747f5f20a
repos: [acme/harbor-web]
---

# Harbor ferry booking

Generated from packet.json by write_packet.py; edit the packet, not this file.

## Overview

See [tdd.html#overview](tdd.html#overview).

## Phases

### Ph1 Seat list

### Ph2 Booking form

## Requirements

- **REQ-1** The system shall show the free seats of a sailing.
- **REQ-2** When a rider books a seat, the system shall hold it for 10 minutes.

## Test plan

| Row | Check | Verifies |
|---|---|---|
| TP-1 | Open a sailing; its seats show | Ph1 |
| TP-2 | Book a seat; it shows held | Ph2 |
