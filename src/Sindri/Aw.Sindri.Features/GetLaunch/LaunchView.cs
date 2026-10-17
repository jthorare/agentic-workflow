using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetLaunch;

/// <summary>A launch as 'GET /sindri/launches/{launchId}' serves it (UI spec E-1, launcher L-10): the app shows what became of a click.</summary>
/// <param name="Id">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Mode">Open, attach or resume.</param>
/// <param name="State">Requested, started or failed.</param>
/// <param name="Target">The agent.</param>
/// <param name="ChosenBy">'click', 'default', or 'engine'.</param>
/// <param name="RequestedAt">When it was asked for.</param>
/// <param name="OutcomeAt">When the outcome was recorded, or null while requested.</param>
/// <param name="Code">The failure's catalogue code, otherwise null.</param>
/// <param name="Detail">The failure's message with its fix, otherwise null.</param>
/// <param name="Stage">Where a failure stopped ('host' or 'backstop'), or null.</param>
/// <param name="SessionId">The agent's session id (known at start, else the one 'session wrap' reported), or null.</param>
/// <param name="TabTitle">The viewer's title, once started.</param>
/// <param name="Viewer">What the viewer did, once started; its detail is the viewer's redacted stderr.</param>
/// <param name="Late">True when a client attached after the start recorded 'unconfirmed'.</param>
/// <param name="Attach">The command that attaches to the session, once started.</param>
/// <param name="CatalogSource">Where the launched entry came from, once started.</param>
/// <param name="ArgvHash">The hash of the entry's argv template, once started.</param>
/// <param name="Ended">How the session ended, or null while it runs.</param>
/// <param name="AsOf">How fresh the answer is.</param>
public sealed record LaunchView(LaunchId Id, ItemId Item, LaunchMode Mode, LaunchState State, AgentId Target, string ChosenBy, DateTimeOffset RequestedAt,
    DateTimeOffset? OutcomeAt, string? Code, string? Detail, LaunchStage? Stage, SessionId? SessionId, string? TabTitle, LaunchViewer? Viewer, bool Late,
    IReadOnlyList<string>? Attach, CatalogSource? CatalogSource, string? ArgvHash, LaunchEnd? Ended, AsOf AsOf);
