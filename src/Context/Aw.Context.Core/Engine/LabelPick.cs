namespace Aw.Context.Core.Engine;

/// <summary>Whether Jev chooses between candidate labels ('context.models.labeling.pick').</summary>
public enum LabelPick
{
    /// <summary>The model's first candidate stands (the default).</summary>
    Off,

    /// <summary>Jev picks among the candidates.</summary>
    Jev,
}
