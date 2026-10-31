using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The Pin sheet in the shell's one sheet (concept pins spec §3.6): how it opens, what Esc does, and what closes it.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellPinSheetTests
{
    private static async Task<ShellViewModel> Shell()
    {
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(TestContext.Current.CancellationToken);
        return shell;
    }

    private static PinSheetOpen NewPin() => new("api", Card: null, Pin: null, Pins: []);

    [Fact]
    public async Task Opening_the_pin_sheet_shows_it_in_the_shells_sheet_with_no_edge_tab()
    {
        var shell = await Shell();

        shell.OpenPinSheet(NewPin());

        Assert.Equal((true, true, false, false), (shell.IsSheetOpen, shell.IsPinSheet, shell.IsSheetPlaceholderShown, shell.HasEdgeTab));
        Assert.Equal("New pin", shell.SheetTitle);
        Assert.Same(shell.ActivePinSheet, shell.PinSheetContent);
    }

    [Fact]
    public async Task Pin_this_names_its_sheet_and_the_link_round_trips()
    {
        var shell = await Shell();

        shell.OpenPinSheet(new PinSheetOpen("api", new ConceptTileViewModel("billing", "Billing", string.Empty, new Ui.Api.Context.ConceptSize(0, 0, 0, 0), setByPerson: false, terms: null, _ => { }), Pin: null, Pins: []));

        Assert.Equal("Pin this", shell.SheetName);
        Assert.True(DeepLink.TryParse("context/pin", out var link));
        Assert.Equal(DeepLink.PinSheet, link);
        Assert.Equal("context/pin", DeepLink.PinSheet.ToString());
    }

    [Fact]
    public async Task Pin_this_tells_the_sheets_title_after_the_sheet_knows_what_opened_it()
    {
        var shell = await Shell();
        List<string> told = [];
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.SheetTitle))
            {
                told.Add(shell.SheetTitle);
            }
        };

        shell.OpenPinSheet(new PinSheetOpen("api", new ConceptTileViewModel("billing", "Billing", string.Empty, new Ui.Api.Context.ConceptSize(0, 0, 0, 0), setByPerson: false, terms: null, _ => { }), Pin: null, Pins: []));

        Assert.Equal("Pin this", told[^1]);
    }

    [Fact]
    public async Task Esc_on_a_sheet_nothing_was_typed_in_closes_it_and_tells_the_layer()
    {
        var shell = await Shell();
        List<PinSheetClosedEventArgs> told = [];
        shell.PinSheetClosed += (_, e) => told.Add(e);
        shell.OpenPinSheet(NewPin());

        Assert.True(shell.ActivePinSheet!.Escape());   // why: OpenPinSheet just made it

        Assert.Equal((false, false, null), (shell.IsSheetOpen, shell.IsPinSheet, shell.ActivePinSheet));
        Assert.False(Assert.Single(told).Written);
    }

    [Fact]
    public async Task Add_several_mounts_its_own_sheet_and_Esc_closes_it_without_writing()
    {
        var shell = await Shell();
        List<PinSheetClosedEventArgs> told = [];
        shell.PinSheetClosed += (_, e) => told.Add(e);

        shell.OpenPinSheet(new PinSheetOpen("api", Card: null, Pin: null, Pins: [], Several: true));

        Assert.True(shell.IsPinSheet);
        _ = Assert.IsType<PinSeveralViewModel>(shell.PinSheetContent);
        Assert.Null(shell.ActivePinSheet);
        Assert.True(shell.EscapePinSeveral());
        Assert.Equal((false, false, null), (shell.IsSheetOpen, shell.IsPinSheet, shell.PinSheetContent));
        Assert.False(Assert.Single(told).Written);
    }

    [Fact]
    public async Task Esc_on_Add_several_with_a_typed_box_asks_to_discard_and_leaves_the_sheet_open()
    {
        var shell = await Shell();
        shell.OpenPinSheet(new PinSheetOpen("api", Card: null, Pin: null, Pins: [], Several: true));
        var several = Assert.IsType<PinSeveralViewModel>(shell.PinSheetContent);
        several.Text = "Payments: how we take money";

        Assert.True(shell.EscapePinSeveral());

        Assert.True(shell.IsSheetOpen);
        Assert.True(several.IsConfirmingDiscard);
    }
}
