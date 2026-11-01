using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.PauseService;
using Aw.Sindri.Features.ResumeJob;
using Aw.Sindri.Features.ResumeService;
using Aw.Sindri.Features.RunService;

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
        new(ActionId.From("ResumeJob"), typeof(ResumeJobRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/jobs/{jobId}/resume", ShellRows.Form("sindri jobs resume <jobId>"), "ready"),
    ];
}
