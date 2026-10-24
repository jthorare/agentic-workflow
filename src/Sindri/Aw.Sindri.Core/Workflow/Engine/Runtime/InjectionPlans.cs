namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The pure merge of the filled inputs into one plan (plan decision 3): no I/O and no clock, so the workflow can run it.</summary>
public static class InjectionPlans
{
    /// <summary>The most bytes one Step's inputs filled together in a run recorded before 'step-inputs-no-plan-cap-v1'; a replay of such a history still applies it, a new run does not.</summary>
    public const int PlanCap = 1024 * 1024;

    /// <summary>The bytes the inputs filled together: every section and typed result of every input, replaced sections included, because their texts are all held.</summary>
    /// <param name="filled">What the inputs returned.</param>
    /// <returns>The total.</returns>
    public static int Bytes(IReadOnlyList<FilledInput> filled)
    {
        ArgumentNullException.ThrowIfNull(filled);
        return filled.Sum(static f => f.Sections.Sum(static s => s.Bytes) + (f.Result?.Bytes ?? 0));
    }

    /// <summary>Merges the inputs in declaration order and their sections in the order given; a later section of the same name replaces the earlier one in place and warns.</summary>
    /// <param name="filled">What the inputs returned.</param>
    /// <returns>The plan.</returns>
    public static InjectionPlan Merge(IReadOnlyList<FilledInput> filled)
    {
        ArgumentNullException.ThrowIfNull(filled);
        List<InjectionEntry> entries = [];
        List<InjectionWarning> warnings = [];
        foreach (var input in filled.OrderBy(static f => f.Index))
        {
            foreach (var section in input.Sections)
            {
                var at = entries.FindIndex(e => string.Equals(e.Section, section.Section, StringComparison.Ordinal));
                if (at < 0)
                {
                    entries.Add(section);
                    continue;
                }

                warnings.Add(new InjectionWarning(section.Section, entries[at].Input, section.Input));
                entries[at] = section with { Replaced = entries[at].Input };
            }
        }

        return new InjectionPlan(entries, warnings);
    }
}
