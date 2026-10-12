using Aw.Kernel.Ids;

namespace Aw.Kernel.Testing.Tests;

/// <summary>The id generator contract fails a generator that repeats or issues the empty id.</summary>
[Lane(TestLane.Unit)]
public sealed class IdGeneratorContractCatchesBrokenFakesTests
{
    [Fact]
    public void A_generator_that_issues_the_empty_id_breaks_the_contract() =>
        Assert.Throws<ContractViolationException>(new Empty().Ids_are_never_empty);

    [Fact]
    public void A_generator_that_repeats_an_id_breaks_the_contract() =>
        Assert.Throws<ContractViolationException>(new Repeats().Ids_from_one_instance_are_distinct);

    [Fact]
    public void A_real_generator_whose_fresh_instances_start_the_same_sequence_breaks_the_contract() =>
        Assert.Throws<ContractViolationException>(new SameSequence().A_fresh_instance_does_not_repeat_another_instances_ids);

    [Fact]
    public void A_deterministic_generator_whose_fresh_instances_differ_breaks_the_contract() =>
        Assert.Throws<ContractViolationException>(new DeterministicButScoped().A_fresh_instance_does_not_repeat_another_instances_ids);

    private sealed class Empty : IdGeneratorContract
    {
        protected override IIdGenerator Create() => new Fixed(Guid.Empty);
    }

    private sealed class Repeats : IdGeneratorContract
    {
        protected override IIdGenerator Create() => new Fixed(SequentialIdGenerator.IdAt(1));
    }

    // A "real" adapter that is really the deterministic fake: fresh instances repeat each other.
    private sealed class SameSequence : IdGeneratorContract
    {
        protected override IIdGenerator Create() => new SequentialIdGenerator();
    }

    // Claims determinism, but each instance gets its own scope.
    private sealed class DeterministicButScoped : IdGeneratorContract
    {
        private uint _scope;

        protected override bool Deterministic => true;

        protected override IIdGenerator Create() => new SequentialIdGenerator(++_scope);
    }

    private sealed class Fixed(Guid id) : IIdGenerator
    {
        public Guid NewId() => id;
    }
}
