namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>What a tile needs before it can sign in.</summary>
internal enum TileMode
{
    /// <summary>A browser sign-in whose provider needs the person's own OAuth client id.</summary>
    ClientId,

    /// <summary>A browser sign-in whose provider also wants the OAuth app's client secret at the token endpoint (GitHub OAuth apps, Google desktop clients).</summary>
    ClientIdAndSecret,

    /// <summary>No browser sign-in: a pasted token.</summary>
    Token,
}
