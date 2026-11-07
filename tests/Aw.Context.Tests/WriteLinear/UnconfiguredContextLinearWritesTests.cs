using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Features.WriteLinear;

namespace Aw.Context.Tests.WriteLinear;

[Trait("Lane", "unit")]
public sealed class UnconfiguredContextLinearWritesTests
{
    private static readonly LinearIssueKey _key = LinearIssueKey.Parse("ENG-12").Value;
    private static readonly LinearClientId _clientId = LinearClientId.Parse("0f8fad5b-d9cb-469f-a165-70867728950e").Value;

    private static ContextScope Scope(ContextCallerKind kind) =>
        new(Settings: null, [], new ContextCaller(kind, kind == ContextCallerKind.Session ? ContextRepoName.From("split-panes") : null, ContextClient.Cli));

    [Fact]
    public async Task A_session_is_refused_with_020_before_the_store_is_said_to_be_unavailable()
    {
        var writes = new UnconfiguredContextLinearWrites();
        var session = Scope(ContextCallerKind.Session);
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal("SND-CONTEXT-020", (await writes.Live(_key, session, ct)).Error.Code);
        Assert.Equal("SND-CONTEXT-020", (await writes.WriteKeyStatus(session, ct)).Error.Code);
        Assert.Equal("SND-CONTEXT-020", (await writes.Create(new LinearIssueDraft(ExternalId.From("t"), "A"), _clientId, session, ct)).Error.Code);
        Assert.Equal("SND-CONTEXT-020", (await writes.Change(_key, new LinearIssueChange(Priority: LinearFieldChange.Set(1)), session, ct)).Error.Code);
        Assert.Equal("SND-CONTEXT-020", (await writes.Comment(_key, "hi", _clientId, session, ct)).Error.Code);
        Assert.Equal("SND-CONTEXT-020", (await writes.EditComment(_key, ExternalId.From("c"), "hi", session, ct)).Error.Code);
        Assert.Equal("SND-CONTEXT-020", (await writes.Attach(_key, new Uri("https://github.com/a/b/pull/1"), "PR", session, ct)).Error.Code);
    }

    [Fact]
    public async Task A_human_is_told_no_write_client_is_wired()
    {
        var writes = new UnconfiguredContextLinearWrites();

        var result = await writes.Comment(_key, "hi", _clientId, Scope(ContextCallerKind.Human), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.StoreUnavailable.Code, result.Error.Code);
    }
}
