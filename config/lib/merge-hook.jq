# Replace every hook command under $event that ends in "# $id" with $entry
# (or remove them when $entry is null). Never touches commands it doesn't own.
#
# Two config shapes:
#   nested (default) — Claude Code settings.json and Codex hooks.json:
#     .hooks[$event] = [{matcher?, hooks: [{type, command, ...}]}]
#   flat ($flat == true) — Cursor hooks.json:
#     {version: 1, hooks: {$event: [{command, matcher?, timeout?, ...}]}}
def tag: "# " + $id;
def owned: ((.command // "") | endswith(tag));
def is_flat: ($ARGS.named.flat // false) == true;

def entry_ok:
  if is_flat then ($entry | owned)
  else ([$entry.hooks[]? | owned] | (length > 0 and all(.)))
  end;

if $entry != null and (entry_ok | not)
then error("every command in the entry must end with '" + tag + "'")
else
  # In place: the first group that holds an owned command is replaced where it stands, so a reinstall never moves an
  # entry. Codex keys its hook trust by event and position and hashes the entry, so a moved or reworded one loses trust.
  # Later owned duplicates are dropped; a group that also holds foreign commands keeps them and the entry is appended.
  def owned_in_group: ((.hooks // []) | any(owned));
  def foreign_only: .hooks = [ (.hooks // [])[] | select(owned | not) ];
  def merged($list):
    if is_flat then
      ([ $list | to_entries[] | select(.value | owned) | .key ] | first // null) as $at
      | if $entry == null or $at == null
        then [ $list[] | select(owned | not) ] + (if $entry == null then [] else [$entry] end)
        else [ $list | to_entries[] | if .key == $at then $entry elif (.value | owned) then empty else .value end ]
        end
    else
      ([ $list | to_entries[] | select(.value | owned_in_group) | .key ] | first // null) as $at
      | if $entry == null or $at == null or ($list[$at].hooks | all(owned) | not)
        then [ $list[] | foreign_only | select(.hooks | length > 0) ] + (if $entry == null then [] else [$entry] end)
        else [ $list | to_entries[]
               | if .key == $at then $entry
                 elif (.value | owned_in_group) then (.value | foreign_only | select(.hooks | length > 0))
                 else .value end ]
        end
    end;
  .hooks = (
    (.hooks // {}) as $h
    | $h + { ($event): merged($h[$event] // []) }
  )
  | if (.hooks[$event] | length) == 0 then .hooks |= del(.[$event]) else . end
  | if is_flat then .version = (.version // 1) else . end
end
