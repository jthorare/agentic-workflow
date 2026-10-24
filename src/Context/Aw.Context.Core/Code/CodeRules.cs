namespace Aw.Context.Core.Code;

/// <summary>The fixed caps of the code lens's scans (S-CL). They are not profile keys: a scan stays bounded the same way everywhere.</summary>
public static class CodeRules
{
    /// <summary>The most implementing files whose tests are searched for.</summary>
    public const int MaxImplementingFiles = 200;

    /// <summary>The most data things whose schema files are searched for.</summary>
    public const int MaxThings = 100;

    /// <summary>The most candidate files one search reads.</summary>
    public const int MaxCandidates = 500;

    /// <summary>The most schema files one thing lists.</summary>
    public const int MaxFilesPerThing = 100;

    /// <summary>The most symbols whose callers and callees the code lens asks the code index for (exported ones first).</summary>
    public const int MaxCallSymbols = 100;

    /// <summary>The most files one symbol read of the code index carries.</summary>
    public const int MaxSymbolFiles = 1_000;

    /// <summary>The longest search term, in characters.</summary>
    public const int MaxTermLength = 200;

    /// <summary>The most terms or paths one search call carries (longer sets are chunked).</summary>
    public const int MaxTermsPerCall = 500;

    /// <summary>The shortest searchable term: shorter names match too much.</summary>
    public const int MinTermLength = 4;

    /// <summary>The longest file text the matcher reads, in characters.</summary>
    public const int MaxFileChars = 512_000;
}
