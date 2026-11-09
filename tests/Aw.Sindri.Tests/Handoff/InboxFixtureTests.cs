using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Handoff;

namespace Aw.Sindri.Tests.Handoff;

/// <summary>The conformance examples in tests/fixtures/handoff/fake-inbox/ (the files check-handoff-schema.mjs checks against the OpenAPI file) get the same verdicts from HttpInboxClient's reader.</summary>
[Lane(TestLane.Unit)]
public sealed class InboxFixtureTests
{
    [Fact]
    public void A_valid_example_reads_and_an_invalid_one_that_names_a_field_is_SND_CONN_201_naming_it()
    {
        var dir = Path.Combine(RepoRoot(), "tests", "fixtures", "handoff", "fake-inbox");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")));
        var checkedAny = 0;
        foreach (var fx in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            var schema = fx.GetProperty("schema").GetString()!;
            if (schema is not ("Offer" or "OfferPage" or "ClaimResponse"))
            {
                continue;
            }

            var file = fx.GetProperty("file").GetString()!;
            var json = File.ReadAllText(Path.Combine(dir, file));
            var read = Read(schema, json);
            if (fx.GetProperty("valid").GetBoolean())
            {
                Assert.False(read.IsError, file + ": " + (read.IsError ? read.Error.Message : string.Empty));
                checkedAny++;
            }
            else if (fx.TryGetProperty("names", out var names))
            {
                Assert.True(read.IsError, file + " should be refused");
                Assert.Equal(SindriErrors.InboxContractViolation.Code, read.Error.Code);
                Assert.Contains(names.GetString()!, read.Error.Message, StringComparison.Ordinal);
                checkedAny++;
            }
        }

        Assert.True(checkedAny >= 8, "the fixtures were not read");
    }

    private static Aw.Kernel.Result<object> Read(string schema, string json)
    {
        switch (schema)
        {
            case "OfferPage":
                var page = InboxWire.ParsePage(json);
                return page.IsError ? page.Error : page.Value;
            case "ClaimResponse":
                var claim = InboxWire.ParseClaim(json);
                return claim.IsError ? claim.Error : claim.Value;
            default:
                using (var document = JsonDocument.Parse(json))
                {
                    var offer = InboxWire.ReadOffer(document.RootElement, "offer");
                    return offer.IsError ? offer.Error : offer.Value;
                }
        }
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test binaries");
    }
}
