namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The text of one row that a filter looks at.</summary>
/// <param name="Name">The session's name (its title).</param>
/// <param name="Provider">The provider, as the label and the key together ("Claude Code claude-code").</param>
/// <param name="Repo">The repo; empty for none.</param>
/// <param name="State">The state, as the served word and the chip word together ("asking Waiting on you").</param>
/// <param name="Doing">What it is doing: what it asks, its Step.</param>
/// <param name="Owner">Who owns or launched it: its lane, or the Sindri launch.</param>
internal readonly record struct AgentFilterTarget(string Name, string Provider, string Repo, string State, string Doing, string Owner);
