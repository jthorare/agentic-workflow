namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>The words a dispatch park's question is built from and read back by (one place, so the two can't drift).</summary>
internal static class DispatchWords
{
    /// <summary>The effect couldn't run (SND-WF-010, -015): the question is '&lt;kind&gt; can't run: &lt;reason&gt;'. The allowlist refusal (SND-WF-011) has none.</summary>
    internal const string CantRun = " can't run: ";
}
