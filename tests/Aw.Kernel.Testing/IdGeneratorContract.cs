using System.Globalization;
using Aw.Kernel.Ids;
using Xunit;

namespace Aw.Kernel.Testing;

/// <summary>
/// The contract every <see cref="IIdGenerator"/> honours, run against the fake (<see cref="SequentialIdGenerator"/>) and
/// each real adapter alike (spec §4b.5, csharp.md Seams). A derived test class binds <see cref="Create"/> to one
/// implementation and carries its lane. Ids are never empty and never repeat across many calls of one instance. A real
/// adapter's fresh instance doesn't repeat another's sequence either; the fake is deterministic by design (two
/// generators in one scope issue the same ids, so a test can predict them), so its binding sets
/// <see cref="Deterministic"/> and the contract then checks that instead.
/// </summary>
public abstract class IdGeneratorContract
{
    /// <summary>How many ids each test draws from one instance.</summary>
    public const int Calls = 1_000;

    /// <summary>
    /// True only for a deterministic fake: two fresh instances issue the same sequence. A real adapter keeps the
    /// default, false: two fresh instances share no id.
    /// </summary>
    protected virtual bool Deterministic => false;

    /// <summary>A fresh instance of the implementation.</summary>
    /// <returns>The generator.</returns>
    protected abstract IIdGenerator Create();

    /// <summary>No id is <see cref="Guid.Empty"/>.</summary>
    [Fact]
    public void Ids_are_never_empty()
    {
        var ids = Draw(Create());

        if (ids.Contains(Guid.Empty))
        {
            throw new ContractViolationException($"{nameof(Ids_are_never_empty)}: an id was Guid.Empty");
        }
    }

    /// <summary>One instance never issues the same id twice.</summary>
    [Fact]
    public void Ids_from_one_instance_are_distinct()
    {
        var ids = Draw(Create());

        var distinct = ids.Distinct().Count();
        if (distinct != Calls)
        {
            throw new ContractViolationException($"{nameof(Ids_from_one_instance_are_distinct)}: {(Calls - distinct).ToString(CultureInfo.InvariantCulture)} of {Calls.ToString(CultureInfo.InvariantCulture)} ids repeated");
        }
    }

    /// <summary>
    /// A fresh instance doesn't repeat another's sequence (a real adapter); a deterministic fake repeats it exactly.
    /// </summary>
    [Fact]
    public void A_fresh_instance_does_not_repeat_another_instances_ids()
    {
        var first = Draw(Create());
        var second = Draw(Create());

        if (Deterministic)
        {
            if (!first.SequenceEqual(second))
            {
                throw new ContractViolationException($"{nameof(A_fresh_instance_does_not_repeat_another_instances_ids)}: a deterministic generator's fresh instances issued different sequences");
            }

            return;
        }

        var shared = first.Intersect(second).Count();
        if (shared != 0)
        {
            throw new ContractViolationException($"{nameof(A_fresh_instance_does_not_repeat_another_instances_ids)}: two fresh instances shared {shared.ToString(CultureInfo.InvariantCulture)} ids");
        }
    }

    private static Guid[] Draw(IIdGenerator generator) => [.. Enumerable.Range(0, Calls).Select(_ => generator.NewId())];
}
