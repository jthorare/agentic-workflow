using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.DeferOnyx;

/// <summary>Leave Onyx for later ("Not now"), or take that back (POST /sindri/onyx/defer, 'sindri onyx defer'; setup-once spec §5.6; desk).</summary>
/// <param name="Deferred">True to set the Onyx step aside as skipped; false to offer it again.</param>
public sealed record DeferOnyxRequest(bool Deferred) : ICommand<DeferOnyxResponse>;
