using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Testing;
using Table = Aw.Sindri.Core.Commands.Authorization;

namespace Aw.Sindri.Tests.Commands.Authorization;

/// <summary>API spec §4.2's table, restated here independently of the code, and the refusal it gives.</summary>
public sealed class AuthorizationTests
{
    /// <summary>Who may run each class: h = human at a terminal, a = human in the app, s = session, l = a session Sindri launched, v = any service.</summary>
    private static readonly Dictionary<ActionClass, string> _table = new()
    {
        [ActionClass.Request] = "haslv",
        [ActionClass.StepReport] = "sl",     // only a session; the service checks its launch secret (SND-AUTH-003)
        [ActionClass.Pause] = "haslv",       // every session may take over (agent-first PR 2)
        [ActionClass.Job] = "haslv",
        [ActionClass.Decision] = "hasl",
        [ActionClass.Desk] = "hasl",         // plus a service the row grants
        [ActionClass.Local] = "",            // never through the API
        [ActionClass.Issue] = "hasl",        // its nonce, heartbeat and exit
    };

    public static TheoryData<ActionClass, string, bool> Cases()
    {
        var cases = new TheoryData<ActionClass, string, bool>();
        foreach (var actionClass in Enum.GetValues<ActionClass>())
        {
            foreach (var caller in (string[])["human", "session", "launched", "service", "none"])
            {
                foreach (var terminal in (bool[])[false, true])
                {
                    cases.Add(actionClass, caller, terminal);
                }
            }
        }

        return cases;
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public void Authorization_matches_the_table_for_every_class_caller_and_terminal(ActionClass actionClass, string caller, bool terminal)
    {
        var context = caller switch
        {
            "human" => terminal ? A.Caller().AtTerminal().Build() : A.Caller().Human().Build(),
            "session" => A.Caller().Session().Build() with { Terminal = terminal },   // a claimed terminal counts only for a human
            "launched" => A.Caller().Launched().Build() with { Terminal = terminal },
            "service" => A.Caller().Service("jobs").Build() with { Terminal = terminal },
            _ => null,
        };
        var who = caller switch { "human" => terminal ? 'h' : 'a', "session" => 's', "launched" => 'l', "service" => 'v', _ => '-' };

        var decision = Table.Decide(actionClass, context, Rows.Of(actionClass));

        Assert.Equal(_table[actionClass].Contains(who, StringComparison.Ordinal), decision.Allowed);
        Assert.Equal(decision.Allowed ? null : SindriErrors.AuthRefused.Code, decision.Refusal?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_caller_context_is_refused()
    {
        var decision = Table.Decide(ActionClass.Request, caller: null, Rows.Of(ActionClass.Request));

        Assert.False(decision.Allowed);
        Assert.Equal("none", decision.Caller);
        Assert.Equal(SindriErrors.AuthRefused.Code, decision.Refusal?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_caller_class_the_table_does_not_know_is_refused_not_treated_as_a_service()
    {
        var unknown = A.Caller().Service("jobs").Build() with { Class = (CallerClass)99 };

        var decision = Table.Decide(ActionClass.Request, unknown, Rows.Of(ActionClass.Request));

        Assert.False(decision.Allowed);
        Assert.Equal(SindriErrors.AuthRefused.Code, decision.Refusal?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Service_host_may_stop_and_nothing_else_desk()
    {
        var host = A.Caller().Service("host").Build();
        var stop = Rows.Of(ActionClass.Desk, "StopProcess", cli: "sindri stop", grants: "host");

        Assert.True(Table.Decide(ActionClass.Desk, host, stop).Allowed);
        Assert.Equal("service:host", Table.Decide(ActionClass.Desk, host, stop).Caller);
        Assert.False(Table.Decide(ActionClass.Desk, host, Rows.Of(ActionClass.Desk, "PauseService")).Allowed);
        Assert.False(Table.Decide(ActionClass.Desk, A.Caller().Service("heartbeat").Build(), stop).Allowed);
        Assert.False(Table.Decide(ActionClass.Decision, host, Rows.Of(ActionClass.Decision, grants: "host")).Allowed);
    }

    [Fact, Trait("Lane", "unit")]
    public void Step_report_is_for_a_session_only_and_the_launch_secret_is_the_services_check()
    {
        var row = Rows.Of(ActionClass.StepReport, "CompleteStep");

        Assert.All([A.Caller().AtTerminal().Build(), A.Caller().Human().Build(), A.Caller().Service("engine").Build()],
            caller => Assert.False(Table.Decide(ActionClass.StepReport, caller, row).Allowed));
        Assert.True(Table.Decide(ActionClass.StepReport, A.Caller().Session().Build(), row).Allowed);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_caller_class_may_run_every_non_local_action_class()
    {
        ActionClass[] open = [ActionClass.Request, ActionClass.Pause, ActionClass.Job, ActionClass.Decision, ActionClass.Desk, ActionClass.Issue];
        CallerContext[] callers = [A.Caller().AtTerminal().Build(), A.Caller().Human().Build(), A.Caller().Session().Build(), A.Caller().Launched().Build()];

        foreach (var actionClass in open)
        {
            Assert.All(callers, caller => Assert.True(Table.Decide(actionClass, caller, Rows.Of(actionClass)).Allowed, $"{actionClass} for {caller.Actor}"));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_row_flagged_NeedsTerminal_no_longer_narrows_any_caller()
    {
        var row = Rows.Of(ActionClass.Desk, "ShowLineHoldout") with { NeedsTerminal = true };

        Assert.All([A.Caller().AtTerminal().Build(), A.Caller().Human().Build(), A.Caller().Session().Build(), A.Caller().Launched().Build()],
            caller => Assert.True(Table.Decide(ActionClass.Desk, caller, row).Allowed));
        Assert.Equal(["human+terminal", "human", "session"], Table.CallersFor(row));
    }

    [Fact, Trait("Lane", "unit")]
    public void Refusal_names_the_command_and_its_cli()
    {
        var service = A.Caller().Service("heartbeat").Build();

        var stop = Table.Decide(ActionClass.Desk, service, Rows.Of(ActionClass.Desk, "StopProcess", cli: "sindri stop")).Refusal;

        Assert.Equal("StopProcess is not available to this caller: sindri stop", stop?.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void Callers_for_each_class_follow_the_table()
    {
        Assert.Equal(["human+terminal", "human", "session", "service"], Table.CallersFor(ActionClass.Request, []));
        Assert.Equal(["human+terminal", "human", "session", "service:host"], Table.CallersFor(ActionClass.Desk, ["host"]));
        Assert.Equal(["human+terminal", "human", "session"], Table.CallersFor(ActionClass.Decision, ["host"]));
        Assert.Equal(["human+terminal", "human", "session"], Table.CallersFor(ActionClass.Desk, []));
        Assert.Equal(["session"], Table.CallersFor(ActionClass.StepReport, []));
        Assert.Empty(Table.CallersFor(ActionClass.Local, []));
    }
}
