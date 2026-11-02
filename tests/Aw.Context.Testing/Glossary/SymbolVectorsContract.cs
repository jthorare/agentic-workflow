using Aw.Context.Core.Engine;
using Aw.Context.Core.Glossary;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Glossary;

/// <summary>
/// The contract every <see cref="ISymbolVectors"/> honours, run against <see cref="FakeSymbolVectors"/> and the code index adapter: a file with embedded
/// symbols comes back with their vectors, a file without any is listed as missing, and nothing comes back for a path that was not asked.
/// </summary>
/// <typeparam name="TSource">The implementation.</typeparam>
public abstract class SymbolVectorsContract<TSource>
    where TSource : ISymbolVectors
{
    /// <summary>The repo the source was made for.</summary>
    protected static ContextRepoName Repo { get; } = ContextRepoName.From("demo");

    /// <summary>A source whose repo holds one embedded symbol, 'ShiftSwap' with vector [1, 0], in 'src/Shift.cs' and nothing in 'src/None.cs'.</summary>
    /// <returns>The source.</returns>
    protected abstract TSource Create();

    /// <summary>The symbol's name and vector come back under its file; the other file is missing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_file_with_vectors_comes_back_with_them_and_a_file_without_is_missing()
    {
        var read = await Create().Read(Repo, ["src/Shift.cs", "src/None.cs"], CancellationToken.None);

        Check(read.IsSuccess && read.Value.Vectors.Count == 1, "one embedded symbol");
        var vector = read.Value.Vectors[0];
        Check(vector.Path == "src/Shift.cs" && vector.Symbol.Name == "ShiftSwap" && vector.Symbol.Vector.ToArray().SequenceEqual([1f, 0f]), "its name, file and vector");
        Check(read.Value.Missing.SequenceEqual(["src/None.cs"], StringComparer.Ordinal), "the file with no vector is missing");
    }

    /// <summary>Only the asked paths are answered.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_path_that_was_not_asked_is_not_answered()
    {
        var read = await Create().Read(Repo, ["src/None.cs"], CancellationToken.None);

        Check(read.IsSuccess && read.Value.Vectors.Count == 0, "no vector for a file that was not asked");
    }

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TSource).Name}: {what}");
        }
    }
}
