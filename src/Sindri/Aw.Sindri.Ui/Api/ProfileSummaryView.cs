namespace Aw.Sindri.Ui.Api;

/// <summary>The approval's summary in plain words (setup-once spec §5.2). With nothing approved yet, the groups list only what the profile itself sets; every group is empty when it has nothing.</summary>
/// <param name="Repos">The repos the approved profile does not name yet.</param>
/// <param name="Services">One sentence per service newly enabled or run more often.</param>
/// <param name="Allowance">One sentence per effect newly allowed or limit added or raised.</param>
/// <param name="Connections">One sentence per outside system added or changed.</param>
/// <param name="HumanOnly">What Sindri keeps for the person.</param>
internal sealed record ProfileSummaryView(
    IReadOnlyList<ProfileSummaryRepo> Repos,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Allowance,
    IReadOnlyList<string> Connections,
    IReadOnlyList<string> HumanOnly);
