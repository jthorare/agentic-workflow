namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>The Agents table's column widths (agents spec §2): the header and every row use the same string, so the cells stay under their heads.</summary>
internal static class AgentColumnsLayout
{
    /// <summary>Status, Agent, Repository, Where it runs, What it is doing, For.</summary>
    internal const string Wide = "150,2*,1.2*,120,1.4*,56";

    /// <summary>Under 640 px: Status, Agent and For; the hidden columns are at zero width.</summary>
    internal const string Compact = "112,*,0,0,0,56";
}
