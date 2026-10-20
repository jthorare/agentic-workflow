namespace Aw.Sindri.Tests.Workflow.InvalidFixtures;

/// <summary>What an invalid fixture is validated against, so its only fault is the one its rule names.</summary>
internal enum FixtureSetup
{
    /// <summary>recipe validate's service with the build's catalog.</summary>
    Shipped,

    /// <summary>The service with the wiring test catalog (the §12 blocks plus a judge-question gate, an evidence provider and a Step input).</summary>
    Wiring,

    /// <summary>The service with the capability test catalog (effect.repo.push, gate.review, gate.check, evidence.tests), as a hand-written recipe.</summary>
    Capability,

    /// <summary>The capability catalog with Workflow/Recipes/base/workflow.cap-base.yaml as the base: a generated recipe.</summary>
    CapabilityWithBase,

    /// <summary>The wiring catalog with a profile whose workflow.effects allows only effect.claim.ref.</summary>
    EffectAllowlist,

    /// <summary>The build's catalog with InvalidFixtureRun.Registry: workflow.version-taken 1 and workflow.version-behind 1 and 3 registered with other hashes.</summary>
    Registry,

    /// <summary>The capability catalog and base, with InvalidFixtureRun.Registry, where the base is registered but not approved.</summary>
    CapabilityWithUnapprovedBase,

    /// <summary>The wiring catalog with a profile whose workflow.effects allows only effect.test.merge, which acts with a human's authority.</summary>
    HumanTrigger,
}
