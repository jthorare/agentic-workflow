using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Decisions;

/// <summary>What each kind of decision offers (P4 plan §1.2): its options and the one to offer first.</summary>
public static class DecisionKinds
{
    private static readonly DecisionOptionSpec[] _acceptRequest =
    [
        new(DecisionOption.From("accept"), "Accept", RejectLike: false),
        new(DecisionOption.From("decline"), "Decline", RejectLike: true),
    ];

    /// <summary>The options of <paramref name="kind"/>, in the order they are shown.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The options.</returns>
    public static IReadOnlyList<DecisionOptionSpec> Options(DecisionKind kind) => kind switch
    {
        DecisionKind.AcceptRequest => _acceptRequest,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a decision kind"),
    };

    /// <summary>The option to offer first for <paramref name="kind"/>.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The option.</returns>
    public static DecisionOption Recommended(DecisionKind kind) => Options(kind)[0].Id;
}
