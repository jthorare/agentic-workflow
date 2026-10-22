using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Features.GetProfile;

/// <summary>One pack the profile resolves (line spec LN-API8).</summary>
/// <param name="Kind">'lenses.design' or 'lenses.review': the profile key the pack is chosen under.</param>
/// <param name="Id">The pack id; null when the profile names something that is not a pack id.</param>
/// <param name="Source">Where it was found: 'built-in' (repo and machine packs are a later unit).</param>
/// <param name="Hash">The hash of the pack's files, or null when it does not resolve.</param>
/// <param name="Pin">'built-in' for a pack the build pins; 'none' when it does not resolve.</param>
/// <param name="Status">'ok', or 'invalid: ' and why the pack does not resolve (SND-PACK-001).</param>
public sealed record ProfilePack(string Kind, LensPackId? Id, string Source, string? Hash, string Pin, string Status);
