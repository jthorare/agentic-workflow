namespace Aw.Context.Core.Linear;

/// <summary>Makes the fields of a <see cref="LinearIssueChange"/>.</summary>
public static class LinearFieldChange
{
    /// <summary>Sends this value.</summary>
    /// <typeparam name="T">The field's type.</typeparam>
    /// <param name="value">The new value; null clears a nullable field.</param>
    /// <returns>The field change.</returns>
    public static LinearFieldChange<T> Set<T>(T? value) => new(IsSet: true, value);
}

/// <summary>One field of a <see cref="LinearIssueChange"/>: left alone (the default), or set to a value, which for a nullable field may be null (clear it).</summary>
/// <typeparam name="T">The field's type.</typeparam>
/// <param name="IsSet">True when the change sends this field.</param>
/// <param name="Value">The value sent; meaningless when <paramref name="IsSet"/> is false.</param>
public readonly record struct LinearFieldChange<T>(bool IsSet, T? Value);
