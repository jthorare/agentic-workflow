using Aw.Kernel.Testing;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Skills;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Tests.Sandbox.Egress;

namespace Aw.Sindri.Tests.Skills;

/// <summary>The manifest loader and the descriptors it makes (skill flows spec D1, §3.2): two fixture manifests, review and officeHours (two blocks).</summary>
[Lane(TestLane.Unit)]
public sealed class SkillManifestLoaderTests
{
    private static LoadedSkillBlock Block(string id) => SkillManifestLoader.Load(SkillFixtures.Folder).Loaded.Single(b => string.Equals(b.Manifest.Name, id, StringComparison.Ordinal));

    [Fact]
    public void The_fixture_folder_loads_three_blocks_sorted_by_id_and_skips_none()
    {
        var blocks = SkillManifestLoader.Load(SkillFixtures.Folder);

        Assert.Equal(["office-hours", "office-hours-revise", "review"], blocks.Loaded.Select(static b => b.Manifest.Name), StringComparer.Ordinal);
        Assert.Empty(blocks.Skipped);
    }

    [Fact]
    public void A_block_is_the_step_skill_descriptor_of_its_id_with_the_agent_runtime()
    {
        var descriptor = Block("review").Descriptor;

        Assert.Equal("step.skill.review", descriptor.Id.Value);
        Assert.Equal(BlockKind.Step, descriptor.Kind);
        Assert.Equal(BlockRuntime.Agent, descriptor.Runtime);
        Assert.Equal("Reviews a pull request with domain reviewers and says pass or needs changes.", descriptor.Sentence);
    }

    [Fact]
    public void A_block_remembers_its_skill_folder_and_the_file_it_came_from()
    {
        var block = Block("office-hours-revise");

        Assert.Equal("officeHours", block.Skill);
        Assert.Equal(Path.Combine(SkillFixtures.Folder, "officeHours", "block.yaml"), block.Path);
    }

    [Fact]
    public void Each_input_is_a_text_parameter_required_as_declared()
    {
        var revise = Block("office-hours-revise").Descriptor.Parameters;
        var review = Block("review").Descriptor.Parameters;

        Assert.Equal([new ParameterSpec("plan", ParameterType.Text, Required: true, IsCapabilityField: false), new ParameterSpec("review", ParameterType.Text, Required: true, IsCapabilityField: false)], revise);
        Assert.Equal([new ParameterSpec("pr", ParameterType.Text, Required: true, IsCapabilityField: false)], review);
    }

    [Fact]
    public void Outputs_are_fact_keys_of_the_camel_cased_id_required_on_the_success_outcome()
    {
        var review = Block("review").Descriptor.Outputs.Single();
        var officeHours = Block("office-hours").Descriptor.Outputs.Single();

        Assert.Equal("fact:reviewFindings", review.Fact.Value);
        Assert.Equal(["pass"], review.RequiredOn.Select(static o => o.Value), StringComparer.Ordinal);
        Assert.Equal("fact:officeHoursFeatureDir", officeHours.Fact.Value);
        Assert.Equal(["done"], officeHours.RequiredOn.Select(static o => o.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_multi_word_id_camel_cases_into_the_fact_key()
    {
        var key = SkillDescriptors.FactKeyOf("bug-fix-orchestrator", "pr");

        Assert.True(key.IsSuccess);
        Assert.Equal("fact:bugFixOrchestratorPr", key.Value);
    }

    [Fact]
    public void A_fact_key_over_64_characters_is_refused()
    {
        var key = SkillDescriptors.FactKeyOf("a-block-id-that-is-written-out-in-full-and-keeps-going-on", "anOutputNameThatIsLong");

        Assert.True(key.IsError);
    }

    [Fact]
    public void Outcomes_are_the_mapped_names_or_just_done()
    {
        Assert.Equal(["needsChanges", "pass"], Block("review").Descriptor.Outcomes.Select(static o => o.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["done"], Block("office-hours").Descriptor.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void Changing_the_args_changes_the_version()
    {
        var before = SkillDescriptors.Build(SkillFixtures.Review);
        var after = SkillDescriptors.Build(SkillFixtures.Review with { Args = [new SkillArg("{pr} --quick")] });

        Assert.True(before.IsSuccess && after.IsSuccess);
        Assert.NotEqual(before.Value.Version, after.Value.Version);
    }

    [Fact]
    public void Changing_the_outcome_map_changes_the_version_but_the_sentence_does_not()
    {
        var before = SkillDescriptors.Build(SkillFixtures.Review).Value;
        var mapped = SkillDescriptors.Build(SkillFixtures.Review with { Outcomes = SkillFixtures.Review.Outcomes with { Map = new Dictionary<string, string>(StringComparer.Ordinal) { ["PASS"] = "pass" } } }).Value;
        var reworded = SkillDescriptors.Build(SkillFixtures.Review with { Sentence = "Reads a PR." }).Value;

        Assert.NotEqual(before.Version, mapped.Version);
        Assert.Equal(before.Version, reworded.Version);   // the sentence is not hashed, like every sentence today
    }

    [Fact]
    public void A_bad_manifest_is_skipped_with_a_reason_and_the_good_ones_still_load()
    {
        using var folder = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(folder.Path, "broken"));
        File.WriteAllText(Path.Combine(folder.Path, "broken", "block.yaml"), "schema: 1\nblocks:\n  - id: broken\n    inputs: {}\n    args: [\"{missing}\"]\n");
        Directory.CreateDirectory(Path.Combine(folder.Path, "review"));
        File.Copy(Path.Combine(SkillFixtures.Folder, "review", "block.yaml"), Path.Combine(folder.Path, "review", "block.yaml"));

        var blocks = SkillManifestLoader.Load(folder.Path);

        Assert.Equal(["review"], blocks.Loaded.Select(static b => b.Manifest.Name), StringComparer.Ordinal);
        var skipped = Assert.Single(blocks.Skipped);
        Assert.Equal("broken", skipped.Name);
        Assert.Contains("missing", skipped.Reason, StringComparison.Ordinal);   // names the slot that isn't a declared input
    }

    [Fact]
    public void A_missing_or_empty_folder_is_an_empty_list()
    {
        using var empty = new TempDirectory();

        Assert.Empty(SkillManifestLoader.Load(empty.Path).Loaded);
        Assert.Empty(SkillManifestLoader.Load(Path.Combine(empty.Path, "no-such-folder")).Loaded);
    }

    [Fact]
    public void A_derived_fact_key_over_64_characters_skips_the_block_with_that_reason()
    {
        using var folder = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(folder.Path, "long"));
        File.WriteAllText(Path.Combine(folder.Path, "long", "block.yaml"),
            "schema: 1\nblocks:\n  - id: a-block-id-that-is-written-out-in-full-and-keeps-going-on\n    sentence: Long.\n    inputs: { x: { type: text, label: X, required: true } }\n    args: [\"{x}\"]\n    outputs: { anOutputNameThatIsLong: { type: text, from: y } }\n    outcomes: { success: done }\n    pushes: false\n    maxDuration: 1h\n");

        var blocks = SkillManifestLoader.Load(folder.Path);

        Assert.Empty(blocks.Loaded);
        Assert.Contains("64", Assert.Single(blocks.Skipped).Reason, StringComparison.Ordinal);
    }
}
