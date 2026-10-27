using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Context.Core.Glossary;
using Aw.Context.Testing.Glossary;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.CodeIndexHost;
using SymbolVector = Aw.CodeIndex.Core.Facade.SymbolVector;

namespace Aw.Sindri.Tests.CodeIndexHost;

/// <summary>The adapter behind Context's symbol vectors port: the same contract as the fake, over a <see cref="FakeCodeIndex"/>.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeIndexSymbolVectorsContractTests : SymbolVectorsContract<ISymbolVectors>
{
    private const string Indexed = "0123456789abcdef0123456789abcdef01234567";
    private static readonly RepoName _name = RepoName.From("demo");

    protected override ISymbolVectors Create() => new CodeIndexSymbolVectors(
        new FakeCodeIndex().WithIndex(_name, CommitSha.From(Indexed)).WithVectors(_name, Vector("src/Shift.cs", "ShiftSwap", 1f, 0f)),
        new FixedScope());

    [Fact]
    public async Task A_repo_with_no_embeddings_is_an_error_that_names_the_missing_index()
    {
        var source = new CodeIndexSymbolVectors(new FakeCodeIndex().WithIndex(_name, CommitSha.From(Indexed)), new FixedScope());

        var read = await source.Read(Repo, ["src/Shift.cs"], TestContext.Current.CancellationToken);

        Assert.True(read.IsError);
        Assert.Equal("SND-CODEIDX-013", read.Error.Code);
    }

    [Fact]
    public async Task A_repo_outside_the_scope_is_an_error_too()
    {
        var index = new FakeCodeIndex().WithIndex(_name, CommitSha.From(Indexed)).WithVectors(_name, Vector("src/Shift.cs", "ShiftSwap", 1f, 0f));
        var source = new CodeIndexSymbolVectors(index, new FixedScope("elsewhere"));

        var read = await source.Read(Repo, ["src/Shift.cs"], TestContext.Current.CancellationToken);

        Assert.True(read.IsError);
    }

    private static SymbolVector Vector(string path, string name, params float[] vector) =>
        new(SymbolRef.From($"demo:{path}#{name}"), path, name, SymbolKind.Method, vector);

    private sealed class FixedScope(string repo = "demo") : ICodeIndexScopeSource
    {
        public ValueTask<Result<CodeIndexScope>> Current(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<CodeIndexScope>>(new CodeIndexScope([new CodeIndexRepo(RepoName.From(repo), "/tmp/" + repo, CodeIndexSettings.Create(new CodeIndexSettingsInput(DenyPaths: [])).Value)], new CodeIndexCaller(CallerKind.Human, Repo: null)));
    }
}
