# f5-displayname (sindri-ui G1)
State: done, PR open. Head a444f2b4dc1da60dbc6b63ea06918756ce318922 on feat/sindri-ui-f5-displayname.
Found already on main: WorkflowNames, displayName on InstanceRecipe/RecipeView/RecipeSummaryView/InstanceDetail, rows, strip, start picker, recipe sheet.
Fixed: ChildGroup gets optional DisplayName; ChildGroupViewModel header and Open-all name use WorkflowNames; workflow filter disambiguation numbers instead of appending raw id.
Gate: build -warnaserror ok; Ui.Tests unit 2108/2108; integration 661 ok + 10 skipped; Rendering 21; Architecture 128; scoped format clean.
Left: InstanceFrame private fallback (followups unit); RecipeSheet Identity line still carries id@hash (muted mono); goldens lack displayName.
