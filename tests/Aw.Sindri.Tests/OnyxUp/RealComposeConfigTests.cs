using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The proof that every image of the real v4.9.0 compose maps to a pin, and that the rendered files hold what Sindri promises.
public sealed class RealComposeConfigTests
{
    private const string AuthValue = "test-value-not-real";

    private static readonly OnyxPins _pins = new EmbeddedOnyxPins().Pins;
    private static readonly EnvSettings _settings = new(_pins.Version, 3000, AuthValue);

    private static readonly string[] _nonProfileImages = ["onyxdotapp/onyx-backend", "onyxdotapp/onyx-web-server", "onyxdotapp/onyx-model-server", "library/postgres", "opensearchproject/opensearch", "library/nginx", "library/redis"];
    private static readonly string[] _envKeys = ["IMAGE_TAG", "DISABLE_TELEMETRY", "AUTH_TYPE", "USER_AUTH_SECRET", "INDEXING_MODEL_SERVER_HOST", "HOST_PORT"];

    private static IReadOnlyList<ComposeService> Services()
        => new YamlComposeServices().Read(OnyxFixtures.ReadText("docker_compose/docker-compose.yml")).Value;

    [Theory, Trait("Lane", "unit")]
    [InlineData("linux/arm64")]
    [InlineData("linux/amd64")]
    public void Every_compose_image_of_the_real_file_resolves_to_a_pin_and_the_override_renders(string platform)
    {
        var rendered = OnyxConfig.RenderOverride(Services(), _settings, _pins, platform);

        Assert.True(rendered.IsSuccess, rendered.IsError ? rendered.Error.Message : "");
        Assert.Contains("ports: !override", rendered.Value, StringComparison.Ordinal);
        Assert.Contains("\"127.0.0.1:3000:80\"", rendered.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0.0.0", rendered.Value, StringComparison.Ordinal);
        foreach (var image in _pins.Images.Where(i => i.Platform == platform))
        {
            Assert.Contains(image.Repository + "@" + image.Digest, rendered.Value, StringComparison.Ordinal);
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("linux/arm64")]
    [InlineData("linux/amd64")]
    public void The_active_images_are_the_seven_non_profile_pins_and_minio_which_the_env_enables(string platform)
    {
        var env = OnyxConfig.RenderEnv(OnyxFixtures.ReadText("docker_compose/env.template"), _settings);
        var active = OnyxConfig.ActiveImages(Services(), _settings, _pins, platform, OnyxConfig.EnabledProfiles(env)).Value;

        var expected = _nonProfileImages.Append("onyxdotapp/minio")
            .Select(name => _pins.Images.Single(i => i.Repository == "docker.io/" + name && i.Platform == platform))
            .Select(static i => i.Repository + "@" + i.Digest)
            .Order(StringComparer.Ordinal);
        Assert.Equal(expected, active, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("linux/arm64")]
    [InlineData("linux/amd64")]
    public void With_no_profile_enabled_the_active_images_are_the_seven_non_profile_pins(string platform)
    {
        var active = OnyxConfig.ActiveImages(Services(), _settings, _pins, platform, []).Value;

        var expected = _nonProfileImages
            .Select(name => _pins.Images.Single(i => i.Repository == "docker.io/" + name && i.Platform == platform))
            .Select(static i => i.Repository + "@" + i.Digest)
            .Order(StringComparer.Ordinal);
        Assert.Equal(expected, active, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_env_template_renders_the_six_keys_exactly_once()
    {
        var env = OnyxConfig.RenderEnv(OnyxFixtures.ReadText("docker_compose/env.template"), _settings);

        foreach (var key in _envKeys)
        {
            Assert.Equal(1, env.Split('\n').Count(l => l.StartsWith(key + "=", StringComparison.Ordinal)));
        }

        Assert.Contains("IMAGE_TAG=v4.9.0\n", env, StringComparison.Ordinal);
        Assert.Contains("INDEXING_MODEL_SERVER_HOST=inference_model_server\n", env, StringComparison.Ordinal);
        Assert.Contains("COMPOSE_PROFILES=s3-filestore\n", env, StringComparison.Ordinal);
        Assert.Equal(AuthValue, OnyxConfig.ReadSecret(env), StringComparer.Ordinal);
    }
}
