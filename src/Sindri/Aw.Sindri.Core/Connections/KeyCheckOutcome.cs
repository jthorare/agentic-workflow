namespace Aw.Sindri.Core.Connections;

/// <summary>What <see cref="IKeyCheck"/> did with a key that no error stopped (#4475).</summary>
public enum KeyCheckOutcome
{
    /// <summary>The preset's service was asked and took the key.</summary>
    Checked,

    /// <summary>The preset has no key check, so nothing was called and the key is unchecked.</summary>
    NotApplicable,
}
