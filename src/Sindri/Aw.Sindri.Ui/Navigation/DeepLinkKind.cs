namespace Aw.Sindri.Ui.Navigation;

/// <summary>What a deep link opens (UI spec §3.3 "Deep links inside the app").</summary>
internal enum DeepLinkKind
{
    /// <summary>'home/needs-you': Home with its Needs you panel expanded.</summary>
    HomeNeedsYou,

    /// <summary>'needs-you/&lt;rowId&gt;': a needs-you row's detail.</summary>
    NeedsYou,

    /// <summary>'item/&lt;ItemId&gt;': an item's detail.</summary>
    Item,

    /// <summary>'decision/&lt;DecisionId&gt;': a decision's detail.</summary>
    Decision,

    /// <summary>'service/&lt;name&gt;': a service's detail.</summary>
    Service,

    /// <summary>'job/&lt;JobId&gt;': a job's detail.</summary>
    Job,

    /// <summary>'workflow/&lt;address&gt;': a workflow instance in the Workflows frame (screens spec §3).</summary>
    Workflow,

    /// <summary>'recipe/&lt;id&gt;': a recipe's inspector (screens spec §4.3).</summary>
    Recipe,

    /// <summary>'node/&lt;key&gt;': a context node (screens spec §4.4).</summary>
    Node,

    /// <summary>'context?item=&lt;ItemId&gt;': the Context page for an item, at its frozen scope (context spec §9 item 1).</summary>
    ContextItem,

    /// <summary>'session/&lt;provider&gt;/&lt;id&gt;': a live agent session's sheet (screens spec §4.5).</summary>
    Session,

    /// <summary>'system/&lt;dependency&gt;': a dependency's doctor check (screens spec §4.6).</summary>
    System,

    /// <summary>'setup': Settings, scrolled to the setup checklist (setup spec §5).</summary>
    Setup,

    /// <summary>'projects': the Projects page.</summary>
    Projects,

    /// <summary>'project/&lt;escaped id&gt;': a project card's sheet (projects page spec §3.2); the id is url-escaped as one segment, since a led project's key holds a slash.</summary>
    Project,

    /// <summary>'projects/add': the Add project sheet over the Projects page (projects page spec §3.2); it has no subject.</summary>
    ProjectAdd,

    /// <summary>'context/page': the page sheet (Name this page, Edit page, New page) opened from the Context page's Pages and actions.</summary>
    PageSheet,

    /// <summary>'context/pages/add': the Add several pages sheet opened from the Context page's Pages and actions.</summary>
    AddSeveralPages,
    /// <summary>'context/pin': the Pin sheet (Pin this, Edit pin, New pin) opened from the Context page's concept layer; it has no subject.</summary>
    PinSheet,
}
