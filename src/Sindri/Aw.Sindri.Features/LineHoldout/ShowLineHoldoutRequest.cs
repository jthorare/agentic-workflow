using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LineHoldout;

/// <summary>
/// The owner reads a phase's hidden scenarios (line spec §9.2, LN-API5), a protected command with a typed confirmation: without an answer it shows the counts and issues the
/// challenge 'line holdout show &lt;line&gt; &lt;phase&gt;'; with one that redeems it, it shows the text and marks every scenario it showed spent.
/// </summary>
/// <param name="Line">The line instance's slug.</param>
/// <param name="Phase">The phase key.</param>
/// <param name="Answer">The challenge and the text the human typed, or null.</param>
internal sealed record ShowLineHoldoutRequest(string Line, string Phase, ChallengeAnswer? Answer = null) : ICommand<ShowLineHoldoutResponse>;
