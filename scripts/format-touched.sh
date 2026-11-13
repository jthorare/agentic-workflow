#!/usr/bin/env bash
# STUB (red commit): passes the argument through naively, the form that checks nothing (#3564).
csproj="$(ls "$(dirname "$1")"/*.csproj | head -1)"
dotnet format "$csproj" --verify-no-changes --include "$1"
