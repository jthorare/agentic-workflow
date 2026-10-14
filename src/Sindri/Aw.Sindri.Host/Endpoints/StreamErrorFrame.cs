namespace Aw.Sindri.Host.Endpoints;

/// <summary>The data of an 'event: error' frame the stream sends before it ends on a read error (API spec §5.4).</summary>
/// <param name="Code">The error code.</param>
/// <param name="Detail">What went wrong.</param>
internal sealed record StreamErrorFrame(string Code, string Detail);
