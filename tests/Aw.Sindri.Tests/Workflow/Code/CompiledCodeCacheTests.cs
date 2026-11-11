using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflow.Code;

namespace Aw.Sindri.Tests.Workflow.Code;

/// <summary>The compiled-assembly cache (agent-first PR 9c): the same code and reference set compile once.</summary>
[Lane(TestLane.Unit)]
public sealed class CompiledCodeCacheTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("aw-code-cache-");

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void The_same_code_and_references_compile_once()
    {
        var compiler = new CountingCompiler("refs-1");
        var cache = new CompiledCodeCache(compiler, _directory.FullName);

        var first = cache.Resolve("return 1;");
        var second = cache.Resolve("return 1;");

        Assert.Equal(1, compiler.Calls);
        Assert.NotNull(first.AssemblyPath);
        Assert.Equal(first.AssemblyPath, second.AssemblyPath);
        Assert.True(File.Exists(first.AssemblyPath));
    }

    [Fact]
    public void Different_code_or_a_different_reference_set_compiles_again()
    {
        var one = new CountingCompiler("refs-1");
        _ = new CompiledCodeCache(one, _directory.FullName).Resolve("return 1;");
        _ = new CompiledCodeCache(one, _directory.FullName).Resolve("return 2;");
        var other = new CountingCompiler("refs-2");
        _ = new CompiledCodeCache(other, _directory.FullName).Resolve("return 1;");

        Assert.Equal(2, one.Calls);
        Assert.Equal(1, other.Calls);
    }

    [Fact]
    public void A_body_that_does_not_compile_is_not_cached_and_gives_its_diagnostics()
    {
        var compiler = new CountingCompiler("refs-1", fail: true);
        var cache = new CompiledCodeCache(compiler, _directory.FullName);

        var first = cache.Resolve("nonsense");
        _ = cache.Resolve("nonsense");

        Assert.Null(first.AssemblyPath);
        Assert.Equal("CS0000", Assert.Single(first.Diagnostics).Id);
        Assert.Equal(2, compiler.Calls);
        Assert.Empty(_directory.GetFiles());
    }

    private sealed class CountingCompiler(string referenceSet, bool fail = false) : ICodeCompiler
    {
        public int Calls { get; private set; }

        public string ReferenceSet { get; } = referenceSet;

        public CodeCompilation Compile(string body)
        {
            Calls++;
            return fail
                ? new CodeCompilation([new CodeDiagnostic(1, 1, "CS0000", "fake")], Assembly: null)
                : new CodeCompilation([], [1, 2, 3]);
        }
    }
}
