using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.PauseService;
using Aw.Sindri.Features.RemoveSessionWorktree;
using Aw.Sindri.Features.ResumeJob;
using Aw.Sindri.Features.ResumeService;
using Aw.Sindri.Features.RunService;
using Aw.Sindri.Features.StartSession;

namespace Aw.Sindri.Host.Catalog;

/// <summary>API P6's rows (API spec §3.2, §4.1, UI spec §7): running a service now and cancelling a job (job class), resuming a job parked for a model harness (desk class), pausing and resuming a service (desk class, a human only).</summary>
internal static class ServiceRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        JobRows.Job("RunService", typeof(RunServiceRequest), "/sindri/services/{name}/run", "sindri services run <name>"),
        new(ActionId.From("PauseService"), typeof(PauseServiceRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/services/{name}/pause", ShellRows.Form("sindri services pause <name>"), "ready"),
        new(ActionId.From("ResumeService"), typeof(ResumeServiceRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/services/{name}/resume", ShellRows.Form("sindri services resume <name>"), "ready"),
        new(ActionId.From("CancelJob"), typeof(CancelJobRequest), Variant: null, ActionClass.Job, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", "/sindri/jobs/{jobId}/cancel", ShellRows.Form("sindri jobs cancel <jobId>"), "ready"),
        new(ActionId.From("StartSession"), typeof(StartSessionRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/sessions", ShellRows.Form("sindri agents start --repo <r> --agent <a> [--prompt <text>] [--worktree] [--act-id <id>]"), "ready"),   // agents spec §15.8: human only, no SessionOk
        new(ActionId.From("RemoveSessionWorktree"), typeof(RemoveSessionWorktreeRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: true, ReadsViews: false, Shortcut: null,
            "DELETE", "/sindri/sessions/{actId}/worktree", ShellRows.Form("sindri agents worktree remove <actId>"), "ready"),
        new(ActionId.From("ResumeJob"), typeof(ResumeJobRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/jobs/{jobId}/resume", ShellRows.Form("sindri jobs resume <jobId>"), "ready"),
    ];
}
