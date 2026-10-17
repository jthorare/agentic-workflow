using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class ComposeImageRefTests
{
    private static readonly Dictionary<string, string> _env = new(StringComparer.Ordinal) { ["IMAGE_TAG"] = "v4.9.0" };

    private static ImageRef Resolved(string raw, IReadOnlyDictionary<string, string>? env = null)
        => ComposeImageRef.Resolve(raw, env ?? _env).Value;

    [Fact, Trait("Lane", "unit")]
    public void Resolve_expands_a_nested_default_and_the_env_tag()
        => Assert.Equal(
            new ImageRef("docker.io/onyxdotapp/onyx-backend", "v4.9.0"),
            Resolved("${ONYX_BACKEND_IMAGE:-onyxdotapp/onyx-backend:${IMAGE_TAG:-latest}}"));

    [Fact, Trait("Lane", "unit")]
    public void Resolve_uses_the_default_when_the_variable_is_unset()
        => Assert.Equal(
            new ImageRef("docker.io/onyxdotapp/onyx-backend", "latest"),
            Resolved("${ONYX_BACKEND_IMAGE:-onyxdotapp/onyx-backend:${IMAGE_TAG:-latest}}", new Dictionary<string, string>(StringComparer.Ordinal)));

    [Fact, Trait("Lane", "unit")]
    public void Resolve_lets_the_env_override_a_whole_image()
        => Assert.Equal(
            new ImageRef("quay.io/x/y", "1"),
            Resolved("${ONYX_BACKEND_IMAGE:-onyxdotapp/onyx-backend:${IMAGE_TAG:-latest}}", new Dictionary<string, string>(StringComparer.Ordinal) { ["ONYX_BACKEND_IMAGE"] = "quay.io/x/y:1" }));

    [Fact, Trait("Lane", "unit")]
    public void Resolve_normalizes_a_registry_variable_and_a_library_image()
        => Assert.Equal(
            new ImageRef("docker.io/library/postgres", "15.2-alpine"),
            Resolved("${BASE_IMAGE_REGISTRY:-docker.io}/library/postgres:15.2-alpine"));

    [Fact, Trait("Lane", "unit")]
    public void Resolve_adds_docker_io_and_library_to_a_bare_name()
        => Assert.Equal(new ImageRef("docker.io/library/redis", "7.4-alpine"), Resolved("redis:7.4-alpine"));

    [Fact, Trait("Lane", "unit")]
    public void Resolve_adds_docker_io_to_a_namespaced_image_with_a_default_tag()
        => Assert.Equal(
            new ImageRef("docker.io/onyxdotapp/code-interpreter", "0.4.8"),
            Resolved("onyxdotapp/code-interpreter:${CODE_INTERPRETER_IMAGE_TAG:-0.4.8}"));

    [Fact, Trait("Lane", "unit")]
    public void Resolve_strips_a_digest_and_keeps_the_tag()
        => Assert.Equal(
            new ImageRef("docker.io/onyxdotapp/minio", "RELEASE.2025-07-23T15-54-02Z-cpuv1"),
            Resolved("${BASE_IMAGE_REGISTRY:-docker.io}/onyxdotapp/minio:RELEASE.2025-07-23T15-54-02Z-cpuv1@sha256:7330be2e7320a7a699b36f72bd06a94d8a21ec9f6f397345d9c38fc6751141f5"));

    [Fact, Trait("Lane", "unit")]
    public void Resolve_keeps_a_registry_port()
        => Assert.Equal(new ImageRef("localhost:5000/a/b", "1"), Resolved("localhost:5000/a/b:1"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("redis")]
    [InlineData("${X:?must be set}")]
    [InlineData("$$HOME/img:1")]
    [InlineData("img:${UNSET}")]
    [InlineData("img:${IMAGE_TAG")]
    [InlineData("img:$IMAGE_TAG")]
    [InlineData("")]
    public void Resolve_refuses_what_it_cannot_resolve_to_a_repository_and_a_tag(string raw)
    {
        var result = ComposeImageRef.Resolve(raw, new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal("SND-ONYX-004", result.Error.Code);
    }
}
