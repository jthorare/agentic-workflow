using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class OnyxConfigTests
{
    private const string Secret = "test-secret-value-not-real";

    private const string Template =
        "# a comment\nIMAGE_TAG=latest\nPOSTGRES_USER=postgres\n# DISABLE_TELEMETRY=\nUSER_AUTH_SECRET=\"\"\n# AUTH_TYPE=\n"
        + "INDEXING_MODEL_SERVER_HOST=indexing_model_server\nCOMPOSE_PROFILES=s3-filestore";

    private static readonly EnvSettings _settings = new("v4.9.0", 3000, Secret);

    private static readonly string[] _keys = ["IMAGE_TAG", "DISABLE_TELEMETRY", "AUTH_TYPE", "USER_AUTH_SECRET", "INDEXING_MODEL_SERVER_HOST", "HOST_PORT"];

    private static readonly OnyxPins _pins = new EmbeddedOnyxPins().Pins;

    private static readonly ComposeService[] _services =
    [
        new("api_server", "${ONYX_BACKEND_IMAGE:-onyxdotapp/onyx-backend:${IMAGE_TAG:-latest}}", Profiles: []),
        new("background", "${ONYX_BACKEND_IMAGE:-onyxdotapp/onyx-backend:${IMAGE_TAG:-latest}}", Profiles: []),
        new("indexing_model_server", "${ONYX_MODEL_SERVER_IMAGE:-onyxdotapp/onyx-model-server:${IMAGE_TAG:-latest}}", Profiles: []),
        new("nginx", "${BASE_IMAGE_REGISTRY:-docker.io}/library/nginx:1.25.5-alpine", Profiles: []),
        new("opensearch", "${BASE_IMAGE_REGISTRY:-docker.io}/opensearchproject/opensearch:3.6.0", Profiles: []),
    ];

    private static int Count(string text, string key)
        => text.Split('\n').Count(l => l.StartsWith(key + "=", StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void RenderEnv_sets_every_key_once_and_keeps_the_rest()
    {
        var env = OnyxConfig.RenderEnv(Template, _settings);

        Assert.All(_keys, key => Assert.Equal(1, Count(env, key)));
        Assert.Contains("IMAGE_TAG=v4.9.0\n", env, StringComparison.Ordinal);
        Assert.Contains("DISABLE_TELEMETRY=true\n", env, StringComparison.Ordinal);
        Assert.Contains("AUTH_TYPE=basic\n", env, StringComparison.Ordinal);
        Assert.Contains("USER_AUTH_SECRET=" + Secret + "\n", env, StringComparison.Ordinal);
        Assert.Contains("INDEXING_MODEL_SERVER_HOST=inference_model_server\n", env, StringComparison.Ordinal);
        Assert.Contains("HOST_PORT=3000\n", env, StringComparison.Ordinal);
        Assert.Contains("# a comment\n", env, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_USER=postgres\n", env, StringComparison.Ordinal);
        Assert.Contains("COMPOSE_PROFILES=s3-filestore\n", env, StringComparison.Ordinal);
        Assert.EndsWith("\n", env, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', env);
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderEnv_puts_a_commented_key_where_it_was_and_appends_a_missing_one()
    {
        var lines = OnyxConfig.RenderEnv(Template, _settings).Split('\n');

        Assert.Equal("DISABLE_TELEMETRY=true", lines[3]);
        Assert.Equal("AUTH_TYPE=basic", lines[5]);
        Assert.Equal("HOST_PORT=3000", lines[^2]);
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderEnv_is_a_no_op_on_its_own_output()
    {
        var once = OnyxConfig.RenderEnv(Template, _settings);

        Assert.Equal(once, OnyxConfig.RenderEnv(once, _settings));
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderEnv_drops_a_duplicate_active_line_and_reads_crlf_as_lf()
    {
        var env = OnyxConfig.RenderEnv("IMAGE_TAG=a\r\nIMAGE_TAG=b\r\nX=1\r\n", _settings);

        Assert.Equal(1, Count(env, "IMAGE_TAG"));
        Assert.DoesNotContain('\r', env);
        Assert.Contains("X=1\n", env, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void ReadSecret_returns_the_existing_secret()
    {
        Assert.Equal(Secret, OnyxConfig.ReadSecret("A=1\nUSER_AUTH_SECRET=" + Secret + "\n"));
        Assert.Equal(Secret, OnyxConfig.ReadSecret("USER_AUTH_SECRET=\"" + Secret + "\"\n"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A=1\n")]
    [InlineData("USER_AUTH_SECRET=\n")]
    [InlineData("USER_AUTH_SECRET=\"\"\n")]
    [InlineData("# USER_AUTH_SECRET=abc\n")]
    public void ReadSecret_is_null_when_missing_or_empty(string? text)
        => Assert.Null(OnyxConfig.ReadSecret(text));

    [Fact, Trait("Lane", "unit")]
    public void RenderOverride_binds_nginx_to_loopback_and_pins_every_image()
    {
        var text = OnyxConfig.RenderOverride(_services, _settings, _pins, "linux/arm64").Value;

        Golden.Match(text, typeof(OnyxConfigTests).Assembly, "OnyxUp/Golden/override.arm64.yml", Normalization.None);
        Assert.Contains("ports: !override\n      - \"127.0.0.1:3000:80\"", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split('\n').Count(l => l.Contains("onyx-backend@sha256:7c3877", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderOverride_for_amd64_differs_from_arm64_only_in_digests()
    {
        var arm = OnyxConfig.RenderOverride(_services, _settings, _pins, "linux/arm64").Value;
        var amd = OnyxConfig.RenderOverride(_services, _settings, _pins, "linux/amd64").Value;

        var swapped = _pins.Images.Where(i => i.Platform == "linux/arm64")
            .Aggregate(arm, (text, a) => text.Replace(a.Digest, _pins.Images.Single(i => i.Repository == a.Repository && i.Platform == "linux/amd64").Digest, StringComparison.Ordinal));
        Assert.Equal(swapped, amd);
        Assert.NotEqual(arm, amd, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderOverride_is_byte_identical_on_a_second_call_whatever_the_service_order()
    {
        var first = OnyxConfig.RenderOverride(_services, _settings, _pins, "linux/arm64").Value;
        var second = OnyxConfig.RenderOverride([.. _services.Reverse()], _settings, _pins, "linux/arm64").Value;

        Assert.Equal(first, second);
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderOverride_uses_the_port_it_is_given()
        => Assert.Contains("\"127.0.0.1:3004:80\"", OnyxConfig.RenderOverride(_services, _settings with { Port = 3004 }, _pins, "linux/arm64").Value, StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void RenderOverride_refuses_an_unpinned_image()
    {
        var services = _services.Append(new ComposeService("db", "mysql:8", Profiles: [])).ToArray();

        var result = OnyxConfig.RenderOverride(services, _settings, _pins, "linux/arm64");

        Assert.Equal("SND-ONYX-004", result.Error.Code);
        Assert.Contains("db", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("docker.io/library/mysql:8", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderOverride_refuses_a_service_with_no_image()
    {
        var services = _services.Append(new ComposeService("db", Image: null, Profiles: [])).ToArray();

        Assert.Equal("SND-ONYX-004", OnyxConfig.RenderOverride(services, _settings, _pins, "linux/arm64").Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void RenderOverride_refuses_a_platform_the_lock_does_not_pin()
        => Assert.Equal("SND-ONYX-004", OnyxConfig.RenderOverride(_services, _settings, _pins, "linux/riscv64").Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void RenderOverride_refuses_a_compose_without_a_sized_service()
    {
        var result = OnyxConfig.RenderOverride([.. _services.Where(s => s.Name != "opensearch")], _settings, _pins, "linux/arm64");

        Assert.Equal("SND-ONYX-004", result.Error.Code);
        Assert.Contains("the compose layout changed", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("opensearch", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void ActiveImages_lists_the_pinned_ref_of_every_service_that_is_not_profiled()
    {
        var services = _services.Append(new ComposeService("minio", "${BASE_IMAGE_REGISTRY:-docker.io}/onyxdotapp/minio:RELEASE.2025-07-23T15-54-02Z-cpuv1@sha256:7330", Profiles: ["s3-filestore"])).ToArray();

        var images = OnyxConfig.ActiveImages(services, _settings, _pins, "linux/arm64", []).Value;

        Assert.Equal(
            [
                "docker.io/library/nginx@" + Digest("docker.io/library/nginx"),
                "docker.io/onyxdotapp/onyx-backend@" + Digest("docker.io/onyxdotapp/onyx-backend"),
                "docker.io/opensearchproject/opensearch@" + Digest("docker.io/opensearchproject/opensearch"),
            ],
            images);
    }

    [Fact, Trait("Lane", "unit")]
    public void ActiveImages_adds_a_profiled_service_whose_profile_the_env_enables()
    {
        var minio = new ComposeService("minio", "${BASE_IMAGE_REGISTRY:-docker.io}/onyxdotapp/minio:RELEASE.2025-07-23T15-54-02Z-cpuv1@sha256:7330", Profiles: ["s3-filestore"]);
        var other = new ComposeService("other", "${BASE_IMAGE_REGISTRY:-docker.io}/library/redis:7.4-alpine", Profiles: ["x"]);
        var services = _services.Append(minio).ToArray();

        var enabled = OnyxConfig.ActiveImages(services, _settings, _pins, "linux/arm64", ["s3-filestore"]).Value;
        var all = OnyxConfig.ActiveImages(services, _settings, _pins, "linux/arm64", ["*"]).Value;
        var none = OnyxConfig.ActiveImages([.. services, other], _settings, _pins, "linux/arm64", ["x-other"]);

        Assert.Contains(enabled, static i => i.Contains("minio", StringComparison.Ordinal));
        Assert.Contains(all, static i => i.Contains("minio", StringComparison.Ordinal));
        Assert.True(none.IsError || none.Value.All(static i => !i.Contains("minio", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void EnabledProfiles_reads_the_comma_separated_COMPOSE_PROFILES_of_the_env()
    {
        Assert.Equal(["a", "b"], OnyxConfig.EnabledProfiles("X=1\nCOMPOSE_PROFILES=a, b\n"));
        Assert.Equal(["s3-filestore"], OnyxConfig.EnabledProfiles("COMPOSE_PROFILES=\"s3-filestore\"\n"));
        Assert.Empty(OnyxConfig.EnabledProfiles("# COMPOSE_PROFILES=a\n"));
        Assert.Empty(OnyxConfig.EnabledProfiles(null));
    }

    [Fact, Trait("Lane", "unit")]
    public void OnyxPaths_lays_the_files_out_under_the_sindri_home()
    {
        var paths = OnyxPaths.For("/h");

        Assert.Equal("/h/onyx", paths.Root);
        Assert.Equal("/h/onyx/state.json", paths.State);
        Assert.Equal("/h/onyx/deploy/deployment", paths.DeployDir);
        Assert.Equal("/h/onyx/deploy/deployment/docker_compose/docker-compose.yml", paths.Compose);
        Assert.Equal("/h/onyx/deploy/deployment/docker_compose/.env", paths.Env);
        Assert.Equal("/h/onyx/deploy/deployment/docker_compose/docker-compose.override.yml", paths.Override);
    }

    [Fact, Trait("Lane", "unit")]
    public void Stamp_is_a_stable_sha256_of_the_parts_and_changes_with_each_one()
    {
        var stamp = OnyxSteps.Stamp("a", "b");

        Assert.Equal(64, stamp.Length);
        Assert.Equal(stamp, OnyxSteps.Stamp("a", "b"));
        Assert.NotEqual(stamp, OnyxSteps.Stamp("a", "c"), StringComparer.Ordinal);
        Assert.NotEqual(stamp, OnyxSteps.Stamp("ab"), StringComparer.Ordinal);
        Assert.Equal(["fetch", "config", "pull", "start"], [OnyxSteps.Fetch, OnyxSteps.Config, OnyxSteps.Pull, OnyxSteps.Start]);
    }

    private static string Digest(string repository)
        => _pins.Images.Single(i => i.Repository == repository && i.Platform == "linux/arm64").Digest;
}
