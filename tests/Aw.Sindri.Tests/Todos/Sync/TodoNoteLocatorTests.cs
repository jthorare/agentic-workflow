using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Features.SyncTodoNote;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>The sync finds its note from the live profile's 'todo.obsidian' and the library's vaults (todo spec §4.5 "Setting it"), the way Check note does.</summary>
public sealed class TodoNoteLocatorTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-locator-").FullName;
    private readonly InMemoryProfiles _profiles = new();

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private TodoNoteLocator Locator() => new(_profiles, (c, r) => FakeContextEngine.Instance.CheckSettings(c, r), new LibraryHost { Home = _home, StateDefault = Path.Combine(_home, ".agentic-workflow") });

    private void Profile(string? vault, string? note, string vaultPath)
    {
        using var context = JsonDocument.Parse(JsonSerializer.Serialize(new { sources = new { library = new { vaults = new[] { new { name = "notes", path = vaultPath } } } } }));
        var todo = vault is null ? null : new TodoSettings(new TodoObsidianSettings(vault, note));
        var profile = new SindriProfile(1, "thor", Context: context.RootElement.Clone(), Todo: todo);   // not checked: the locator re-applies the key's rules to what the file holds now
        _profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_without_the_key_has_no_note()
    {
        Profile(vault: null, note: null, vaultPath: Path.Combine(_home, "vault"));

        var located = await Locator().Locate(Ct);

        Assert.True(located.IsSuccess);
        Assert.Null(located.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_key_resolves_to_the_vault_folder_and_the_note_inside_it()
    {
        var vault = Directory.CreateDirectory(Path.Combine(_home, "vault")).FullName;
        Profile("notes", "Daily/Todo.md", vault);

        var located = await Locator().Locate(Ct);

        Assert.Equal(new NotePlace("notes", "Daily/Todo.md", vault), located.Value);
        Assert.Equal(Path.Combine(vault, "Daily/Todo.md"), located.Value!.Path);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_vault_path_with_a_tilde_starts_at_the_home_folder()
    {
        Profile("notes", "Todo.md", "~/vault");

        var located = await Locator().Locate(Ct);

        Assert.Equal(Path.Combine(_home, "vault"), located.Value!.Root);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_vault_the_library_refuses_cannot_be_resolved_to_a_folder()
    {
        Profile("notes", "Todo.md", _home);   // a vault may not be the home folder

        var located = await Locator().Locate(Ct);

        Assert.Equal(SindriErrors.TodoNoteKey.Code, located.Error.Code);
        Assert.Contains("can't be resolved to a folder", located.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_note_path_that_climbs_out_of_the_vault_is_refused_by_the_key_rules()
    {
        Profile("notes", "../Todo.md", Path.Combine(_home, "vault"));

        var located = await Locator().Locate(Ct);

        Assert.Equal(SindriErrors.TodoNoteKey.Code, located.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_that_cannot_be_loaded_is_the_error()
    {
        _profiles.Live = SindriErrors.ProfileInvalid.With("/home/profile.yaml: user: must be non-empty text");

        var located = await Locator().Locate(Ct);

        Assert.Equal(SindriErrors.ProfileInvalid.Code, located.Error.Code);
    }
}
