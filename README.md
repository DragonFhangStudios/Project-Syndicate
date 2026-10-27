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
| 2 | Adds `TotalXP` (lifetime XP, never reduced, used for career tiers) and `JobsCompleted`. Version 1 saves load with `TotalXP` rebuilt from `Level` and `XP`. |
| 3 | Adds `CareerPath` (always `Civilian` for now) and `CreatedAtUtc` (player-created characters). Version 1 and 2 saves load as Civilian with no creation time. |

Older saves are upgraded in memory when loaded and rewritten in the current version only when that
character next saves; listing characters never rewrites a save.

## Characters

- **Player-created characters** get a random id such as `char-3f9a1b2c4d5e`, independent of the name,
  and start as Civilians at level 1 with $500 and 5 in each attribute. Names are 2–24 characters:
  letters, spaces, apostrophes, hyphens and periods; demo names and existing names are taken.
  Creation never overwrites an existing save, and a repeated form submission creates one character.
- **Demo characters** (Sonny Perelli, Carlo Valentini, Giovanni Ragoli) are development test profiles
  with fixed ids. Their saves are kept as they are.

## Progression

- `XP` is progress within the current level; reaching level *L + 1* takes `100 × L` XP and resets it.
- `TotalXP` is lifetime XP and never goes down. Career tier requirements use it, so levelling up
  never takes a player further from promotion.
- Promotion requires the tier's lifetime XP and cash on hand (requirement plus fee); the fee is deducted.

## Civilian jobs

Defined in `src/DFS.JobSystem/Data/SimpleJobs.json` (`MinLevel` / `MinTier` gate a job; 0 or 1 means
open to everyone). Requirements are only level and tier, which every character raises by working, so
no job is permanently out of reach. Eligibility is checked by the game rules when a job is accepted,
not only by the UI.

| Job | Unlocks at | Tasks | Pay | XP | Avg pay per task | Avg XP per task | Role |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Street Trash Cleanup | — | 4 | $5 | 40 | $1 | 10 | Fastest XP, almost no pay |
| Grocery Delivery Run | — | 3 | $50 | 20 | $17 | 6.7 | Short and balanced |
| Diner Night Shift | — | 5 | $100 | 15 | $20 | 3 | Best early pay, slow XP |
| Warehouse Inventory | Level 2 | 5 | $130 | 30 | $26 | 6 | Better on both once levelled |
| Night Security Patrol | Tier 2 (Worker) | 6 | $220 | 45 | $37 | 7.5 | Open the moment you're promoted |
| Night Freight Sorting | Tier 2, Level 2 | 5 | $260 | 40 | $52 | 8 | First raise after promotion |
| Fleet Maintenance Crew | Tier 2, Level 3 | 6 | $340 | 50 | $57 | 8.3 | Most XP per contract below level 4 |
| Municipal Records Contract | Tier 2, Level 3 | 6 | $420 | 35 | $70 | 5.8 | Best pay at level 3, lightest XP |
| Logistics Dispatch Coordinator | Tier 2, Level 4 | 7 | $575 | 55 | $82 | 7.9 | Top Worker contract, best pay and XP |
| Commercial Property Inspector | Tier 3, Level 4 | 6 | $720 | 60 | $120 | 10 | First raise after promotion |
| Hospital Supply Coordinator | Tier 3, Level 5 | 7 | $860 | 75 | $123 | 10.7 | Most XP per task |
| Port Customs Broker | Tier 3, Level 5 | 8 | $1,050 | 50 | $131 | 6.3 | Best pay at level 5, lightest XP |
| City Contracts Manager | Tier 3, Level 6 | 8 | $1,300 | 80 | $162 | 10 | Top Professional contract, best pay and XP |

Pay and XP are paid in full only when the whole contract is finished. The job board shows the average
per task (derived from each job's terms, not stored) so jobs of different lengths compare fairly. Within
each rank no contract leads on both averages, and the rank's top contract (Dispatch for Workers, the City
Contracts Manager for Professionals) leads on pay. Open jobs are
grouped by rank with the character's best rank first and the best-paid work first within it; locked
jobs follow, nearest unlock first, with what each one still needs.

Job data is checked when the web game starts, and any problem stops it with an error instead of
silently dropping a job.

- **Ids** are unique groups of lowercase letters (`a`–`z`) and digits (`0`–`9`) joined by single
  underscores, at most 64 characters: `night_security` or `route_2`. Capitals, spaces, hyphens,
  accented letters, line breaks and leading, trailing or doubled underscores are rejected.
- Every job also needs a title, at least one named task, pay and XP of zero or more, and level and
  tier requirements of zero or more, with the tier no higher than the top tier.
- **A problem with one entry** (an empty entry, a duplicate, missing or malformed id, a missing title or
  tasks) is reported with its entry number in the file, counting from 1, and its id when it has one.
- **A file that can't be read as a list of jobs** (invalid JSON, a single object instead of a list,
  `null` or an empty list) is reported for the file as a whole: for invalid JSON the message carries
  the parser's description of where reading failed, since no job can be identified.

### Tier 1 → Tier 2 (Worker)

Tier 2 needs 150 lifetime XP and $1,500 on hand ($1,000 plus the $500 fee).

- A new character ($500) needs about $1,000 more. The fastest possible route is **10 jobs**
  (for example ten Diner shifts, or seven Diner shifts then three Warehouse jobs); a natural mix of
  two Trash Cleanups, two Grocery runs and then Warehouse work takes **11**. Repeating only Trash
  Cleanup would take about 200, so variety is the point.
- Demo characters start with more cash, so XP is their limit: Sonny and Giovanni qualify after 4
  Trash Cleanups; Carlo ($1,200) needs 5 jobs.
- The Worker contracts all need Tier 2, so they do not change these numbers.

### Tier 2 (Worker) → Tier 3 (Professional)

Tier 3 needs 400 lifetime XP and $7,000 on hand ($5,000 plus the $2,000 fee). Workers have far more
than 400 XP long before they have the cash, so **cash is the limit**; XP decides which contracts are
open (level 3 opens Fleet Maintenance and Municipal Records, level 4 opens Dispatch).

Worker contracts needed after promotion, played through the game rules:

| Strategy | After the natural route ($1,020, 330 XP, level 3) | After the fastest route ($1,000, 150 XP, level 2) |
| --- | --- | --- |
| Fewest possible | 13 | 15 |
| Cash-focused (best pay on offer) | 13 | 16 |
| XP-focused (most XP on offer) | 13 | 16 |
| Rotating through every Worker contract | 18 | 20 |
| Night Security only (before Phase 4) | 28 | 28 |

The design target is roughly 12–20 contracts for a reasonable strategy. A cash-focused Worker climbs
the ladder: Freight until level 3, Records until level 4, then Dispatch.

### Tier 3 (Professional) → Tier 4 (Executive)

Tier 4 needs 800 lifetime XP and $20,000 on hand ($15,000 plus the $5,000 fee). Again **cash is the
limit**: a new Professional has $5,050–$5,545 and needs about $15,000 more. XP decides which
contracts are open (level 5 opens Hospital Supply and Port Customs, level 6 the Contracts Manager).

Professional contracts needed after promotion, played through the game rules. Each column is a
Professional made by one of the Tier 1 routes above followed by a Tier 2 strategy from the table above:

| Strategy | Natural, then cash-focused ($5,255, 885 XP, level 4) | Natural, then rotating ($5,050, 1,135 XP, level 5) | Fastest, then cash-focused ($5,545, 790 XP, level 4) | Fastest, then rotating ($5,430, 1,030 XP, level 5) |
| --- | --- | --- | --- | --- |
| Fewest possible | 15 | 13 | 15 | 14 |
| Cash-focused | 15 | 14 | 15 | 14 |
| XP-focused | 15 | 14 | 16 | 14 |
| Rotating through every Professional contract | 17 | 16 | 17 | 16 |
| Dispatch only (before Phase 5) | 26 | 26 | 26 | 26 |

The design target is again roughly 12–20 contracts. A cash-focused Professional climbs the ladder:
Inspector until level 5, Customs until level 6, then the Contracts Manager. A character played from
creation along the natural route and then rotating through each rank's contracts becomes an Executive
after 45 jobs (11 + 18 + 16).

These numbers are checked by `CareerBalanceTests`, `Tier3BalanceTests` and `Tier4BalanceTests`. The fewest-jobs search
covers every job sequence but keeps only the states not beaten on both cash and XP, so it stays fast
as jobs are added. Changing a job or a tier that moves a number means updating the test and this
README together.

## Prototype limitations

Deliberately kept for now:

- **Local demo only.** There is no sign-in: anyone who can reach the app can play any character by
  its URL. It listens on localhost; do not host it publicly.
- **Civilian path only.** `CareerPath` is always `Civilian`; Criminal and Emergency Services careers
  are not started.
- **No content for Tier 4 and above.** Executives and Business Tycoons can promote but have no jobs of
  their own yet; they keep access to every civilian job.
- **The WPF app ignores job requirements.** It lists and accepts every job, including the level- and
  tier-gated ones, and does not run the web game's job data checks. It still loads all thirteen jobs.
- **Jobs are instant.** No timers, energy, failure chance, items or other currencies.
- A job is worked one task per click; the per-task figures are a comparison aid, not a time cost.

## Tests

`tests/DFS.ProjectSyndicate.Tests` (xUnit) covers the game library:

- **Rules:** progression and levelling, promotion and fees (`ProgressionTests`, `PromotionProgressTests`),
  job acceptance and task order (`CareerServiceTests`), eligibility and unlock thresholds
  (`JobEligibilityTests`, `MidgameJobTests`, `ProfessionalJobTests`), and every job played start to finish
  (`CivilianLoopTests`).
- **Job data:** the shipped catalog, the original five jobs' terms, and rejection of duplicate,
  missing and malformed ids and malformed files (`JobCatalogDataTests`).
- **Balance:** the Tier 2, Tier 3 and Tier 4 numbers above (`CareerBalanceTests`, `Tier3BalanceTests`,
  `Tier4BalanceTests`).
- **Persistence and sessions:** saving and loading, version 1–3 saves, corrupt saves left untouched,
  restarts, concurrent and stale sessions, removed or out-of-range active jobs, character creation
  (`JsonFilePlayerRepositoryTests`, `GameServiceTests`, `GameActionTests`, `CharacterCreationTests`,
  `CivilianLoopTests`).
- **WPF compatibility:** the WPF loading path reads the same thirteen jobs (`CivilianLoopTests`).

The Blazor pages have no automated UI tests; check them in a browser after UI changes.
