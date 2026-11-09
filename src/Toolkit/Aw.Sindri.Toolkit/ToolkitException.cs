namespace Aw.Sindri.Toolkit;

/// <summary>A failure a toolkit author or the SDK raises on purpose. Its message is safe to show a client; any other exception is not.</summary>
public sealed class ToolkitException : Exception
{
    /// <summary>Makes an exception with the generic tool-failed code.</summary>
    public ToolkitException()
        : this(ToolkitErrors.ToolFailed, ToolkitErrors.ToolFailed.Title)
    {
    }

    /// <summary>Makes an exception with the generic tool-failed code.</summary>
    /// <param name="message">What happened, without secrets.</param>
    public ToolkitException(string message)
        : this(ToolkitErrors.ToolFailed, message)
    {
    }

    /// <summary>Makes an exception with the generic tool-failed code.</summary>
    /// <param name="message">What happened, without secrets.</param>
    /// <param name="innerException">The cause; never shown to a client.</param>
    public ToolkitException(string message, Exception innerException)
        : base(message, innerException)
    {
        Definition = ToolkitErrors.ToolFailed;
    }

    /// <summary>Makes an exception for an SDK error code.</summary>
    /// <param name="definition">The code.</param>
    /// <param name="message">What happened and the fix, without secrets.</param>
    public ToolkitException(ToolkitError definition, string message)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
    }

    /// <summary>The error code this exception carries.</summary>
    public ToolkitError Definition { get; }
}
