# 0001: Toggling orientation keeps the focused pane first

Status: accepted (SPL-103)

When a person toggles a split between vertical and horizontal, the focused pane becomes the first pane, so the
cursor stays where the eye already is. Swapping the pane order instead was considered and rejected: it moved the
cursor to the other side of the screen on every toggle.
