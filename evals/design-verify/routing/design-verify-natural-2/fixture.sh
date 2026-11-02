#!/usr/bin/env bash
# Offline desktop fixture: an .axaml view and a csproj naming Avalonia, nothing else.
set -euo pipefail
mkdir -p Views
cat > NoteApp.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="Avalonia" Version="11.2.0" /></ItemGroup>
</Project>
EOF
cat > Views/SidebarView.axaml <<'EOF'
<UserControl xmlns="https://github.com/avaloniaui"><TextBlock Text="Notes" /></UserControl>
EOF
