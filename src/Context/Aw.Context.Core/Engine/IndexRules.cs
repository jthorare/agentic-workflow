using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The owned index's profile keys (owned index spec §5.3 to §5.6, §6, Q7): 'context.index' and the 'github' and 'slack' entries of 'context.sources'. Every range
/// or shape problem is SND-CONTEXT-001 naming the key path; a token that is not a secret pointer is SND-CONTEXT-002 and is never repeated.
/// </summary>
internal static partial class IndexRules
{
    private const int MaxWindowDays = 36_500;
    private const int MaxMaxChunks = 10_000_000;
    private const int MaxRetainDays = 36_500;
    private const int MaxRescueHits = 50;
    private const double MaxDistance = 2.0;
    private const int MaxRepos = 500;
    private const int MaxChannels = 500;
    private const int MaxName = 200;
    private const int MaxHost = 253;
    private static readonly string[] _pointerSchemes = ["env", "file", "keychain", "op"];

    /// <summary>'context.index' checked, with the spec's defaults for what is left out.</summary>
    internal static Result<ContextIndexSettings> Index(IndexSection? section)
    {
        var defaults = new ContextIndexSettings();
        if (section is null)
        {
            return defaults;
        }

        var floors = Floors(section.Floors);
        var problem = Range("context.index.windowDays", section.WindowDays, MaxWindowDays)
            ?? Range("context.index.maxChunks", section.MaxChunks, MaxMaxChunks)
            ?? Range("context.index.retainDays", section.RetainDays, MaxRetainDays)
            ?? (floors.IsError ? floors.Error : null);
        return problem is not null
            ? problem
            : new ContextIndexSettings
            {
                WindowDays = section.WindowDays ?? defaults.WindowDays,
                MaxChunks = section.MaxChunks ?? defaults.MaxChunks,
                RetainDays = section.RetainDays,
                Floors = floors.Value,
                AcknowledgeOnyxLeftover = section.AcknowledgeOnyxLeftover ?? false,
            };
    }

    /// <summary>'context.sources.github' checked, or null success when the profile names none.</summary>
    internal static Result<GitHubSource?> GitHub(GitHubSection? section)
    {
        if (section is null)
        {
            return Kernel.Results.Success<GitHubSource?>(value: null);
        }

        var allow = section.AllowPrivateHost ?? false;
        if ((Repos(section.Repos) ?? Host(section.Host, allow) ?? Pointer("context.sources.github.token", section.Token, "GitHub token")) is { } problem)
        {
            return problem;
        }

        return Kernel.Results.Success<GitHubSource?>(new GitHubSource { Repos = [.. section.Repos ?? []], Host = section.Host, AllowPrivateHost = allow, Token = section.Token });
    }

    /// <summary>'context.sources.slack' checked, or null success when the profile names none.</summary>
    internal static Result<SlackSource?> Slack(SlackSection? section)
    {
        if (section is null)
        {
            return Kernel.Results.Success<SlackSource?>(value: null);
        }

        if ((Channels(section.Channels) ?? Range("context.sources.slack.retainDays", section.RetainDays, MaxRetainDays) ?? Pointer("context.sources.slack.token", section.Token, "Slack bot token")) is { } problem)
        {
            return problem;
        }

        // why: Channels refused a null entry, an empty id or an empty name just above
        var channels = (section.Channels ?? []).Select(static c => new SlackChannel(c!.Id!, c.Name!)).ToList();
        return Kernel.Results.Success<SlackSource?>(new SlackSource { Channels = channels, RetainDays = section.RetainDays, Token = section.Token, Workspace = section.Workspace });
    }

    private static Result<DenseFloors> Floors(FloorsSection? floors)
    {
        var defaults = new DenseFloors();
        var floor = new DenseFloors
        {
            KeepDistance = floors?.KeepDistance ?? defaults.KeepDistance,
            RescueDistance = floors?.RescueDistance ?? defaults.RescueDistance,
            RescueHits = floors?.RescueHits ?? defaults.RescueHits,
        };
        return FloorProblem(floor) is { } problem ? problem : floor;
    }

    private static Error? FloorProblem(DenseFloors floor) =>
        Distance("context.index.floors.keepDistance", floor.KeepDistance)
        ?? Distance("context.index.floors.rescueDistance", floor.RescueDistance)
        ?? (floor.RescueDistance < floor.KeepDistance ? SettingsReader.Invalid("context.index.floors.rescueDistance", "must not be smaller than keepDistance") : null)
        ?? (floor.RescueHits is < 0 or > MaxRescueHits ? SettingsReader.Invalid("context.index.floors.rescueHits", $"must be 0 to {MaxRescueHits.ToString(CultureInfo.InvariantCulture)}") : null);

    private static Error? Distance(string path, double value) =>
        double.IsNaN(value) || value <= 0 || value > MaxDistance ? SettingsReader.Invalid(path, "is a cosine distance above 0 and at most 2") : null;

    private static Error? Range(string path, int? value, int max) =>
        value is null || (value >= 1 && value <= max)
            ? null
            : SettingsReader.Invalid(path, string.Create(CultureInfo.InvariantCulture, $"must be 1 to {max}"));

    private static Error? Repos(IReadOnlyList<string>? repos)
    {
        if (repos is null)
        {
            return null;
        }

        if (repos.Count > MaxRepos)
        {
            return SettingsReader.Invalid("context.sources.github.repos", string.Create(CultureInfo.InvariantCulture, $"lists {repos.Count} repos, at most {MaxRepos}"));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < repos.Count; i++)
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"context.sources.github.repos[{i}]");
            if (repos[i] is not { } repo || !RepoShape.IsMatch(repo))
            {
                return SettingsReader.Invalid(at, $"'{SettingsReader.Printable(repos[i] ?? string.Empty)}' isn't a GitHub repo, written owner/name");
            }

            if (!seen.Add(repo))
            {
                return SettingsReader.Invalid(at, $"'{repo}' is listed twice");
            }
        }

        return null;
    }

    private static Error? Host(string? host, bool allowPrivate)
    {
        const string path = "context.sources.github.host";
        if (host is null)
        {
            return null;
        }

        var (name, portOk) = SplitPort(host);
        var kind = Uri.CheckHostName(name);
        if (!IsPlainText(host) || !portOk || kind == UriHostNameType.Unknown)
        {
            return SettingsReader.Invalid(path, $"'{SettingsReader.Printable(host)}' isn't a host name: write the Enterprise API host alone, such as ghe.example.com or ghe.example.com:8443 (no scheme, path or user name)");
        }

        return (kind != UriHostNameType.Dns || IsLocalName(name)) && !allowPrivate
            ? SettingsReader.Invalid(path, $"'{SettingsReader.Printable(host)}' is an IP address or a local name; an Enterprise host is a public name unless the profile sets allowPrivateHost: true beside it")
            : null;
    }

    private static bool IsPlainText(string host) =>
        host.Length is > 0 and <= MaxHost && !host.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c)) && host.AsSpan().IndexOfAny("/@?#\\") < 0;

    /// <summary>The host without its port, and whether the port (if any) is 1 to 65535. A bare IPv6 address (more than one colon, no brackets) has no valid port.</summary>
    private static (string Name, bool PortOk) SplitPort(string host)
    {
        var bracketEnd = host.StartsWith('[') ? host.IndexOf(']', StringComparison.Ordinal) : -1;
        var colon = bracketEnd >= 0 ? bracketEnd + 1 : host.IndexOf(':', StringComparison.Ordinal);
        if (bracketEnd < 0 && colon != host.LastIndexOf(':'))
        {
            return (host, false);
        }

        if (colon < 0 || colon == host.Length)
        {
            return (host, true);
        }

        var ok = host[colon] == ':' && int.TryParse(host[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 1 and <= 65_535;
        return (host[..colon], ok);
    }

    private static bool IsLocalName(string name) =>
        string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);

    private static Error? Channels(IReadOnlyList<SlackChannelSection?>? channels)
    {
        if (channels is null)
        {
            return null;
        }

        if (channels.Count > MaxChannels)
        {
            return SettingsReader.Invalid("context.sources.slack.channels", string.Create(CultureInfo.InvariantCulture, $"lists {channels.Count} channels, at most {MaxChannels}"));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < channels.Count; i++)
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"context.sources.slack.channels[{i}]");
            var entry = channels[i];
            var problem = Channel(at, entry);
            if (problem is null && entry is { Id: { } id } && !seen.Add(id))
            {
                problem = SettingsReader.Invalid(at + ".id", $"'{id}' is listed twice");
            }

            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    private static Error? Channel(string at, SlackChannelSection? channel) =>
        channel switch
        {
            null => SettingsReader.Invalid(at, "must be a map with an id and a name"),
            { Id: null or "" } => SettingsReader.Invalid(at + ".id", "is required: the channel's Slack id, such as C0123ABC"),
            { Id: var id } when !ChannelShape.IsMatch(id) =>
                SettingsReader.Invalid(at + ".id", $"'{SettingsReader.Printable(id)}' isn't a channel id (C or G, then capitals and digits); direct messages are never read"),
            { Name: var n } when string.IsNullOrEmpty(n) || n.Length > MaxName || n.Any(char.IsControl) =>
                SettingsReader.Invalid(at + ".name", string.Create(CultureInfo.InvariantCulture, $"is required: 1 to {MaxName} characters, no control characters")),
            _ => null,
        };

    /// <summary>A token entry: absent is fine (the pointer is written by 'sindri index connect'); present must be a secret pointer, and is never repeated.</summary>
    private static Error? Pointer(string path, string? token, string what)
    {
        if (token is null)
        {
            return null;
        }

        var colon = token.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && colon < token.Length - 1 && _pointerSchemes.Contains(token[..colon], StringComparer.Ordinal)
            ? null
            : ContextErrors.SourceNotConfigured.With(
                $"{path}: isn't a secret pointer, so it isn't used or shown here. Keep the {what} outside the profile and point to it: env:NAME, file:/path (mode 0600), keychain:service/account or op:vault/item/field; rotate a token that was written into the profile.");
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/(?!\\.{1,2}\\z)[A-Za-z0-9._-]{1,100}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoShape { get; }

    [GeneratedRegex("^[CG][A-Z0-9]{2,30}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ChannelShape { get; }
}
