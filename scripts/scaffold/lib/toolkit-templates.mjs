// scripts/scaffold/lib/toolkit-templates.mjs — the files /new-toolkit writes (docs/csharp/scaffolds/new-toolkit.md).
// `t` is the resolved input: { name, kebab, tool, toolPascal, needs: [{ kind, ... }], dir, feed, sdkVersion, sdk: pins }.
// A toolkit lives outside the repo, so it carries its own build rails: Directory.Build.props, .editorconfig,
// global.json, Directory.Packages.props and nuget.config. Layout:
//   <dir>/<Name>.Toolkit.csproj, Program.cs, <Name>Tools.cs, <Name>Json.cs
//   <dir>/<Name>.Toolkit.Tests/<Name>.Toolkit.Tests.csproj, <Tool>Tests.cs

const xml = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;");
const cs = (s) => JSON.stringify(s);

/** "1g" -> "1GiB", "500m" -> "500MiB", "2gib" -> "2GiB": the SDK's size text. */
export const sdkSize = (size) => {
  const m = /^([1-9][0-9]*)([kmgt])(i?b)?$/i.exec(size);
  return `${m[1]}${m[2].toUpperCase()}iB`;
};

/** The C# expression that declares one need through the SDK's Resources factory (the one place that knows its shape). */
export const needExpr = (n) => {
  if (n.kind === "database") return `Resources.Database(${cs(n.name)})`;
  if (n.kind === "disk") return `Resources.Disk(${cs(n.name)}, ${cs(sdkSize(n.size))})`;
  return `Resources.Connection(${cs(n.name)})${n.service ? ` /* ${n.service} */` : ""}`;
};

export const globalJson = (sdkVersion) => `{
  "sdk": {
    "version": "${sdkVersion}",
    "rollForward": "disable",
    "allowPrerelease": false
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
`;

export const nugetConfig = (feed) => `<?xml version="1.0" encoding="utf-8"?>
<!-- Scaffolded by /new-toolkit. Two sources: the local folder feed that scripts/install-cs.sh fills with the
     Aw.Sindri.Toolkit package (path from $AW_SINDRI_SDK_FEED, default $AW_SINDRI_HOME/sdk/packages), and nuget.org
     for everything else. Source mapping keeps the SDK id on the folder feed only. -->
<configuration>
  <packageSources>
    <clear />
    <add key="aw-sindri-sdk" value="${xml(feed)}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="aw-sindri-sdk">
      <package pattern="Aw.Sindri.Toolkit" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
`;

export const directoryBuildProps = () => `<Project>
  <!-- Scaffolded by /new-toolkit: this toolkit lives outside the agentic-workflow repo, so the repo's build rails
       (Directory.Build.props, analyzers, locked restore) don't reach it. These are its own, kept small. -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <MSBuildTreatWarningsAsErrors>true</MSBuildTreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
    <Deterministic>true</Deterministic>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
    <WarningsNotAsErrors>$(WarningsNotAsErrors);NU1901;NU1902</WarningsNotAsErrors>
    <UseArtifactsOutput>true</UseArtifactsOutput>
    <ArtifactsPath>$(MSBuildThisFileDirectory)artifacts</ArtifactsPath>
  </PropertyGroup>
  <PropertyGroup Condition="'$(CI)' == 'true'">
    <RestoreLockedMode>true</RestoreLockedMode>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  </PropertyGroup>
</Project>
`;

export const directoryPackagesProps = (t) => `<Project>
  <!-- Scaffolded by /new-toolkit. Exact pins, copied from the agentic-workflow repo's Directory.Packages.props at the
       time of scaffolding. Aw.Sindri.Toolkit comes from the folder feed in nuget.config. -->
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>false</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup Label="Analyzers">
${t.pins.analyzers.map((p) => `    <GlobalPackageReference Include="${p.id}" Version="${p.version}" />`).join("\n")}
  </ItemGroup>
  <ItemGroup>
    <PackageVersion Include="Aw.Sindri.Toolkit" Version="${t.sdkVersion}" />
    <PackageVersion Include="${t.pins.xunit.id}" Version="${t.pins.xunit.version}" />
  </ItemGroup>
</Project>
`;

export const editorconfig = () => `# Scaffolded by /new-toolkit: the toolkit's own analyzer policy (it is outside the repo's .editorconfig).
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
indent_style = space
indent_size = 4

[*.{csproj,props,config,json,yml}]
indent_size = 2

[*.cs]
dotnet_analyzer_diagnostic.severity = warning
dotnet_diagnostic.CA1707.severity = none           # test names use underscores
dotnet_diagnostic.CA1515.severity = none           # a toolkit's types are public to its test project
dotnet_diagnostic.CA2007.severity = none           # an app, not a library: no ConfigureAwait noise
dotnet_diagnostic.CA1848.severity = none
dotnet_diagnostic.MA0004.severity = none
dotnet_diagnostic.CS1591.severity = none           # no XML docs required
`;

export const gitignore = () => `artifacts/
bin/
obj/
`;

export const toolkitCsproj = (t) => `<Project Sdk="Microsoft.NET.Sdk">
  <!-- ${t.name} toolkit (scaffolded by /new-toolkit): a C# MCP stdio server built on Aw.Sindri.Toolkit. Sindri
       learns its tools and needs by running it with the manifest flag, and runs it in a sandbox (PR 16). -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <AssemblyName>${t.name}.Toolkit</AssemblyName>
    <RootNamespace>Toolkits.${t.name}</RootNamespace>
    <IsAotCompatible>true</IsAotCompatible>
    <PublishAot Condition="'$(RuntimeIdentifier)' != ''">true</PublishAot>
    <!-- The test project sits in a sub-folder: keep its sources out of this project. -->
    <DefaultItemExcludes>$(DefaultItemExcludes);${t.name}.Toolkit.Tests/**;artifacts/**</DefaultItemExcludes>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aw.Sindri.Toolkit" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="${t.name}.Toolkit.Tests" />
  </ItemGroup>
</Project>
`;

export const program = (t) => `using Aw.Sindri.Toolkit;
using Toolkits.${t.name};

return await ToolkitHost.RunAsync(${t.name}Tools.Build(), args, CancellationToken.None);
`;

export const jsonContext = (t) => `using System.Text.Json.Serialization;

namespace Toolkits.${t.name};

/// <summary>Source-generated JSON for every tool request and response (AOT-safe; the input schema comes from the request type).</summary>
// The SDK throws AW-TOOLKIT-001 unless both Respect* options are on: they make the input schema mark required fields.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(${t.toolPascal}Request))]
[JsonSerializable(typeof(${t.toolPascal}Response))]
internal sealed partial class ${t.name}Json : JsonSerializerContext;
`;

export const tools = (t) => {
  const needs = t.needs.map((n) => `.Needs(${needExpr(n)})`).join("\n            ");
  const R = `${t.toolPascal}Request`;
  const S = `${t.toolPascal}Response`;
  return `using Aw.Sindri.Toolkit;

namespace Toolkits.${t.name};

/// <summary>The input of ${t.tool}. A nullable property is optional in the tool's input schema.</summary>
public sealed record ${R}(string Subject);

/// <summary>The output of ${t.tool}.</summary>
public sealed record ${S}(string Result);

/// <summary>The ${t.kebab} toolkit: its needs and its tools.</summary>
public static class ${t.name}Tools
{
    /// <summary>Declares the toolkit. Add a tool with one more .Tool call; its schema comes from the request record.</summary>
    public static Toolkit Build() => Toolkit.Named(${cs(t.kebab)})
            .WithVersion("0.1.0")${needs ? `\n            ${needs}` : ""}
            .UseJson(${t.name}Json.Default)
            .Tool<${R}, ${S}>(${cs(t.tool)}, "TODO: one line on what ${t.tool} does", ${t.toolPascal});

    /// <summary>The ${t.tool} tool. Red until ${t.toolPascal}Tests passes (see ${t.name}.Toolkit.Tests).</summary>
    public static Task<${S}> ${t.toolPascal}(${R} request, ToolkitContext ctx, CancellationToken ct)
        => throw new NotSupportedException("${t.toolPascal}: make ${t.toolPascal}Tests pass");
}
`;
};

export const testsCsproj = (t) => `<Project Sdk="Microsoft.NET.Sdk">
  <!-- ${t.name}.Toolkit.Tests (scaffolded by /new-toolkit): xunit v3 on Microsoft.Testing.Platform. -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>Toolkits.${t.name}.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="${t.pins.xunit.id}" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../${t.name}.Toolkit.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
`;

export const sampleTest = (t) => `using Toolkits.${t.name};

namespace Toolkits.${t.name}.Tests;

public sealed class ${t.toolPascal}Tests
{
    [Fact]
    [Trait("Lane", "unit")]
    public async Task ${t.toolPascal}_answers_the_subject_it_was_given()
    {
        var response = await ${t.name}Tools.${t.toolPascal}(new ${t.toolPascal}Request("hello"), null!, TestContext.Current.CancellationToken);

        Assert.Equal("hello", response.Result);
    }
}
`;
