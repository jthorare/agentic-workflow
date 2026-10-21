# Open decisions: context screen

1. Is "Why this matters" pinned on the canvas or a side panel?
   Options: pinned card top left; right-hand panel; header strip.
   Recommended: pinned card docked in the intent band's empty left side (covers no label or node), collapsible, collapsed by default under 1100 px. It stays in view while panning and keeps the inspector free. Follows: SuperPlane (floating panels on the canvas).
2. Edge labels always on or on hover?
   Options: all always; band-crossing always and in-band on hover; all on hover.
   Recommended: band-crossing always, in-band on hover, so 23 edges stay readable. Follows: SuperPlane (channel pills only where they mean something).
3. Show kind words with technical detail, such as "route · POST /api/links"?
   Options: plain label only; label plus kind and technical detail in the caption.
   Recommended: caption with kind and technical detail, since the people reading the map are developers and the detail tells two routes apart. Follows: SuperPlane (node subtitle).
4. What happens at 900 px wide?
   Options: fit the whole map (62%, text under 11 px); keep 100% and pan; fit with a floor.
   Recommended: fit with a 62% floor, summary card collapsed, the right edge pans. Follows: SuperPlane (default zoom fits the content, minimum zoom lets it pan).
5. Show conflicts between decisions on the map?
   Options: a Conflict footer and a "conflicts" edge; a badge only; inspector only.
   Recommended: footer plus a dashed edge, and the inspector links to Needs you. It is the item's main risk. The second decision is mockup-authored (data-gap context-conflicts); drop it if the API cannot serve conflicts. Follows: none (our addition).
6. Are the band-crossing edges served?
   Answered by the context lane: yes. LensesView.edges[] has the cross-band edges and bands.product.edges[] the in-product ones; no data-gap is marked on edges. Only the second decision, the conflict and the suggested revoke.ts link remain gaps.
7. Should `?item=<id>` show a different map?
   Options: map per item; the Link expiry map for any id.
   Recommended: the Link expiry map for any id, because sample.json has one map. Follows: lead's instruction.
8. Should the file map be a sub-view or live in the inspector?
   Recommended: sub-view (Map | Files, hashes #filemap and #files); a tree needs width. Follows: Linear (full-width list views).
