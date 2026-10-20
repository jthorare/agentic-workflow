using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The detail sheet and its subject (UI spec §3.3, §4.2): open, closed to the edge tab, reopened. <see cref="Navigate"/> opens it.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>True while the detail sheet is open.</summary>
    public bool IsSheetOpen { get; private set; }

    /// <summary>The sheet's subject (the deep link's subject); kept after the sheet closes for the edge tab; empty before the first.</summary>
    public string SheetSubject => SheetLink?.Subject ?? string.Empty;

    /// <summary>The link the sheet was opened by (its kind and subject); null before the first.</summary>
    public DeepLink? SheetLink { get; private set; }

    /// <summary>True when the closed sheet shows its edge tab (a subject was opened before).</summary>
    public bool HasEdgeTab => SheetSubject.Length > 0;

    /// <summary>The sheet shows when it's open over a visible page.</summary>
    public bool IsSheetShown => IsSheetOpen && IsConnected;

    /// <summary>The sheet's accessible name: the kind's noun and the subject ("Workflow X"); an id that names itself stays bare.</summary>
    public string SheetName => SheetLink is { } link && Labels.KindNoun(link.Kind) is { Length: > 0 } noun ? noun + " " + NamedSubject : SheetSubject;

    /// <summary>The edge tab's accessible name: "Reopen" and the sheet's name, its kind's noun in lower case.</summary>
    public string EdgeTabName => Labels.Reopen + " " + (SheetLink is { } link && Labels.KindNoun(link.Kind) is { Length: > 0 } noun ? char.ToLowerInvariant(noun[0]) + noun[1..] + " " + NamedSubject : SheetSubject);

    /// <summary>The edge tab shows when the sheet is closed and has a subject, over a visible page.</summary>
    public bool IsEdgeTabShown => !IsSheetOpen && HasEdgeTab && IsConnected;

    /// <summary>True while the sheet shows its "not served yet" placeholder: until a lane's content claims the link's kind.</summary>
#pragma warning disable IDE0072 // the lanes populate the kinds they claim; every other kind keeps the placeholder
    public bool IsSheetPlaceholderShown => SheetLink?.Kind switch
    {
        DeepLinkKind.Item => false,
        DeepLinkKind.NeedsYou or DeepLinkKind.Decision => false,
        // LANE ANCHOR U3': one `DeepLinkKind.X or … => false` arm claims the kinds from the placeholder (kinds: NeedsYou, Decision), as screens spec §8 assigns them. `Item` is U2's item sheet (#197): its arm, `DeepLinkKind.Item => false`, is written above both markers. `HomeNeedsYou` is a page link, not a sheet. Insert immediately above this marker, never between the markers.
        // LANE ANCHOR U8: one `DeepLinkKind.X or … => false` arm claims the kinds (kinds: Workflow); U8b re-bases the item sheet (§4.7), which is content and adds no arm. Insert immediately below this marker, never between the markers.
        DeepLinkKind.Workflow => false,
        // LANE ANCHOR U10: one `DeepLinkKind.X or … => false` arm claims the kinds (kinds: Recipe), U10a's read-only recipe sheet (UI spec §4.3). System, Service and Job are claimed later, at this switch, by the phase that renders them (screens spec §8). Insert immediately below this marker, never between the markers.
        DeepLinkKind.Recipe => false,
        // LANE ANCHOR U9: one `DeepLinkKind.X or … => false` arm claims the kinds (kinds: Node), the node card of the Context page (U9b writes the arm; until then Node still shows the placeholder). System, Service and Job are claimed later, at this switch, by the phase that renders them (screens spec §8). Insert immediately below this marker, never between the markers.
        // LANE ANCHOR U11a: one `DeepLinkKind.X or … => false` arm claims the kinds (kinds: Session), U11a's session sheet (collector spec CU-3); the arm is written by the sheet's builder (T4), not by the contract. Insert immediately below this marker, never between the markers.
        DeepLinkKind.Session => false,
        DeepLinkKind.Job => false,
        _ => true,
    };
#pragma warning restore IDE0072

    /// <summary>Opens the sheet on a deep link's subject, keeping the link's kind (an item and a workflow with one subject are two sheets).</summary>
    /// <param name="link">The link, with a subject.</param>
    internal void OpenSheet(DeepLink link)
    {
        SheetLink = link;
        IsSheetOpen = true;
        ForgetRowSheetUnless(link);
        OnSheetOpened(link);
        Changed(nameof(IsItemSheet), nameof(IsNeedsYouSheet), nameof(IsDecisionSheet), nameof(SheetLink), nameof(SheetSubject), nameof(SheetName), nameof(EdgeTabName), nameof(IsSheetOpen), nameof(HasEdgeTab), nameof(IsSheetShown), nameof(IsEdgeTabShown), nameof(IsSheetPlaceholderShown));
    }

    /// <summary>Closes the sheet (Esc); its subject stays on the edge tab.</summary>
    internal void CloseSheet()
    {
        IsSheetOpen = false;
        _ = NeedsYou.CloseSheet();
        Changed(nameof(IsSheetOpen), nameof(IsSheetShown), nameof(IsEdgeTabShown));
    }

    /// <summary>Opens the sheet again on its last subject (the edge tab).</summary>
    internal void ReopenSheet()
    {
        if (!HasEdgeTab)
        {
            return;
        }

        (IsSheetOpen, OpenerRow, OpenerStrip, OpenerRecipeGroup, OpenerSession) = (true, null, null, null, null);
        OnJobSheetReopened();
        Changed(nameof(IsSheetOpen), nameof(IsSheetShown), nameof(IsEdgeTabShown));
    }
}
