using System.Globalization;
using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Delegations;

/// <summary>Work the meta handed to a lane: a GitHub issue and the ACTION message that tells the lane it owns it.</summary>
/// <param name="Issue">The issue number.</param>
/// <param name="Url">The issue's web address.</param>
/// <param name="Title">The issue title, one line.</param>
/// <param name="Lane">The lane, as named in the team config.</param>
/// <param name="Session">The lane's session name, "team-lane": the send that settles the delegation goes to it.</param>
/// <param name="At">When the meta delegated it.</param>
public sealed record Delegation(int Issue, string Url, string Title, string Lane, string Session, DateTimeOffset At)
{
    /// <summary>Whether aw-meta delivered the message itself (lane-send to a non-Claude lane); false while a SendMessage is owed.</summary>
    public bool Delivered { get; init; }

    /// <summary>Whether the lane runs Claude Code, so the meta sends with SendMessage; false for a lane reached through lane-send.</summary>
    public bool Claude { get; init; } = true;

    /// <summary>The id of aw-meta's own lane-send of the message to a lane that can't act on it (launch-only, or its hooks aren't running); that send does not settle the delegation. Null for none.</summary>
    public MessageId? Attempt { get; init; }

    /// <summary>The issue as text names it: "#12".</summary>
    public string Marker => "#" + Issue.ToString(CultureInfo.InvariantCulture);

    /// <summary>The ACTION message the lane gets.</summary>
    public string Message => DelegationText.Message(Issue, Title, Url);
}
