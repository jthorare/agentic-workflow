using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="ICallerContextAccessor"/> that always answers one caller, for a service test that does not run the command chain.</summary>
/// <param name="current">The caller, or null for none.</param>
public sealed class FixedCallerContextAccessor(CallerContext? current) : ICallerContextAccessor
{
    /// <inheritdoc/>
    public CallerContext? Current { get; } = current;

    /// <inheritdoc/>
    public IDisposable Enter(CallerContext context) => throw new NotSupportedException();

    /// <inheritdoc/>
    public IDisposable EnterService(string service) => throw new NotSupportedException();
}
