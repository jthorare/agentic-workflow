namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>Which rules apply: a draft saves with wiring gaps, activation takes none.</summary>
public enum GraphMode
{
    /// <summary>Saving a revision: a wiring gap (no trigger yet, a node not yet connected) is kept as a finding but doesn't refuse.</summary>
    Draft = 1,

    /// <summary>Activating a revision, and starting a run: every error refuses.</summary>
    Activate,
}
