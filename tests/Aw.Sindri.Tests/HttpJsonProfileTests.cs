using System.Text.Json.Serialization.Metadata;
using Aw.Kernel.Json;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Aw.Sindri.Tests;

public sealed class HttpJsonProfileTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task The_http_json_options_are_the_Kernel_profile()
    {
        await using var app = LoopbackBinding.Build([], _ => null, new FakeTimeProvider());
        var http = app.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        var profile = AwJson.CreateOptions(SindriJsonContext.Default);

        // Exactly ASP.NET's ProblemDetails context, then ours: no reflection resolver, so a missing [JsonSerializable]
        // fails here as it would in the NativeAOT host.
        Assert.Equal(2, http.TypeInfoResolverChain.Count);
        Assert.Same(SindriJsonContext.Default, http.TypeInfoResolverChain[1]);
        Assert.DoesNotContain(http.TypeInfoResolverChain, r => r is DefaultJsonTypeInfoResolver);
        Assert.NotNull(http.TypeInfoResolver?.GetTypeInfo(typeof(ProblemDetails), http));   // error results serialize
        Assert.Equal(profile.PropertyNamingPolicy, http.PropertyNamingPolicy);
        Assert.Equal(profile.PropertyNameCaseInsensitive, http.PropertyNameCaseInsensitive);
        Assert.Equal(profile.NumberHandling, http.NumberHandling);
        Assert.Equal(profile.DefaultIgnoreCondition, http.DefaultIgnoreCondition);
        Assert.Equal(profile.UnmappedMemberHandling, http.UnmappedMemberHandling);
        Assert.Equal(profile.Encoder, http.Encoder);
        Assert.Equal(profile.RespectNullableAnnotations, http.RespectNullableAnnotations);
        Assert.Equal(profile.RespectRequiredConstructorParameters, http.RespectRequiredConstructorParameters);
        Assert.Equal(profile.Converters.Select(c => c.GetType()), http.Converters.Select(c => c.GetType()));
    }
}
