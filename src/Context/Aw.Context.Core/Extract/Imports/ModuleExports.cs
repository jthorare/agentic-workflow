namespace Aw.Context.Core.Extract.Imports;

/// <summary>What a TS or JS file exports.</summary>
/// <param name="Declared">Names it declares and exports ('export const x', 'export function f', 'export class C').</param>
/// <param name="Aliases">Names exported from a local name ('export { a as b }'): exported name to the local one.</param>
/// <param name="ReExports">Names re-exported from another module ('export { a as b } from 'x''): exported name to the module and the name it has there.</param>
/// <param name="Stars">The modules re-exported whole ('export * from 'x''), in order.</param>
internal sealed record ModuleExports(IReadOnlySet<string> Declared, IReadOnlyDictionary<string, string> Aliases, IReadOnlyDictionary<string, (string Specifier, string Name)> ReExports, IReadOnlyList<string> Stars)
{
    internal static ModuleExports Empty { get; } = new(new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, (string, string)>(StringComparer.Ordinal), []);
}
