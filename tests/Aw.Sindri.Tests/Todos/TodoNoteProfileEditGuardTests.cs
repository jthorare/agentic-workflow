using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.Todos;

/// <summary>Issue 2385 items 1 and 2: the todo note's '--if-hash' guard checks the text the write replaces, and 'todo.obsidian' is live (no approval), so setting it is no approval drift.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteProfileEditGuardTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: golden\nrepos: []\ncontext:\n  sources:\n    library:\n      vaults:\n        - { name: notes, path: /work/notes }\n";
    private const string HandEdited = "schemaVersion: 1\nuser: someone-else\nrepos: []\ncontext:\n  sources:\n    library:\n      vaults:\n        - { name: notes, path: /work/notes }\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private TodoNoteProfileEdit Edit(Action<string>? beforePlan = null, Action<string>? beforeSave = null)
    {
        var real = new YamlProfileKeyWriter(_files.Settings, new NoCache());
        return new TodoNoteProfileEdit(new Interfering(real, _files.Settings.ProfilePath, beforePlan, beforeSave), new YamlProfileTextParser(ProfileFiles.Options), _files.Source);
    }

    [Fact]
    public async Task An_edit_landing_between_the_guard_and_the_plan_read_is_refused_and_not_overwritten()
    {
        _files.Write(Base);
        var seen = (await _files.Source.Load(Token)).Value.Hash.Value;
        var edit = Edit(beforePlan: static path => File.WriteAllText(path, HandEdited));

        var result = await edit.Set("notes", "Todo.md", seen, Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.TodoConflict.Code, result.Error.Code);
        Assert.Equal(HandEdited, await File.ReadAllTextAsync(_files.Settings.ProfilePath, Token));
    }

    [Fact]
    public async Task An_edit_landing_between_the_plan_and_the_rename_is_refused_even_without_an_ifHash()
    {
        _files.Write(Base);
        var edit = Edit(beforeSave: static path => File.WriteAllText(path, HandEdited));

        var result = await edit.Set("notes", "Todo.md", ifHash: null, Token);

        Assert.True(result.IsError);
        Assert.Equal(HandEdited, await File.ReadAllTextAsync(_files.Settings.ProfilePath, Token));
    }

    [Fact]
    public async Task Setting_the_note_leaves_the_profile_hash_and_the_approval_diff_alone_while_another_drifted_key_still_shows()
    {
        _files.Write(Base);
        var approved = (await _files.Source.Load(Token)).Value;

        var set = await Edit().Set("notes", "Todo.md", ifHash: null, Token);
        var afterSet = (await _files.Source.Load(Token)).Value;

        Assert.True(set.IsSuccess);
        Assert.Equal(approved.Hash, afterSet.Hash);
        Assert.Empty(ProfileDiff.Between(approved.Profile, afterSet.Profile));

        _files.Write((await File.ReadAllTextAsync(_files.Settings.ProfilePath, Token)).Replace("user: golden", "user: someone-else", StringComparison.Ordinal));
        var drifted = (await _files.Source.Load(Token)).Value;

        Assert.NotEqual(approved.Hash, drifted.Hash);
        Assert.Equal(["user"], ProfileDiff.Between(approved.Profile, drifted.Profile));
    }

    private sealed class NoCache : IProfileCache
    {
        public void Invalidate()
        {
        }
    }

    private sealed class Interfering(IProfileKeyWriter inner, string path, Action<string>? beforePlan, Action<string>? beforeSave) : IProfileKeyWriter
    {
        public Result<ProfileKeyEdit> Plan(string keyPath, string value) => inner.Plan(keyPath, value);

        public Result<ProfileKeyEdit> PlanText(string keyPath, string value) => inner.PlanText(keyPath, value);

        public Result<ProfileKeyEdit> PlanTexts(IReadOnlyList<(string KeyPath, string Value)> values)
        {
            beforePlan?.Invoke(path);
            return inner.PlanTexts(values);
        }

        public Result<ProfileKeyEdit> PlanRemove(string keyPath) => inner.PlanRemove(keyPath);

        public Result<string> Save(ProfileKeyEdit edit)
        {
            beforeSave?.Invoke(path);
            return inner.Save(edit);
        }
    }
}
