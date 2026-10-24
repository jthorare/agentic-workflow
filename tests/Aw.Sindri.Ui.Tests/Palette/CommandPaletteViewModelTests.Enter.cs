using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>What Enter does (<see cref="CommandPaletteViewModelTests"/>, continued): an entry that shows as runnable does something.</summary>
public sealed partial class CommandPaletteViewModelTests
{
    // K8 (Linux proof): IssueChallenge and RegisterRecipe showed "ready" and did nothing on Enter. An entry is enabled only when
    // the app can do what Enter means (send it, copy it, show its page), and each such entry does.
    [Fact]
    public async Task Every_entry_that_is_enabled_does_something_on_Enter()
    {
        _palette.Open();
        var enabled = _palette.Entries.Where(e => e.IsEnabled).ToList();
        Assert.NotEmpty(enabled);

        foreach (var entry in enabled)
        {
            _ = await _clipboard.SetText(string.Empty, Token);
            var before = (_pages.Count, _server.RebuildCalls);

            await _palette.Run(entry, Token);

            var did = (_pages.Count, _server.RebuildCalls) != before || (entry.Control is { } control && string.Equals(_clipboard.Text, control.Cli, StringComparison.Ordinal));
            Assert.True(did, $"Enter on '{entry.Label}' ({entry.Status}) did nothing");
            _palette.Open();
            _clock.Advance(TimeSpan.FromSeconds(3));
        }
    }

    // A row the app has no sender for is not "ready": it is a command to copy, and Enter copies it.
    [Fact]
    public void A_row_without_a_sender_is_a_command_to_copy_not_called_ready()
    {
        _palette.Open();

        var challenge = Entry("IssueChallenge");

        Assert.Equal((true, Labels.CopyCli(_catalog.Rows.Single(r => r.Action == "IssueChallenge").Cli)), (challenge.IsEnabled, challenge.Status));
        Assert.Equal("ready", Entry("RebuildViews").Status);
        Assert.Equal("ready", Entry("StopProcess").Status);   // it goes to Health, where its confirm lives
    }

    // arch-0: U2 ships Create item, so the palette runs it (closing itself and opening the form); the item sheet's close
    // actions need the version the sheet shows, so the palette says where they live. Nothing U2 ships reads "coming soon".
    [Fact]
    public async Task Create_item_opens_the_form_and_the_close_actions_point_at_the_sheet()
    {
        var created = 0;
        var controls = new CatalogViewModel(_clipboard, _clock);   // the API's catalog has the P3 rows ready
        controls.Show(_api, _server, AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value);
        var palette = new CommandPaletteViewModel(controls, _pages.Add, _history.Add, createItem: () => created++);
        palette.Open();
        var entries = palette.Entries.Where(e => e.IsAction).ToDictionary(e => e.Id, StringComparer.Ordinal);

        Assert.Equal((true, Labels.Ready), (entries["CreateItem"].IsEnabled, entries["CreateItem"].Status));
        foreach (var close in new[] { "CloseItem:done", "CloseItem:cancelled" })
        {
            Assert.Equal((false, Labels.OnItemSheet), (entries[close].IsEnabled, entries[close].Status));
        }

        await palette.Run(entries["CreateItem"], Token);

        Assert.Equal((1, false, "CreateItem"), (created, palette.IsOpen, Assert.Single(_history)));
        Assert.Empty(_pages);
    }

    // Without a form to open (no shell), Create item is not called ready.
