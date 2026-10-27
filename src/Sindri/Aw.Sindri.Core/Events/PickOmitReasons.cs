namespace Aw.Sindri.Core.Events;

/// <summary>The values of <see cref="OmittedFile.Why"/>.</summary>
public static class PickOmitReasons
{
    /// <summary>The brief's size cap was reached.</summary>
    public const string Cap = "cap";

    /// <summary>The file matches a denied glob.</summary>
    public const string Denied = "denied";

    /// <summary>The file looks like it holds a secret.</summary>
    public const string Secret = "secret";
}
