using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflow.Code;

namespace Aw.Sindri.Tests.Workflow.Code;

/// <summary>The validator's compile check (#2727): it goes through the runner's compiled-code cache, so a body compiles once whether it passes or fails, and it stops on the caller's token before it compiles.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeCheckerTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("aw-code-check-");

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void A_good_body_checked_twice_compiles_once()
    {
        var compiler = new CountingCompiler();
        var checker = new CodeChecker(new CompiledCodeCache(compiler, _directory.FullName));

        var first = checker.Check("return 1;", TestContext.Current.CancellationToken);
        var second = checker.Check("return 1;", TestContext.Current.CancellationToken);

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(1, compiler.Calls);
    }

    [Fact]
    public void A_bad_body_checked_twice_compiles_once_and_gives_the_same_problems()
    {
        var compiler = new CountingCompiler();
        var checker = new CodeChecker(new CompiledCodeCache(compiler, _directory.FullName));

        var first = checker.Check("bad", TestContext.Current.CancellationToken);
        var second = checker.Check("bad", TestContext.Current.CancellationToken);

        Assert.Equal("CS1002", Assert.Single(first).Rule);
        Assert.Equal(first, second);
        Assert.Equal(1, compiler.Calls);
    }

    [Fact]
    public void A_checked_body_is_not_compiled_again_when_it_runs()
    {
        var compiler = new CountingCompiler();
        var cache = new CompiledCodeCache(compiler, _directory.FullName);

        _ = new CodeChecker(cache).Check("return 1;", TestContext.Current.CancellationToken);
        var run = cache.Resolve("return 1;", TestContext.Current.CancellationToken);

        Assert.NotNull(run.AssemblyPath);
        Assert.Equal(1, compiler.Calls);
    }

    [Fact]
    public void A_cancelled_check_throws_before_it_compiles()
    {
        var compiler = new CountingCompiler();
        var checker = new CodeChecker(new CompiledCodeCache(compiler, _directory.FullName));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        _ = Assert.Throws<OperationCanceledException>(() => checker.Check("return 1;", cancelled.Token));

        Assert.Equal(0, compiler.Calls);
    }

    [Fact]
    public void A_bad_body_that_is_then_fixed_compiles_again_and_passes()
    {
        var compiler = new CountingCompiler();
        var checker = new CodeChecker(new CompiledCodeCache(compiler, _directory.FullName));

        var bad = checker.Check("bad", TestContext.Current.CancellationToken);
        var fixedBody = checker.Check("return 1;", TestContext.Current.CancellationToken);

        _ = Assert.Single(bad);
        Assert.Empty(fixedBody);
        Assert.Equal(2, compiler.Calls);
    }

    /// <summary>Counts compiles; the body "bad" fails with one CS1002, any other body compiles.</summary>
    private sealed class CountingCompiler : ICodeCompiler
    {
        public int Calls { get; private set; }

        public string Fingerprint => "refs-1";

        public CodeCompilation Compile(string body, CancellationToken cancellationToken)
        {
            Calls++;
            return string.Equals(body, "bad", StringComparison.Ordinal)
                ? new CodeCompilation([new CodeDiagnostic(1, 4, "CS1002", "; expected")], Assembly: null)
                : new CodeCompilation([], [1, 2, 3]);
        }
    }
}
