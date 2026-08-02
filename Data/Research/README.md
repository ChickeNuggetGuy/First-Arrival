# Research authoring

1. Create a `ResearchProject` resource for each project.
2. Give every project a permanent, unique `ProjectId`. Save data uses this ID, so do not rename it after release.
3. Set its point cost, optional per-project scientist limit, prerequisites, and results.
4. Add the project resource to `ResearchDatabase.tres`.

Project result order becomes part of the save format once a campaign has
completed that project. Add new results at the end rather than reordering
released results, so per-result completion tracking remains stable.

Projects with no prerequisites are immediately available. A project with prerequisites becomes available after all of them are complete. Each assigned scientist contributes one research point per simulated day.

Team research is managed on the globe. The base-screen Research button returns
to the globe and opens the same window, keeping one authoritative copy of hired
scientists, assignments, progress, and rewards on `GlobeTeamHolder`.

Available result resources are:

- `UnlockItemsResult`: unlocks its item resources for purchasing. For an item
  with `RequiredResearch`, that value must match the completing project's
  `ProjectId`; the database validator reports mismatches.
- `GrantFundsResult`: adds funds through the team finance ledger.
- `TriggerResearchEventResult`: queues an event ID on the team and emits
  `ResearchEventTriggered`. This is the hook for a future event/mission system;
  queued IDs remain saved until a consumer handles them.

The laboratory defaults to five scientist slots. Its capacity, price, upkeep, footprint, and construction time are editable in `Data/Facilities/ResearchLaboratory.tres`.
