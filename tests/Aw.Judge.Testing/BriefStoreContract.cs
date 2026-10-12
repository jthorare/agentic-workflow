using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The IBriefStore contract: the same tests run against the in-memory fake and every real adapter
/// (.agents/rules/csharp.md, Seams). A derived class supplies a fresh, empty store and one that can't be used.
/// </summary>
public abstract class BriefStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty store.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IBriefStore> Create(CancellationToken cancellationToken);

    /// <summary>A store that can't be opened (a read-only or missing location).</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IBriefStore> CreateUnavailable(CancellationToken cancellationToken);

    /// <summary>A saved brief reads back by its tool use id with every field.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_saved_brief_reads_back_by_its_tool_use_id()
    {
        var store = await Create(_none);
        var brief = A.Brief().Named("reviewer").Build();

        Expect.That((await store.Save(brief, _none)).IsSuccess, "a new brief is saved");

        var found = await store.ByToolUseId(brief.ToolUseId, _none);
        Expect.That(found.IsSuccess && found.Value == brief, "it reads back with every field");
    }

    /// <summary>An unknown tool use id has no brief.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unknown_tool_use_id_has_no_brief()
    {
        var store = await Create(_none);

        var found = await store.ByToolUseId(ToolUseId.From("toolu_missing"), _none);

        Expect.That(found.IsSuccess && found.Value is null, "no brief, and no error");
    }

    /// <summary>Saving a tool use id twice is AW-JUDGE-004 and keeps the first brief.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Saving_a_tool_use_id_twice_is_a_conflict_and_keeps_the_first()
    {
        var store = await Create(_none);
        var first = A.Brief().Build();
        _ = await store.Save(first, _none);

        var again = await store.Save(first with { Goal = "something else" }, _none);

        Expect.That(again.IsError && again.Error.Code == JudgeErrors.BriefExists.Code, "a second save of the same tool use id is AW-JUDGE-004");
        Expect.That((await store.ByToolUseId(first.ToolUseId, _none)).Value == first, "the first brief is kept unchanged");
    }

    /// <summary>By teammate name: the oldest unmapped brief with that name, whatever the save order.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task By_name_returns_the_oldest_unmapped_brief_with_that_name()
    {
        var store = await Create(_none);
        var newer = A.Brief().WithToolUseId("toolu_newer").Named("reviewer").SavedAt(KnownInstants.Epoch.AddMinutes(2)).Build();
        var older = A.Brief().WithToolUseId("toolu_older").Named("reviewer").SavedAt(KnownInstants.Epoch.AddMinutes(1)).Build();
        var oldest = A.Brief().WithToolUseId("toolu_oldest").Named("reviewer").SavedAt(KnownInstants.Epoch).Build();
        var other = A.Brief().WithToolUseId("toolu_other").Named("tester").SavedAt(KnownInstants.Epoch.AddMinutes(-1)).Build();
        await SaveAll(store, newer, older, oldest, other);
        _ = await store.SetAgentId(oldest.ToolUseId, AgentId.From("agent-1"), _none);

        var found = await store.FirstUnmappedByDispatchName("reviewer", _none);

        Expect.That(found.IsSuccess && found.Value?.ToolUseId == older.ToolUseId, "the oldest reviewer brief not yet mapped (the mapped one is skipped)");
    }

    /// <summary>By teammate name: none when every brief with that name is mapped or none has it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task By_name_finds_nothing_when_no_unmapped_brief_has_that_name()
    {
        var store = await Create(_none);
        var mapped = A.Brief().Named("reviewer").Build();
        await SaveAll(store, mapped, A.Brief().WithToolUseId("toolu_unnamed").Build());
        _ = await store.SetAgentId(mapped.ToolUseId, AgentId.From("agent-1"), _none);

        var byMappedName = await store.FirstUnmappedByDispatchName("reviewer", _none);
        var byUnknownName = await store.FirstUnmappedByDispatchName("nobody", _none);

        Expect.That(byMappedName.IsSuccess && byMappedName.Value is null, "a mapped brief isn't returned");
        Expect.That(byUnknownName.IsSuccess && byUnknownName.Value is null, "an unknown name finds nothing");
    }

    /// <summary>By dispatch: session, prompt and subagent type must all match; the oldest unmapped one wins.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task By_dispatch_matches_session_prompt_and_type_and_returns_the_oldest_unmapped()
    {
        var store = await Create(_none);
        var mapped = A.Brief().WithToolUseId("toolu_mapped").DispatchedBy("s", "p", "coder").SavedAt(KnownInstants.Epoch).Build();
        var wanted = A.Brief().WithToolUseId("toolu_wanted").DispatchedBy("s", "p", "coder").SavedAt(KnownInstants.Epoch.AddMinutes(1)).Build();
        var later = A.Brief().WithToolUseId("toolu_later").DispatchedBy("s", "p", "coder").SavedAt(KnownInstants.Epoch.AddMinutes(2)).Build();
        var otherSession = A.Brief().WithToolUseId("toolu_s2").DispatchedBy("s2", "p", "coder").SavedAt(KnownInstants.Epoch.AddMinutes(-3)).Build();
        var otherPrompt = A.Brief().WithToolUseId("toolu_p2").DispatchedBy("s", "p2", "coder").SavedAt(KnownInstants.Epoch.AddMinutes(-2)).Build();
        var otherType = A.Brief().WithToolUseId("toolu_t2").DispatchedBy("s", "p", "tester").SavedAt(KnownInstants.Epoch.AddMinutes(-1)).Build();
        await SaveAll(store, later, wanted, mapped, otherSession, otherPrompt, otherType);
        _ = await store.SetAgentId(mapped.ToolUseId, AgentId.From("agent-1"), _none);

        var found = await store.FirstUnmappedByDispatch(SessionId.From("s"), PromptId.From("p"), "coder", _none);
        var none = await store.FirstUnmappedByDispatch(SessionId.From("s"), PromptId.From("p"), "reviewer", _none);

        Expect.That(found.IsSuccess && found.Value?.ToolUseId == wanted.ToolUseId, "the oldest unmapped brief matching all three");
        Expect.That(none.IsSuccess && none.Value is null, "no match finds nothing");
    }

    /// <summary>Setting the agent id maps the brief; an unknown tool use id changes nothing and isn't an error.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Setting_the_agent_id_maps_the_brief_and_an_unknown_id_is_a_no_op()
    {
        var store = await Create(_none);
        var brief = A.Brief().Build();
        _ = await store.Save(brief, _none);

        var mapped = await store.SetAgentId(brief.ToolUseId, AgentId.From("agent-7"), _none);
        var unknown = await store.SetAgentId(ToolUseId.From("toolu_missing"), AgentId.From("agent-8"), _none);

        Expect.That(mapped.IsSuccess && unknown.IsSuccess, "both calls succeed");
        Expect.That((await store.ByToolUseId(brief.ToolUseId, _none)).Value == brief with { AgentId = AgentId.From("agent-7") }, "only agent_id changed");
        Expect.That((await store.ByToolUseId(ToolUseId.From("toolu_missing"), _none)).Value is null, "no brief appears for the unknown id");
    }

    /// <summary>A store that can't be opened returns errors and never throws.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unusable_store_returns_errors()
    {
        var store = await CreateUnavailable(_none);
        var brief = A.Brief().Named("reviewer").Build();

        Expect.That((await store.Save(brief, _none)).IsError, "Save returns an error");
        Expect.That((await store.ByToolUseId(brief.ToolUseId, _none)).IsError, "ByToolUseId returns an error");
        Expect.That((await store.FirstUnmappedByDispatchName("reviewer", _none)).IsError, "FirstUnmappedByDispatchName returns an error");
        Expect.That((await store.FirstUnmappedByDispatch(brief.SessionId, brief.PromptId, brief.SubagentType, _none)).IsError, "FirstUnmappedByDispatch returns an error");
        Expect.That((await store.SetAgentId(brief.ToolUseId, AgentId.From("agent-1"), _none)).IsError, "SetAgentId returns an error");
    }

    private static async Task SaveAll(IBriefStore store, params Brief[] briefs)
    {
        foreach (var brief in briefs)
        {
            Expect.That((await store.Save(brief, _none)).IsSuccess, "a brief is saved");
        }
    }
}
