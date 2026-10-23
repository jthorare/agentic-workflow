using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Redaction;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Infrastructure.Notifications;

/// <summary>
/// The desk's notices (engine spec §6.3; README X5, X50; engine P6 unit (c)): each is one JSON line of
/// '$AW_SINDRI_HOME/notifications.jsonl', at most once per effect identity, so an activity retry or a re-emission writes
/// nothing twice. An interrupting notice is then shown through <see cref="IOsNotifier"/>; an info one isn't, and is
/// collected into the day's digest ('digest/&lt;yyyy-MM-dd&gt;.jsonl', UTC). The text is scrubbed first (<see cref="IRedactor"/>,
/// <see cref="Redactor.Default"/> without one): a park question can quote a value that looks like a secret. An interrupting
/// notice's line is written before the banner, so a crash between them loses the banner, never the record; an info notice's
/// digest line is written first and its notifications line is the commit marker, so a crash between them repeats a digest
/// line on the retry and never loses one. The identities sent are held in memory, seeded once from the file. Sends are
/// serialized within the process; only serve's worker sends.
/// </summary>
/// <param name="settings">The home.</param>
/// <param name="os">The OS notification centre.</param>
/// <param name="json">The package's AwJson options.</param>
/// <param name="redactor">Scrubs the text; the default patterns when none is registered.</param>
internal sealed class FileDeskNotices(SindriSettings settings, IOsNotifier os, JsonSerializerOptions json, IRedactor? redactor = null) : IDeskNotices, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _sent = new(StringComparer.Ordinal);
    private bool _seeded;

    public void Dispose() => _gate.Dispose();

    public async ValueTask<Result<bool>> Send(DeskNotice notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Seed(cancellationToken).ConfigureAwait(false);
            if (_sent.Contains(notice.Identity))
            {
                return false;
            }

            OwnerOnly.EnsureDirectory(settings.Home);
            var scrubbed = notice with { Text = (redactor ?? Redactor.Default).Scrub(notice.Text).Text };   // a blocked scrub yields its placeholder, never the input
            var line = Line(scrubbed);
            if (scrubbed.Class == NoticeClass.Interrupt)
            {
                await OwnerOnly.Append(settings.NotificationsPath, line, cancellationToken).ConfigureAwait(false);
                await os.Show("Sindri: " + scrubbed.Instance.Value, scrubbed.Text, "workflow/" + scrubbed.Instance.Value, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                OwnerOnly.EnsureDirectory(settings.DigestDirectory);
                var day = scrubbed.At.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                await OwnerOnly.Append(Path.Combine(settings.DigestDirectory, day + ".jsonl"), line, cancellationToken).ConfigureAwait(false);
                await OwnerOnly.Append(settings.NotificationsPath, line, cancellationToken).ConfigureAwait(false);   // the commit marker
            }

            _ = _sent.Add(scrubbed.Identity);
            return true;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Reads the identities the file already names, once; a line that isn't a notice is skipped.</summary>
    private async Task Seed(CancellationToken cancellationToken)
    {
        if (_seeded)
        {
            return;
        }

        if (File.Exists(settings.NotificationsPath))
        {
            foreach (var line in await File.ReadAllLinesAsync(settings.NotificationsPath, cancellationToken).ConfigureAwait(false))
            {
                if (line.Length > 0 && AwJson.Deserialize<DeskNotice>(Encoding.UTF8.GetBytes(line), json) is { IsError: false } read)
                {
                    _ = _sent.Add(read.Value.Identity);
                }
            }
        }

        _seeded = true;
    }

    private byte[] Line(DeskNotice notice) => [.. AwJson.Serialize(notice, json), (byte)'\n'];
}
