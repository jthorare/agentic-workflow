using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>What a build does with its structure measures.</summary>
/// <param name="Verdict">The worst verdict; with 'warn' a refusal reads Degraded.</param>
/// <param name="Refusal">The error that stops the build, or null.</param>
/// <param name="Notes">The notes for the build report's Degraded list, in plain words.</param>
/// <param name="Stored">The reason kept with the draft (numbers and limits only), or null when nothing is degraded.</param>
public sealed record MapChecksOutcome(CheckVerdict Verdict, Error? Refusal, IReadOnlyList<string> Notes, string? Stored);
