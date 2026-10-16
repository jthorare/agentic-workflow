namespace Aw.Sindri.Core.Commands;

/// <summary>A request whose catalog row serves several commands and so names, in an authorization refusal, the one command it asked for.</summary>
public interface IRefusalCli
{
    /// <summary>The CLI form to name instead of the row's, or null to name the row's.</summary>
    public string? RefusalCli { get; }
}
