namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri wf takeover|handback &lt;run&gt; &lt;node&gt; [--item N] [--json]' (red stub: the real verbs come next).</summary>
internal static class WfAgentNodeCommand
{
    internal static Task<int> TakeOver(string[] args, RecipeDesk desk, Output output, CancellationToken ct) => throw new NotSupportedException();

    internal static Task<int> HandBack(string[] args, RecipeDesk desk, Output output, CancellationToken ct) => throw new NotSupportedException();
}
