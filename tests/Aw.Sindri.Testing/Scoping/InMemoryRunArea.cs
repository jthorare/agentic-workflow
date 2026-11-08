using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The in-memory <see cref="IRunArea"/>: files by instance address and name, under the same name and address rules as the disk run area; it can be made to refuse every write.</summary>
public sealed class InMemoryRunArea : IRunArea
{
    private readonly Dictionary<(string Address, string Name), string> _files = [];

    /// <summary>Whether every write fails, as an unwritable run area does.</summary>
    public bool Unwritable { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Write(string address, string name, string content, bool append, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        if (Check(address, name) is { } refused)
        {
            return ValueTask.FromResult<Result<bool>>(refused);
        }

        if (Unwritable)
        {
            return ValueTask.FromResult<Result<bool>>(WorkflowErrors.BlockRunAreaUnavailable.With($"the run area file {name} of {address} cannot be written"));
        }

        _files[(address, name)] = (append && _files.TryGetValue((address, name), out var before) ? before : string.Empty) + content;
        return ValueTask.FromResult<Result<bool>>(true);
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Read(string address, string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Result<string>>(Check(address, name) is { } refused ? refused : _files.GetValueOrDefault((address, name), string.Empty));
    }

    /// <inheritdoc/>
    public Result<string> PathFor(string address, string name) => Check(address, name) is { } refused ? refused : "/run/" + address + "/" + name;

    /// <summary>The text of the file whose location (<see cref="PathFor"/> of its address and name) is <paramref name="path"/>; null when no file is there.</summary>
    /// <param name="path">The file's location.</param>
    /// <returns>The text, or null.</returns>
    public string? ReadLocation(string path) => _files.FirstOrDefault(f => string.Equals($"/run/{f.Key.Address}/{f.Key.Name}", path, StringComparison.Ordinal)) is { Value: { } text } ? text : null;

    private static Error? Check(string address, string name) =>
        !RunAreaNames.IsValid(name) ? SindriErrors.InvalidInput.With($"'{name}' is not a run area file name")
        : !InstanceAddress.TryFrom(address, out _) ? SindriErrors.InvalidInput.With($"'{address}' is not an instance address")
        : null;
}
