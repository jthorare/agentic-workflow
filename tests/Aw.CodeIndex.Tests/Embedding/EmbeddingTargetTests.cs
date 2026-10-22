using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Tests.Embedding;

[Trait("Lane", "unit")]
public sealed class EmbeddingTargetTests
{
    [Theory]
    [InlineData("http://127.0.0.1:11434", true)]
    [InlineData("http://127.0.0.1", true)]
    [InlineData("http://[::1]:11434", true)]
    [InlineData("http://localhost:11434", false)]
    [InlineData("http://127.0.0.2:11434", false)]
    [InlineData("https://127.0.0.1:11434", false)]
    [InlineData("http://user@127.0.0.1:11434", false)]
    [InlineData("http://10.0.0.5:11434", false)]
    [InlineData("127.0.0.1:11434", false)]
    [InlineData("", false)]
    public void Only_an_http_ip_literal_loopback_address_without_credentials_is_loopback(string url, bool loopback)
        => Assert.Equal(loopback, EmbeddingTarget.IsLoopbackUrl(url));

    [Theory]
    [InlineData("nomic-embed-text:latest", true)]
    [InlineData(null, true)]
    [InlineData("qwen3-embedding:cloud", false)]
    [InlineData("Some-CLOUD-model", false)]
    public void A_model_with_cloud_in_its_name_is_refused(string? model, bool allowed)
    {
        var error = EmbeddingTarget.Check(new EmbeddingsSettings(Enabled: true, CodeIndexSettings.DefaultEmbeddingsUrl, model));

        Assert.Equal(allowed, error is null);
        Assert.True(allowed || error!.Code == CodeIndexErrors.EmbeddingTargetRefused.Code);
    }

    [Fact]
    public void A_bad_url_is_refused_with_the_embedding_target_code()
        => Assert.Equal(CodeIndexErrors.EmbeddingTargetRefused.Code, EmbeddingTarget.Check(new EmbeddingsSettings(Enabled: true, "http://localhost:11434", Model: null))!.Code);
}
