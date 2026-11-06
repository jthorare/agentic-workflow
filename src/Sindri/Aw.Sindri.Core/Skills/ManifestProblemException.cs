namespace Aw.Sindri.Core.Skills;

/// <summary>A block.yaml check that failed, with the reason in plain words; the parser turns it into a skipped block.</summary>
internal sealed class ManifestProblemException : Exception
{
    public ManifestProblemException()
    {
    }

    public ManifestProblemException(string message)
        : base(message)
    {
    }

    public ManifestProblemException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
