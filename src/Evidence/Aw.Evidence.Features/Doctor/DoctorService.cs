using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.Doctor;

/// <summary>
/// Read-only stack health. The app's page title and API port are deployment-specific, so they come from the settings
/// (no port configured skips that check); a page on :3000 with another title is a foreign process, never driven.
/// With a worktree, the checks target that worktree's stack slot (SKILL 'Worktree stacks'): the slot file in its root names slot N,
/// slot N serves the web app on 3000 + step * N and the API on the API port + step * N, its database is the {slot}-templated name,
/// and both ports must be served from that worktree.
/// </summary>
internal sealed partial class DoctorService(IStackProbe probe, IEvidenceSettings settings, IRunFiles files) : IService<DoctorRequest, DoctorResponse>
{
    private const int MainWebPort = 3000;

    internal static readonly Uri LoginPage = new(new Uri(HostGuard.DefaultOrigin), "/login");

    public async ValueTask<Result<DoctorResponse>> Handle(DoctorRequest request, CancellationToken cancellationToken)
    {
        var target = request.Worktree is { } worktree
            ? await SlotTargetAsync(worktree.TrimEnd(trimChar: '/'), cancellationToken).ConfigureAwait(false)
            : new Target(Slot: null, MainWebPort, settings.ApiPort, Refusal: null);
        if (target.Refusal is { } refusal)
        {
            return new DoctorResponse(Healthy: false, [refusal]);
        }

        var failures = new List<string>();
        failures.AddRange(await AppFailuresAsync(target, cancellationToken).ConfigureAwait(false));
        failures.AddRange(await DatabaseFailuresAsync(target, cancellationToken).ConfigureAwait(false));
        if (target.ApiPort is { } port && (await probe.PortOpenAsync("localhost", port, cancellationToken).ConfigureAwait(false)).IsError)
        {
            failures.Add(string.Create(CultureInfo.InvariantCulture, $"api-server (:{port}) is not accepting connections"));
        }

        if (target.Slot is { } slot)
        {
            foreach (var served in new[] { target.WebPort, target.ApiPort }.OfType<int>())
            {
                if (await ForeignAsync(served, slot.Worktree, cancellationToken).ConfigureAwait(false) is { } foreign)
                {
                    failures.Add(foreign);
                }
            }
        }

        return new DoctorResponse(failures.Count == 0, failures, target.Slot);
    }

    private async ValueTask<IReadOnlyList<string>> AppFailuresAsync(Target target, CancellationToken cancellationToken)
    {
        if (await settings.AppTitleAsync(cancellationToken).ConfigureAwait(false) is not { } title)
        {
            return [$"the app title is not set: set {settings.AppTitleSource}"];
        }

        var login = target.Slot is null ? LoginPage : new Uri(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{target.WebPort}/login"));
        var page = await probe.PageTitleAsync(login, cancellationToken).ConfigureAwait(false);
        return page.IsError || !string.Equals(page.Value, title, StringComparison.Ordinal)
            ? [string.Create(CultureInfo.InvariantCulture, $":{target.WebPort} does not look like this app (a foreign process, or the stack is not up): refusing to drive it")]
            : [];
    }

    // The container behind the database (or none) is the probe adapter's detail; a slot's own database is checked once the server is ready.
    private async ValueTask<IReadOnlyList<string>> DatabaseFailuresAsync(Target target, CancellationToken cancellationToken)
    {
        if ((await probe.DatabaseReadyAsync(cancellationToken).ConfigureAwait(false)).IsError)
        {
            return ["the database is not ready"];
        }

        return target.Slot is { Database: { } database } slot && (await probe.DatabaseExistsAsync(database, cancellationToken).ConfigureAwait(false)).IsError
            ? [$"database {database} does not exist: prepare the slot's database in {slot.Worktree}"]
            : [];
    }

    // A slot's ports must be served from that worktree, so the evidence shows its code; a port nobody listens on is the port checks' to report.
    private async ValueTask<string?> ForeignAsync(int port, string worktree, CancellationToken cancellationToken)
    {
        var listener = await probe.ListenerDirectoryAsync(port, cancellationToken).ConfigureAwait(false);
        if (listener.IsError)
        {
            return string.Create(CultureInfo.InvariantCulture, $":{port}'s directory could not be read ({listener.Error.Message}): refusing to drive it");
        }

        return listener.Value is not { } directory || directory == worktree || directory.StartsWith(worktree + "/", StringComparison.Ordinal)
            ? null
            : string.Create(CultureInfo.InvariantCulture, $":{port} is served from {directory}, not {worktree}: foreign stack, refusing to drive it");
    }

    private async ValueTask<Target> SlotTargetAsync(string worktree, CancellationToken cancellationToken)
    {
        if (settings.Slots is not { } slots)
        {
            return Target.Refuse($"{worktree} was named but no worktree slots are configured (set UI_EVIDENCE_SLOT_FILE; see SKILL.md \"Worktree stacks\")");
        }

        var path = $"{worktree}/{slots.FileName}";
        var read = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return Target.Refuse(read.Error.Code == EvidenceErrors.FileNotFound.Code
                ? $"{path} does not exist: claim a stack slot there first"
                : $"{path} can't be read: {read.Error.Message}");
        }

        if (SlotNumber(Encoding.UTF8.GetString(read.Value), slots.Key) is not { } number || number > slots.Count)
        {
            return Target.Refuse($"{path} has no {slots.Key} between 1 and {slots.Count}");
        }

        string? database = null;
        if (slots.Database is { } template)
        {
            database = template.Replace("{slot}", number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            if (!DatabaseName.IsMatch(database))
            {
                return Target.Refuse("UI_EVIDENCE_SLOT_DATABASE must name a database of letters, digits and _ (with {slot})");   // it goes into a SQL literal
            }
        }

        var web = MainWebPort + (slots.PortStep * number);
        int? api = settings.ApiPort is { } main ? main + (slots.PortStep * number) : null;
        return new Target(new DoctorSlot(number, worktree, string.Create(CultureInfo.InvariantCulture, $"http://localhost:{web}"), database), web, api, Refusal: null);
    }

    // The first KEY=<digits> line, as doctor.sh's sed read it; 0 and anything not a number are no slot.
    private static int? SlotNumber(string content, string key)
    {
        foreach (var line in content.Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.StartsWith(key + "=", StringComparison.Ordinal) && DigitsOnly.IsMatch(text[(key.Length + 1)..]))
            {
                return int.TryParse(text[(key.Length + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 ? number : null;
            }
        }

        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9_]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex DatabaseName { get; }

    [GeneratedRegex("^[0-9]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex DigitsOnly { get; }

    private sealed record Target(DoctorSlot? Slot, int WebPort, int? ApiPort, string? Refusal)
    {
        internal static Target Refuse(string refusal) => new(Slot: null, MainWebPort, ApiPort: null, refusal);
    }
}
