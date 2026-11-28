namespace Aw.Sindri.Features.AddConnection;

/// <summary>The Config entry that says how an mcp connection signs in (#4482), written at add so a key replace never has to guess from the stored token.</summary>
internal static class ConnectionAuth
{
    /// <summary>The Config key.</summary>
    internal const string Key = "auth";

    /// <summary>An mcp connection added with --key-env: its secret is a pasted key.</summary>
    internal const string Keyed = "key";

    /// <summary>An mcp connection signed in through its server's own browser sign-in.</summary>
    internal const string SignIn = "signin";
}
