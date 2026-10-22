namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>How far a counted step is.</summary>
/// <param name="Done">Units finished.</param>
/// <param name="Total">Units in all.</param>
/// <param name="Unit">What is counted, plural; absent from an older server.</param>
internal sealed record SetupProgressView(int Done, int Total, string? Unit = null);
