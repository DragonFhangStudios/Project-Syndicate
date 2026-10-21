# Project Syndicate

A text-based life and city simulation RPG by Dragon Fhang Studios.

Extracted from [DFS-Gaming-Hub](https://github.com/DragonFhangStudios/DFS-Gaming-Hub) with history preserved.

## Layout

- `ProjectSyndicate.sln` — solution
- `src/DFS.ProjectSyndicate` — WPF desktop app (net8.0-windows)
- `src/DFS.JobSystem` — job data and loading (netstandard2.1)

## Build

Requires Windows and the .NET SDK (8.0 or later).

```
dotnet build ProjectSyndicate.sln
```

Run from the build output folder (`src/DFS.ProjectSyndicate/bin/<Configuration>/net8.0-windows/`);
job data is loaded from `Data/SimpleJobs.json` relative to the working directory.
