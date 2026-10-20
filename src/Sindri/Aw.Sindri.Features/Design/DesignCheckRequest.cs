using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>Run the design packet's checks over the files a caller holds (line spec §4.3; 'sindri design check').</summary>
/// <param name="Facts">Text by name: the design packet's files ('spec.md', 'sections.json' …), the scope packet's under 'scope/', a style or lens pack's 'pack.json' under 'pack/style.json' and 'pack/lenses.json', the repo files a contract edit quotes under 'repo/', and optionally 'options.json'; a missing name makes the rules that need it not-run.</param>
/// <param name="Stage">The stage to run.</param>
public sealed record DesignCheckRequest(IReadOnlyDictionary<string, string> Facts, LineStage Stage);
