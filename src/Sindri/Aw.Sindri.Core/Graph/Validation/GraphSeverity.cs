namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>How serious a finding is.</summary>
public enum GraphSeverity
{
    /// <summary>The graph can't be used as written (a draft may still save it when the finding is a wiring gap).</summary>
    Error = 1,

    /// <summary>Something the validator couldn't check yet (a connection or memory name nobody answers for); never blocks.</summary>
    Warning,
}
