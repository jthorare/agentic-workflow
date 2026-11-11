using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Sindri.Ui.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Architecture.Tests;

/// <summary>
/// Every HTTP route the Sindri UI client calls is mapped by the Sindri host (#2798). The client's public methods are found by
/// reflection and called with dummy arguments over a handler that records each request's method and path, so a new client method
/// is covered the day it is added; the recorded routes are matched against the real host's endpoint table.
/// </summary>
[Trait("Lane", "integration")]
public sealed class UiClientRoutesTests : IDisposable
{
    private const string ClientType = "Aw.Sindri.Ui.Api.SindriApiClient";
    private const int MaxDepth = 4;

    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    // why: a client method that can't send a request with dummy arguments goes here with the reason, never silently. Empty today:
    // every method is expected to send at least one request.
    private static readonly Dictionary<string, string> _skipped = new(StringComparer.Ordinal);

    private readonly string _home = Directory.CreateTempSubdirectory("aw-arch-ui-routes-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (IOException)
        {
            // a leftover temp folder is not a failure
        }
    }

    [Fact]
    public async Task Every_route_the_ui_client_calls_is_mapped_by_the_host()
    {
        var token = TestContext.Current.CancellationToken;
        var (calls, silent) = await RecordClientCalls(token);
        await using var app = BuildHost();
        var mapped = Mapped(app);

        var unmatched = calls
            .Where(c => !mapped.Exists(m => m.Matches(c.Method, c.Path)))
            .Select(c => $"{c.Method} {c.Path}  (client method {c.Client}; the dummy id is x)")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(calls);
        Assert.True(
            silent.Count == 0 && unmatched.Count == 0,
            (unmatched.Count == 0 ? string.Empty : "The client calls routes the host doesn't map:" + Environment.NewLine + string.Join(Environment.NewLine, unmatched) + Environment.NewLine)
            + (silent.Count == 0 ? string.Empty : "These client methods sent no request with dummy arguments; fix the dummy or list them in _skipped with a reason: " + string.Join(", ", silent)));
    }

    private static async Task<(List<(string Method, string Path, string Client)> Calls, List<string> Silent)> RecordClientCalls(CancellationToken token)
    {
        var recorder = new Recorder();
        using var http = new HttpClient(recorder);
        var clientType = typeof(IHumanTokenSource).Assembly.GetType(ClientType, throwOnError: true)!;
        var client = Activator.CreateInstance(clientType, http, new FixedToken(), TimeProvider.System, null)!;
        var calls = new List<(string Method, string Path, string Client)>();
        var silent = new List<string>();

        var methods = clientType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && IsCall(m.ReturnType))
            .OrderBy(m => m.Name, StringComparer.Ordinal);
        foreach (var method in methods)
        {
            if (_skipped.ContainsKey(method.Name))
            {
                continue;
            }

            var before = recorder.Seen.Count;
            try
            {
                var args = method.GetParameters().Select(p => DummyFor(p, token)).ToArray();
                await Run(method.Invoke(client, args), token);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // a parse or serialisation error after the request went out is fine; no request at all is reported below
            }

            var sent = recorder.Seen.Skip(before).ToList();
            if (sent.Count == 0)
            {
                silent.Add(method.Name);
            }

            calls.AddRange(sent.Select(s => (s.Method, s.Path, method.Name)));
        }

        return (calls, silent);
    }

    private static bool IsCall(Type returnType) =>
        typeof(Task).IsAssignableFrom(returnType) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

    private static async Task Run(object? result, CancellationToken token)
    {
        if (result is Task task)
        {
            await task;
        }
        else if (result is not null)
        {
            var element = result.GetType().GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)).GetGenericArguments()[0];
            var drain = typeof(UiClientRoutesTests).GetMethod(nameof(Drain), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(element);
            await (Task)drain.Invoke(null, [result, token])!;
        }
    }

    private static async Task Drain<T>(IAsyncEnumerable<T> items, CancellationToken token)
    {
        await foreach (var item in items.WithCancellation(token))
        {
            _ = item;
        }
    }

    // why: a string parameter named ...Json is parsed before anything is sent (ApplyWorkflowOps' opsJson), so it gets an empty JSON array.
    private static object? DummyFor(ParameterInfo parameter, CancellationToken token) =>
        parameter.ParameterType == typeof(string) && parameter.Name!.EndsWith("Json", StringComparison.Ordinal) ? "[]" : Dummy(parameter.ParameterType, 0, token);

    private static object? Dummy(Type type, int depth, CancellationToken token)
    {
        if (type == typeof(CancellationToken))
        {
            return token;
        }

        if (type == typeof(Uri))
        {
            return _api;
        }

        if (type == typeof(string))
        {
            return "x";
        }

        if (Nullable.GetUnderlyingType(type) is not null)
        {
            return null;
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsPrimitive)
        {
            return Convert.ChangeType(1, type, CultureInfo.InvariantCulture);
        }

        return DummyOther(type, depth, token);
    }

    private static object? DummyOther(Type type, int depth, CancellationToken token)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        if (type.IsGenericType && type.GetGenericArguments() is [var only] && type.IsAssignableFrom(only.MakeArrayType()))
        {
            return Array.CreateInstance(only, 0);
        }

        if (type.IsInterface || type.IsAbstract || depth > MaxDepth)
        {
            return null;
        }

        return Construct(type, depth, token);
    }

    private static object? Construct(Type type, int depth, CancellationToken token)
    {
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(c => !(c.GetParameters() is [var p] && p.ParameterType == type))   // a record's copy constructor
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();
        try
        {
            return ctor is null
                ? RuntimeHelpers.GetUninitializedObject(type)
                : ctor.Invoke([.. ctor.GetParameters().Select(p => Dummy(p.ParameterType, depth + 1, token))]);
        }
        catch (Exception e) when (e is TargetInvocationException or ArgumentException or MissingMethodException)
        {
            return RuntimeHelpers.GetUninitializedObject(type);
        }
    }

    private WebApplication BuildHost()
    {
        var build = HostIntrospection.Host.GetType("Aw.Sindri.Host.LoopbackBinding", throwOnError: true)!
            .GetMethod("Build", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        var configure = HostIntrospection.Host.GetType("Aw.Sindri.Host.Pipeline", throwOnError: true)!
            .GetMethod("Configure", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        Func<string, string?> environment = name => name == "AW_SINDRI_HOME" ? _home : null;
        var app = (WebApplication)build.Invoke(null, [Array.Empty<string>(), environment, TimeProvider.System, null])!;
        _ = configure.Invoke(null, [app]);   // maps the endpoint table; the app is never started
        return app;
    }

    private static List<MappedRoute> Mapped(WebApplication app) =>
        [.. app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => new MappedRoute(
                e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [],
                new Regex("^" + Template(e.RoutePattern.RawText ?? string.Empty) + "/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))))];

    // {param} and {param:constraint} are one path segment; {*rest} and {**rest} are the remainder.
    private static string Template(string raw) =>
        string.Concat(Regex.Split(raw.TrimEnd('/'), @"(?<param>\{[^}]*\})", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1))
            .Select(part => part.StartsWith('{') ? (part.StartsWith("{*", StringComparison.Ordinal) ? ".+" : "[^/]+") : Regex.Escape(part)));

    private sealed record MappedRoute(IReadOnlyList<string> Methods, Regex Path)
    {
        public bool Matches(string method, string path) =>
            (Methods.Count == 0 || Methods.Contains(method, StringComparer.OrdinalIgnoreCase)) && Path.IsMatch(path);
    }

    private sealed class FixedToken : IHumanTokenSource
    {
        public string Location => "test";

        public ValueTask<string?> Read(CancellationToken cancellationToken) => ValueTask.FromResult<string?>("tok");
    }

    private sealed class Recorder : HttpMessageHandler
    {
        public List<(string Method, string Path)> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Seen.Add((request.Method.Method, path));
            var stream = path.EndsWith("/events", StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(stream ? string.Empty : "{}", Encoding.UTF8, stream ? "text/event-stream" : "application/json"),
            });
        }
    }
}
