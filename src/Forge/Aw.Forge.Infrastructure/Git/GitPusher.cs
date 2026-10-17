using System.Text;
using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Kernel;
using Aw.Redaction;

namespace Aw.Forge.Infrastructure.Git;

/// <summary>
/// Pushes one explicit sha with a lease, hardened against the repository it comes from (spec 10.2). The push runs from a
/// fresh empty git directory that borrows the repository's objects, so nothing in the repository's own config (a
/// credential helper, core.sshCommand, url.*.insteadOf, hooks) reaches git; git's system and global config are off, hooks
/// are off, --no-verify is set, and the credential travels in the environment, not in an argument.
/// </summary>
internal sealed class GitPusher(IRedactor redactor, string gitProgram = "git")
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);
    private static readonly string _nullDevice = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    internal async Task<Result<PushOutcome>> Push(PushRequest request, string? authorizationHeader, CancellationToken cancellationToken)
    {
        var invalid = Check(request);
        if (invalid is not null)
        {
            return invalid;
        }

        var scratch = Path.Combine(Path.GetTempPath(), "aw-forge-push-" + Guid.NewGuid().ToString("N"));
        try
        {
            return await PushFrom(scratch, request, authorizationHeader, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteQuietly(scratch);
        }
    }

    private static Error? Check(PushRequest request)
    {
        if (!IsSha(request.Sha) || (request.LeaseSha is not null && !IsSha(request.LeaseSha)))
        {
            return ForgeErrors.Invalid.With("sha: 40 or 64 hexadecimal characters");
        }

        if (!IsBranch(request.Branch))
        {
            return ForgeErrors.Invalid.With("branch: letters, digits, '/', '.', '_' and '-' only");
        }

        if (request.Remote.Length == 0 || request.Remote.StartsWith('-'))
        {
            return ForgeErrors.Invalid.With("remote: a URL or a path");
        }

        return Directory.Exists(request.RepoDir) ? null : ForgeErrors.Invalid.With("repoDir: not a directory");
    }

    private static bool IsSha(string value) => value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit);

    private static bool IsBranch(string branch) =>
        branch.Length > 0 && !branch.StartsWith('-') && !branch.Contains("..", StringComparison.Ordinal)
        && branch.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '_' or '-');

    private async Task<Result<PushOutcome>> PushFrom(string scratch, PushRequest request, string? authorizationHeader, CancellationToken cancellationToken)
    {
        var baseEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = _nullDevice,
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GIT_ASKPASS"] = "true",
            ["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes",
        };

        var objects = await ObjectsDirectory(request.RepoDir, baseEnvironment, cancellationToken).ConfigureAwait(false);
        if (objects.IsError)
        {
            return objects.Error;
        }

        var init = await Git(["init", "--quiet", "--bare", scratch], baseEnvironment, cancellationToken).ConfigureAwait(false);
        if (init is not { ExitCode: 0 })
        {
            return ForgeErrors.PushFailed.With("git init failed");
        }

        var environment = new Dictionary<string, string>(baseEnvironment, StringComparer.Ordinal)
        {
            ["GIT_DIR"] = scratch,
            ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = objects.Value,
        };
        AddHeader(environment, authorizationHeader);

        var has = await Git(["cat-file", "-e", request.Sha + "^{commit}"], environment, cancellationToken).ConfigureAwait(false);
        return has is { ExitCode: 0 }
            ? Outcome(await Git(PushArguments(request), environment, cancellationToken).ConfigureAwait(false), authorizationHeader)
            : ForgeErrors.Invalid.With("sha: not a commit in the repository");
    }

    private static string[] PushArguments(PushRequest request) =>
    [
        "-c", "core.hooksPath=" + _nullDevice,
        "-c", "credential.helper=",
        "-c", "protocol.ext.allow=never",
        "push", "--no-verify", "--no-recurse-submodules", "--porcelain",
        "--force-with-lease=refs/heads/" + request.Branch + ":" + request.LeaseSha,
        "--", request.Remote, request.Sha + ":refs/heads/" + request.Branch,
    ];

    private Result<PushOutcome> Outcome(ProcessResult? pushed, string? authorizationHeader)
    {
        if (pushed is null)
        {
            return ForgeErrors.PushFailed.With("git could not be started");
        }

        if (pushed.ExitCode == 0)
        {
            return PushOutcome.Pushed;
        }

        var text = pushed.Output + "\n" + pushed.Error;
        if (text.Contains("stale info", StringComparison.Ordinal))
        {
            return PushOutcome.HeadMoved;
        }

        var tail = redactor.Scrub(WithoutHeader(text.Trim(), authorizationHeader)).Text;
        return ForgeErrors.PushFailed.With(tail.Length > 400 ? tail[^400..] : tail);
    }

    // git (or a hook on the remote's side) may print the environment it was given; the credential never leaves in the error.
    private static string WithoutHeader(string text, string? header)
    {
        if (header is null)
        {
            return text;
        }

        var encoded = header[(header.LastIndexOf(' ') + 1)..];
        return text.Replace(header, "[redacted]", StringComparison.Ordinal).Replace(encoded, "[redacted]", StringComparison.Ordinal);
    }

    private async Task<Result<string>> ObjectsDirectory(string repoDir, Dictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var common = await Git(["-C", repoDir, "rev-parse", "--path-format=absolute", "--git-common-dir"], environment, cancellationToken).ConfigureAwait(false);
        if (common is not { ExitCode: 0 })
        {
            return ForgeErrors.Invalid.With("repoDir: not a git repository");
        }

        return Path.Combine(common.Output.Trim(), "objects");
    }

    private Task<ProcessResult?> Git(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken) =>
        ProcessRunner.Run(new ProcessSpec(gitProgram, arguments, environment, WorkingDirectory: null, Timeout: _timeout), cancellationToken);

    private static void AddHeader(Dictionary<string, string> environment, string? header)
    {
        if (header is null)
        {
            return;
        }

        environment["GIT_CONFIG_COUNT"] = "1";
        environment["GIT_CONFIG_KEY_0"] = "http.extraHeader";
        environment["GIT_CONFIG_VALUE_0"] = header;
    }

    /// <summary>The HTTP basic header GitHub accepts for a token over git's smart-HTTP transport.</summary>
    internal static string BasicHeader(string token) =>
        "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + token));

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A scratch directory under the temp root: the next boot's cleanup takes it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }
}
