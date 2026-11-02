namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>What the two launch-refusal triggers carry (sandbox spec §6): who saw it, the launcher's code, reason, source and message. Each trigger declares its own refused run.</summary>
public interface ILaunchRefusal
{
    /// <summary>The service that saw the refusal.</summary>
    public string Actor { get; }

    /// <summary>The launch code ('SND-LAUNCH-0…').</summary>
    public string Code { get; }

    /// <summary>The refusal's reason, or null.</summary>
    public string? Reason { get; }

    /// <summary>The credential source for -017, otherwise null.</summary>
    public string? Source { get; }

    /// <summary>The launcher's message, scrubbed.</summary>
    public string Detail { get; }
}
