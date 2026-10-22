namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>An event's one value: the card or step key picked, the point of the world centred on, the view asked for.</summary>
/// <typeparam name="T">The value's type.</typeparam>
/// <param name="value">The value.</param>
internal sealed class ValueEventArgs<T>(T value) : EventArgs
{
    /// <summary>Gets the value.</summary>
    internal T Value { get; } = value;
}
