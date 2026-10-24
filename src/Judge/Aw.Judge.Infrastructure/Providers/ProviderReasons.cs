using System.Globalization;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>The reason codes the providers return (docs/csharp/judge.md, Providers). The engine adds its own.</summary>
internal static class ProviderReasons
{
    /// <summary>Decided: the model answered within the question's outputs.</summary>
    public const string Model = "model";

    /// <summary>Unavailable: the agent CLI's binary isn't installed.</summary>
    public const string NotInstalled = "not-installed";

    /// <summary>Errored: the adapter threw (a bug in it; FailOpenModelProvider records it so the chain moves on).</summary>
    public const string ProviderThrew = "provider-threw";

    /// <summary>Errored: the agent CLI is installed but the system refused to start it (argument limit, not a program, permissions).</summary>
    public const string SpawnFailed = "spawn-failed";

    /// <summary>Unavailable: no TypeSafe API key in the environment or the keychain (not a failure).</summary>
    public const string NoApiKey = "no-api-key";

    /// <summary>Unavailable: Jev couldn't be reached (offline, DNS, refused).</summary>
    public const string NetworkError = "network-error";

    /// <summary>Errored: the CLI's stdout isn't its output envelope.</summary>
    public const string UnparseableOutput = "unparseable-output";

    /// <summary>Errored: the envelope holds no JSON answer object with a string decision.</summary>
    public const string UnparseableAnswer = "unparseable-answer";

    /// <summary>Errored: the decision isn't one of the question's outputs.</summary>
    public const string OutOfEnum = "out-of-enum";

    /// <summary>Errored: a declared extra field has the wrong JSON type.</summary>
    public const string ExtraOutOfSchema = "extra-out-of-schema";

    /// <summary>Errored: the CLI reported the run failed (is_error, codex turn.failed or error) without an answer.</summary>
    public const string CliReportedError = "cli-reported-error";

    /// <summary>Errored: an image question's input has no usable screenshot paths.</summary>
    public const string InvalidImageInput = "invalid-image-input";

    /// <summary>Errored: the question has more outputs than Jev's 255-option limit.</summary>
    public const string TooManyOptions = "too-many-options";

    /// <summary>Errored: a score question has more levels than Jev's 10.</summary>
    public const string TooManyLevels = "too-many-levels";

    /// <summary>Errored: two questions of a batch share a key.</summary>
    public const string DuplicateQuestion = "duplicate-question";

    /// <summary>Unavailable: the allowlist or config.json leaves Jev out of the chain (not a failure).</summary>
    public const string Excluded = "excluded";

    /// <summary>Errored: the CLI exited non-zero ('exit-1').</summary>
    /// <param name="code">The exit code.</param>
    /// <returns>The reason code.</returns>
    public static string Exit(int? code) => string.Create(CultureInfo.InvariantCulture, $"exit-{code}");

    /// <summary>Errored: Jev answered with a non-success HTTP status ('http-500').</summary>
    /// <param name="status">The status code.</param>
    /// <returns>The reason code.</returns>
    public static string Http(int status) => string.Create(CultureInfo.InvariantCulture, $"http-{status}");
}
