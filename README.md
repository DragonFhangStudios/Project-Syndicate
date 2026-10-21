# Project Syndicate

A text-based life and city simulation RPG by Dragon Fhang Studios.

Extracted from [DFS-Gaming-Hub](https://github.com/DragonFhangStudios/DFS-Gaming-Hub) with history preserved.

## Layout

- `ProjectSyndicate.sln` — solution
- `src/DFS.ProjectSyndicate.Game` — game rules and state: characters, progression, careers, persistence (net10.0, no UI)
- `src/DFS.ProjectSyndicate.Web` — browser game: Blazor Web App, Interactive Server (net10.0)
- `src/DFS.ProjectSyndicate` — original WPF desktop app (net10.0-windows)
- `src/DFS.JobSystem` — job definitions and loading, including `Data/SimpleJobs.json` (netstandard2.1)
- `tests/DFS.ProjectSyndicate.Tests` — xUnit tests for the game library

## Build and test

Requires the .NET 10 SDK. The WPF project needs Windows; the other projects build anywhere.

```
dotnet build ProjectSyndicate.sln
dotnet test ProjectSyndicate.sln
```

## Run the web game

```
dotnet run --project src/DFS.ProjectSyndicate.Web --launch-profile http
```

Then open http://localhost:5026. Pick a demo character, take a job on the job board,
complete its tasks, and collect cash and XP. Progress is saved after every action.

The demo characters are for local development: there is no sign-in and nothing here is
account security. Do not expose the app publicly.

## Run the WPF app

Run from the build output folder (`src/DFS.ProjectSyndicate/bin/<Configuration>/net10.0-windows/`);
job data is loaded from `Data/SimpleJobs.json` relative to the working directory.

## Save data (web)

One JSON file per character in `src/DFS.ProjectSyndicate.Web/App_Data/saves/` (git-ignored, never
served over HTTP). Change the folder with the `Syndicate:SaveDirectory` setting.

- Writes go to a temporary file that then replaces the save, so an interrupted save cannot leave a partial file.
- A save that cannot be read is reported in the UI and the server log, and is never overwritten.
- Actions for the same character are serialized and always apply to the latest save, so a reward
  cannot be collected twice (for example from two browser tabs).

Schema versions:

| Version | Changes |
| --- | --- |
| 1 | Initial format. |
| 2 | Adds `TotalXP` (lifetime XP, never reduced, used for career tiers) and `JobsCompleted`. Version 1 saves load with `TotalXP` rebuilt from `Level` and `XP`, and are rewritten as version 2 on the next save. |

## Progression

- `XP` is progress within the current level; reaching level *L + 1* takes `100 × L` XP and resets it.
- `TotalXP` is lifetime XP and never goes down. Career tier requirements use it, so levelling up
  never takes a player further from promotion.
- Promotion requires the tier's lifetime XP and cash on hand (requirement plus fee); the fee is deducted.
