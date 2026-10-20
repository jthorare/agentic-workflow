namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>What GET /sindri/jobs is asked for; every part optional. The Jobs page filters in the app over one unfiltered read, so it sends none.</summary>
/// <param name="State">Only jobs in this state.</param>
/// <param name="Kind">Only jobs of this kind.</param>
internal sealed record JobQuery(string? State = null, string? Kind = null);
