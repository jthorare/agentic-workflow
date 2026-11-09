using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>What the Obsidian todo note card tests share: the fake server, a clock at the golden's "now" (2026-01-01, UTC), a library with the vaults "work" and "inbox", and the calls the card made.</summary>
internal sealed class TodoNoteCardFixture
{
    internal static readonly Uri Url = new("http://127.0.0.1:7410/");

    internal FakeSindriApi Server { get; } = new() { TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("not set")) };

    internal FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    internal TodoNoteCardViewModel Card() => new(() => Url, Server, Clock);

    /// <summary>The routes of every setup call the card has sent, in order, with their last path segment ("check", "set", "import", "stop").</summary>
    internal string[] Calls => [.. Server.TodoNoteSetup.Select(c => c.Route[(c.Route.LastIndexOf('/') + 1)..])];

    /// <summary>The library keeps only these vaults (the golden's repo folders and aw folder stay, as in a real library).</summary>
    /// <param name="vaults">The vault names to keep; none leaves a library with no vault.</param>
    internal void ServeVaults(params string[] vaults)
    {
        var library = JsonNode.Parse(RouteAnswer.Api("library", "ok").Body)!;   // why: the golden is a JSON document
        var roots = library["roots"]!.AsArray();   // why: as above
        foreach (var root in roots.Where(r => string.Equals((string?)r!["kind"], "vault", StringComparison.Ordinal) && !vaults.Contains((string?)r["name"], StringComparer.Ordinal)).ToList())
        {
            _ = roots.Remove(root);
        }

        Server.LibraryAnswer = new RouteAnswer(HttpStatusCode.OK, library.ToJsonString());
    }

    internal void ServeNote(string state, string? lastError = null) => Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn(state, lastError));

    /// <summary>A card that has read the server once, with the vault "work" chosen and the note "Todo.md" typed.</summary>
    internal async Task<TodoNoteCardViewModel> Read(CancellationToken token)
    {
        ServeVaults("work", "inbox");
        var card = Card();
        await card.Refresh(Url, token);
        return card;
    }

    /// <summary>A card that has read the server once and checked a note.</summary>
    /// <param name="check">The check's answer.</param>
    /// <param name="token">Cancels.</param>
    internal async Task<TodoNoteCardViewModel> Checked(string check, CancellationToken token)
    {
        var card = await Read(token);
        Server.TodoNoteCheckAnswer = new RouteAnswer(HttpStatusCode.OK, check);
        await card.Check(token);
        return card;
    }
}
