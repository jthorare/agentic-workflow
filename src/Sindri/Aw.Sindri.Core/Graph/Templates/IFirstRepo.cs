namespace Aw.Sindri.Core.Graph.Templates;

/// <summary>Names the profile's first repo, which a data template's agent step runs in (#3509).</summary>
public interface IFirstRepo
{
    /// <summary>The first repo's name.</summary>
    /// <param name="cancellationToken">Cancels the read of the profile.</param>
    /// <returns>The name, or null when the profile has no repo.</returns>
    public ValueTask<string?> Name(CancellationToken cancellationToken);
}
