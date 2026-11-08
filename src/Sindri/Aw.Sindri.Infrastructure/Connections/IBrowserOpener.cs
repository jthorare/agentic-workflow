namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>Opens a URL in the owner's browser.</summary>
internal interface IBrowserOpener
{
    /// <summary>Opens the URL.</summary>
    /// <param name="url">The consent URL.</param>
    /// <returns>True when a browser was started; false when none could be (the caller still returns the URL).</returns>
    public bool Open(Uri url);
}
