using System.Globalization;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using static Aw.Sindri.Features.RegisterMcp.ProviderPlans;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>
/// Builds, and with Apply writes, the per-repo registration of /sindri/mcp for each requested provider: Claude Code's local
/// scope, the checkout's .cursor/mcp.json and the checkout's .codex/config.toml. Never a user-wide entry. A provider that
/// fails is a result, not an error, so one failing never stops the others.
/// </summary>
internal sealed class RegisterMcpService(IApprovedProfile approvals, IRepoLocator locator, IEndpointFile endpoint, IClaudeCli claude, IProviderHost host)
    : IService<RegisterMcpRequest, RegisterMcpResponse>
{
    /// <summary>Owner's call (A), the default: also mark the checkout trusted in Codex's user config so its project file loads at once. False: the human trusts the project in Codex (B).</summary>
    internal bool WriteCodexTrust { get; init; } = true;

    private const string CursorFile = ".cursor/mcp.json";
    private const string CodexFile = ".codex/config.toml";

    public async ValueTask<Result<RegisterMcpResponse>> Handle(RegisterMcpRequest request, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileMissing.With("no profile yet, so no repo is mapped (fix: sindri repo add <path>)");
        }

        var entry = await Entry(request, loaded.Profile.Repos ?? [], cancellationToken).ConfigureAwait(false);
        if (entry.IsError)
        {
            return entry.Error;
        }

        var target = await TargetFor(entry.Value, loaded.Profile, request.Apply, cancellationToken).ConfigureAwait(false);
        var results = new List<ProviderResult>();
        foreach (var provider in Requested(request))
        {
            results.Add(await Register(provider, target, cancellationToken).ConfigureAwait(false));
        }

        return new RegisterMcpResponse(target.RepoName, target.Checkout, target.Arguments, request.Apply, results);
    }

    /// <summary>The requested providers, each once; Claude Code alone when none are named.</summary>
    private static IEnumerable<ProviderFile> Requested(RegisterMcpRequest request) =>
        (request.Providers is { Count: > 0 } asked ? asked : [ProviderFile.Claude]).Distinct();

    /// <summary>The repo, its checkout and the URL on the running control plane's port (else the profile's).</summary>
    private async ValueTask<Target> TargetFor((RepoName Repo, string Checkout) entry, SindriProfile profile, bool apply, CancellationToken cancellationToken)
    {
        var running = await endpoint.Read(cancellationToken).ConfigureAwait(false);
        var port = McpRegistration.Port(running.IsSuccess ? running.Value?.Url : null, profile.Api?.Port);
        return new Target(entry.Repo, entry.Checkout, McpRegistration.Url(port), McpRegistration.Arguments(entry.Repo, port), apply);
    }

    private async ValueTask<ProviderResult> Register(ProviderFile provider, Target target, CancellationToken cancellationToken)
    {
        if (!host.Installed(provider))
        {
            return new ProviderResult(provider, ProviderOutcome.Skipped, "not installed", Fix: null);
        }

        try
        {
            return provider switch
            {
                ProviderFile.Claude => await RegisterClaude(target, cancellationToken).ConfigureAwait(false),
                ProviderFile.Cursor => await RegisterFile(provider, target, CursorFile, Cursor(await ReadInCheckout(target, CursorFile, cancellationToken).ConfigureAwait(false), target.Url, target.Repo), cancellationToken).ConfigureAwait(false),
                ProviderFile.Codex => await RegisterFile(provider, target, CodexFile, Codex(await ReadInCheckout(target, CodexFile, cancellationToken).ConfigureAwait(false), target.Url, target.Repo), cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Not a provider"),
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new ProviderResult(provider, ProviderOutcome.Failed, e.Message, "check the file's permissions in the checkout, then run it again");
        }
    }

    private ValueTask<string?> ReadInCheckout(Target target, string relative, CancellationToken cancellationToken) =>
        host.Read(Path.Combine(target.Checkout, relative), cancellationToken);

    private async ValueTask<ProviderResult> RegisterClaude(Target target, CancellationToken cancellationToken)
    {
        var checkout = target.Checkout;
        var state = Claude(await host.Read(host.UserConfigPath(ProviderFile.Claude), cancellationToken).ConfigureAwait(false), checkout, target.Url, target.Repo);
        var line = McpRegistration.CommandLine(target.Arguments);
        if (state == ClaudeState.Same)
        {
            return new ProviderResult(ProviderFile.Claude, ProviderOutcome.Unchanged, $"local scope in {checkout}", Fix: null);
        }

        if (!target.Apply)
        {
            return new ProviderResult(ProviderFile.Claude, ProviderOutcome.Registered, (state == ClaudeState.Different ? "would replace the entry, then run: " : "would run: ") + line, Fix: null);
        }

        if (state == ClaudeState.Different
            && await claude.Run(["mcp", "remove", "--scope", "local", McpScope.ServerName], checkout, cancellationToken).ConfigureAwait(false) is not { ExitCode: 0 })
        {
            return Failed(checkout, exit: null, "removing the old entry failed", line);
        }

        return await claude.Run(target.Arguments, checkout, cancellationToken).ConfigureAwait(false) switch
        {
            null => Failed(checkout, exit: null, $"the claude CLI couldn't be started in {checkout}", line),
            { ExitCode: not 0 } failed => Failed(checkout, failed.ExitCode, failed.ErrorTail, line),
            _ => new ProviderResult(ProviderFile.Claude, ProviderOutcome.Registered, $"local scope in {checkout}", Fix: null),
        };
    }

    private static ProviderResult Failed(string checkout, int? exit, string tail, string line) => new(
        ProviderFile.Claude,
        ProviderOutcome.Failed,
        exit is { } code ? string.Create(CultureInfo.InvariantCulture, $"claude exited {code}{(tail.Length == 0 ? string.Empty : ": " + tail)}") : tail,
        $"run it by hand in {checkout}: {line} (install Claude Code or set AW_SINDRI_CLAUDE_BIN if it wasn't found)");

    /// <summary>Writes a project file's plan (Codex's also needs the project trusted); a tracked file is never changed.</summary>
    private async ValueTask<ProviderResult> RegisterFile(ProviderFile provider, Target target, string relative, FilePlan plan, CancellationToken cancellationToken)
    {
        var path = Path.Combine(target.Checkout, relative);
        if (plan.Problem is { } problem)
        {
            return new ProviderResult(
                provider,
                ProviderOutcome.Failed,
                $"{path} {problem}",
                $"add the sindri entry to {relative} by hand ({HandEntry(provider == ProviderFile.Codex, target.Url, target.Repo)}), or fix the file and run it again");
        }

        var outcome = ProviderOutcome.Unchanged;
        if (plan.Text is { } text)
        {
            if (await host.Tracked(target.Checkout, relative, cancellationToken).ConfigureAwait(false))
            {
                return new ProviderResult(
                    provider,
                    ProviderOutcome.Failed,
                    $"{path} is tracked by git, so it is refused and left unchanged",
                    $"add the sindri entry to {relative} by hand ({HandEntry(provider == ProviderFile.Codex, target.Url, target.Repo)}) and commit it, or untrack {relative}; then run it again");
            }

            if (target.Apply)
            {
                await host.Write(path, text, cancellationToken).ConfigureAwait(false);
            }

            outcome = ProviderOutcome.Registered;
        }

        return provider == ProviderFile.Codex ? await WithTrust(target, path, outcome, cancellationToken).ConfigureAwait(false) : new ProviderResult(provider, outcome, path, Fix: null);
    }

    /// <summary>Codex loads a project file only for a trusted project: add the trust row when there is none, leave any existing level alone.</summary>
    private async ValueTask<ProviderResult> WithTrust(Target target, string path, ProviderOutcome outcome, CancellationToken cancellationToken)
    {
        var userPath = host.UserConfigPath(ProviderFile.Codex);
        var user = await host.Read(userPath, cancellationToken).ConfigureAwait(false);
        var level = CodexTrust(user, target.Checkout);
        if (string.Equals(level, "trusted", StringComparison.Ordinal))
        {
            return new ProviderResult(ProviderFile.Codex, outcome, path, Fix: null);
        }

        if (level is not null)
        {
            return NeedsTrust(target.Apply, path, $"project trust is \"{level}\", not changed");
        }

        if (!WriteCodexTrust)
        {
            return NeedsTrust(target.Apply, path, "the project isn't trusted");
        }

        if (target.Apply)
        {
            await host.Write(userPath, CodexTrustRow(user, target.Checkout), cancellationToken).ConfigureAwait(false);
        }

        return new ProviderResult(ProviderFile.Codex, ProviderOutcome.Registered, $"{(target.Apply ? "registered; trusted" : "would register; trust")} this project in {userPath}", Fix: null);
    }

    private static ProviderResult NeedsTrust(bool apply, string path, string why) =>
        new(ProviderFile.Codex, ProviderOutcome.NeedsTrust, $"{(apply ? "registered" : "would register")}; {why} - Codex won't load {path} until it is trusted", "trust this project in Codex");

    /// <summary>The named repo, or the one the directory is in (a worktree resolves to its main checkout); else SND-CONTEXT-024.</summary>
    private async ValueTask<Result<(RepoName Repo, string Checkout)>> Entry(RegisterMcpRequest request, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken)
    {
        var name = request.Repo is { } named
            ? (RepoName.TryFrom(named, out var parsed) ? parsed : null)
            : await locator.Locate(request.Directory, repos, cancellationToken).ConfigureAwait(false);
        if (name is { } repo && repos.FirstOrDefault(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal)) is { } entry)
        {
            return (repo, entry.Path);
        }

        return ContextErrors.McpRegisterRepoUnknown.With(request.Repo is { } asked
            ? $"'{asked}' is not a repo the profile names (fix: one of: {string.Join(", ", repos.Select(r => r.Name))})"
            : $"{request.Directory} is in no repo the profile names (fix: run it in the work repo, or pass --repo NAME)");
    }

    /// <summary>Where and what to register: the repo, its main checkout, the MCP URL, the claude CLI's arguments, and whether to write.</summary>
    /// <param name="RepoName">The repo.</param>
    /// <param name="Checkout">Its main checkout.</param>
    /// <param name="Url">The /sindri/mcp URL.</param>
    /// <param name="Arguments">The claude CLI's arguments.</param>
    /// <param name="Apply">True to write.</param>
    private sealed record Target(RepoName RepoName, string Checkout, string Url, IReadOnlyList<string> Arguments, bool Apply)
    {
        public string Repo => RepoName.Value;
    }
}
