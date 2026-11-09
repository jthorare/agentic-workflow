namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>A pasted secret (a key, an OAuth client secret): held until it is sent once, then gone. Its text is never shown by <see cref="ToString"/>.</summary>
internal sealed class SecretField : ObservableObject
{
    /// <summary>The pasted text, as the masked box edits it. Empty after <see cref="Take"/>.</summary>
    public string Text
    {
        get;
        set
        {
            field = value ?? string.Empty;
            OnChanged(nameof(Text));
            OnChanged(nameof(HasValue));
        }
    } = string.Empty;

    /// <summary>True while something is pasted.</summary>
    public bool HasValue => Text.Length > 0;

    /// <summary>Hands the secret over for the one request that carries it, and forgets it.</summary>
    /// <returns>The secret, or null when nothing was pasted.</returns>
    internal string? Take()
    {
        var taken = Text;
        Text = string.Empty;
        return taken.Length > 0 ? taken : null;
    }

    /// <summary>A fixed text: the secret is not in it.</summary>
    /// <returns>'SecretField'.</returns>
    public override string ToString() => nameof(SecretField);
}
