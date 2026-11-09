#pragma warning disable CA1822, MA0041, MA0182 // red commit: stubs, replaced by the green commit
namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>Stub (red commit).</summary>
internal sealed class AddConnectionViewModel : ObservableObject
{
    public IReadOnlyList<ConnectionPresetChoice> Presets => throw new NotSupportedException();

    public ConnectionPresetChoice? SelectedPreset { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public SecretField Key { get; } = new();

    public bool ShowClient => throw new NotSupportedException();

    public bool ShowClientSecret => throw new NotSupportedException();

    public bool ShowKey => throw new NotSupportedException();

    public bool ShowUrl => throw new NotSupportedException();

    public bool CanSend => throw new NotSupportedException();

    public string StatusText => throw new NotSupportedException();

    public string ErrorText => throw new NotSupportedException();

    internal void Open() => throw new NotSupportedException();

    internal Task Send(CancellationToken cancellationToken) => throw new NotSupportedException();
}
