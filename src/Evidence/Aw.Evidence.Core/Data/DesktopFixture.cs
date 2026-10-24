using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>The seeded-data command: a program and the arguments of each step, run in the repo with the scratch directory in the home variable.</summary>
/// <param name="Command">The program and its leading arguments; a relative program is the repo's.</param>
/// <param name="Up">The steps that build the data, each a list of arguments appended to <paramref name="Command"/>, run in order.</param>
/// <param name="Down">The arguments that tear it down (run last, even after a failure); null runs nothing.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopFixture(IReadOnlyList<string> Command, IReadOnlyList<IReadOnlyList<string>> Up, IReadOnlyList<string>? Down = null);
