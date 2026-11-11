using Aw.Sindri.Infrastructure.Workflow.Code;

namespace Aw.Sindri.Tests.Workflow.Code;

/// <summary>One compile of a LINQ and Json body before the class's first test (#2822): Roslyn's first compile in a process takes seconds on a cold runner, and a class fixture's setup is not charged to any test, so no fact pays it against the unit cap.</summary>
public sealed class RoslynWarmup
{
    private static readonly Lazy<bool> _warm = new(static () => new CodeCompiler().Compile("return JsonSerializer.Serialize(new[] { 1, 2, 3 }.Where(static n => n > 1));", CancellationToken.None).Succeeded);

    /// <summary>Runs the warm-up compile once per process; a second fixture waits on the first.</summary>
    public RoslynWarmup()
    {
        if (!_warm.Value)
        {
            throw new InvalidOperationException("the warm-up body did not compile");
        }
    }
}
