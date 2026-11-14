using System.Reflection;
using Aw.Kernel.Services;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>The chain as Program composes it (API spec §2.1): no command can skip a decorator, and queries get validation only.</summary>
public sealed class ChainRegistrationTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-chain-test");

    [Fact, Trait("Lane", "unit")]
    public void Every_registered_command_is_wrapped_gate_audit_authorize_validate_await_views_outermost_first()
    {
        var (services, scope) = Composed();
        using (scope)
        {
            var commands = Registered(services, typeof(ICommand<>));

            Assert.NotEmpty(commands);
            Assert.All(commands.Where(type => !IsStop(type) && !IsTestConnection(type) && !IsSignInConnection(type) && !IsStartSession(type)), type => Assert.Equal(
                [typeof(CommandGate<,>), typeof(AuditingService<,>), typeof(AuthorizingService<,>), typeof(ValidatingService<,>), typeof(AwaitingViews<,>), null],
                Chain(scope.ServiceProvider.GetRequiredService(type))));
        }
    }

    // A stop is audited and authorized like any command but never waits on the gate: once allowed, it cancels the holder (StoppingGate).
    [Fact, Trait("Lane", "unit")]
    public void The_stop_is_wrapped_audit_authorize_validate_then_cancels_the_gate_holder_without_waiting()
    {
        var (services, scope) = Composed();
        using (scope)
        {
            var stop = Assert.Single(Registered(services, typeof(ICommand<>)), IsStop);

            Assert.Equal(
                [typeof(AuditingService<,>), typeof(AuthorizingService<,>), typeof(ValidatingService<,>), typeof(StoppingGate<,>), typeof(AwaitingViews<,>), null],
                Chain(scope.ServiceProvider.GetRequiredService(stop)));
        }
    }

    // The connection test reads a secret and calls a provider (up to 15 s + the probe): holding the gate for that fails every other command with SND-API-009 (#2349). Only its status write takes the gate.
    [Fact, Trait("Lane", "unit")]
    public void The_connection_test_is_audited_and_authorized_but_does_not_hold_the_gate_while_it_probes()
    {
        var (services, scope) = Composed();
        using (scope)
        {
            var test = Assert.Single(Registered(services, typeof(ICommand<>)), IsTestConnection);

            Assert.Equal(
                [typeof(AuditingService<,>), typeof(AuthorizingService<,>), typeof(ValidatingService<,>), typeof(AwaitingViews<,>), null],
                Chain(scope.ServiceProvider.GetRequiredService(test)));
        }
    }

    // The sign-in reads the client secret and waits up to 5 min for the browser: holding the gate for that blocks every other command (#2369). Only its token write takes the gate.
    [Fact, Trait("Lane", "unit")]
    public void The_connection_sign_in_is_audited_and_authorized_but_does_not_hold_the_gate_while_it_waits()
    {
        var (services, scope) = Composed();
        using (scope)
        {
            var signIn = Assert.Single(Registered(services, typeof(ICommand<>)), IsSignInConnection);

            Assert.Equal(
                [typeof(AuditingService<,>), typeof(AuthorizingService<,>), typeof(ValidatingService<,>), typeof(AwaitingViews<,>), null],
                Chain(scope.ServiceProvider.GetRequiredService(signIn)));
        }
    }

    // A start makes a worktree, then a tmux session (up to 64 s): holding the gate that long blocks every other command. Only its ledger write takes the gate (A7, #3380).
    [Fact, Trait("Lane", "unit")]
    public void The_session_start_is_audited_and_authorized_but_does_not_hold_the_gate_while_it_starts()
    {
        var (services, scope) = Composed();
        using (scope)
        {
            var start = Assert.Single(Registered(services, typeof(ICommand<>)), IsStartSession);

            Assert.Equal(
                [typeof(AuditingService<,>), typeof(AuthorizingService<,>), typeof(ValidatingService<,>), typeof(AwaitingViews<,>), null],
                Chain(scope.ServiceProvider.GetRequiredService(start)));
        }
    }

    private static bool IsStartSession(Type service) => service.GenericTypeArguments[0] == typeof(Features.StartSession.StartSessionRequest);

    private static bool IsStop(Type service) => service.GenericTypeArguments[0] == typeof(Features.StopProcess.StopProcessRequest);

    private static bool IsTestConnection(Type service) => service.GenericTypeArguments[0] == typeof(Features.TestConnection.TestConnectionRequest);

    private static bool IsSignInConnection(Type service) => service.GenericTypeArguments[0] == typeof(Features.AddConnection.SignInConnectionRequest);

    [Fact, Trait("Lane", "unit")]
    public void Every_registered_command_has_a_validator()
    {
        var (services, scope) = Composed();
        using (scope)
        {
            Assert.All(Registered(services, typeof(ICommand<>)), type =>
                Assert.NotNull(scope.ServiceProvider.GetService(typeof(IValidator<>).MakeGenericType(type.GenericTypeArguments[0]))));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Queries_are_wrapped_in_validation_only()
    {
        var (services, scope) = Composed();
        using (scope)
        {
            var queries = Registered(services, typeof(IQuery<>));

            Assert.NotEmpty(queries);
            Assert.All(queries, type => Assert.Equal([typeof(QuerySpan<,>), typeof(ValidatingService<,>), null],
                Chain(scope.ServiceProvider.GetRequiredService(type))));
        }
    }

    /// <summary>The IService&lt;TRequest, _&gt; registrations whose request implements <paramref name="marker"/>.</summary>
    private static List<Type> Registered(IServiceCollection services, Type marker) =>
        [.. services.Select(d => d.ServiceType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IService<,>)
                && t.GenericTypeArguments[0].GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == marker))
            .Distinct()];

    /// <summary>The decorators from the outside in, then null for the handler itself.</summary>
    private static List<Type?> Chain(object service)
    {
        var chain = new List<Type?>();
        for (var current = service; current is not null; current = Inner(current))
        {
            chain.Add(IsDecorator(current) ? current.GetType().GetGenericTypeDefinition() : null);
            if (!IsDecorator(current))
            {
                break;
            }
        }

        return chain;
    }

    private static bool IsDecorator(object service)
    {
        for (var type = service.GetType().BaseType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Decorated<,>))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Decorated's protected Inner (the Kernel's ValidatingService has no Next).</summary>
    private static object? Inner(object service) =>
        IsDecorator(service)
            ? service.GetType().GetProperty("Inner", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(service)
            : null;

    private static (IServiceCollection Services, IServiceScope Scope) Composed()
    {
        var services = Composition.Compose(new ServiceCollection(),
            name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider());
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<TelemetryRuntime>();
        return (services, new OwningScope(provider));
    }

    /// <summary>A scope that disposes its provider with it.</summary>
    private sealed class OwningScope(ServiceProvider provider) : IServiceScope
    {
        private readonly IServiceScope _scope = provider.CreateScope();

        public IServiceProvider ServiceProvider => _scope.ServiceProvider;

        public void Dispose()
        {
            _scope.Dispose();
            provider.Dispose();
        }
    }
}
