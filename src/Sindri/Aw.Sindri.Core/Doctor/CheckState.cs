namespace Aw.Sindri.Core.Doctor;

/// <summary>A doctor check's verdict, shown as a word.</summary>
public enum CheckState
{
    /// <summary>Healthy.</summary>
    Ok,

    /// <summary>Needs attention (exit 1).</summary>
    Warn,

    /// <summary>Broken (exit 2).</summary>
    Fail,
}
