# Final assessment evidence

This maps the 20 criteria in the course requirements to the project. Each criterion is worth 5%. The examiner decides the score and may ask either teammate about any part.

The presentation includes both full names, the topic and aims, development plan, technology comparison, architecture, practical use, and methodology. Defence notes include a 17 minute outline and a live demonstration.

## Evidence matrix

| # | Criterion | Project evidence | Boundary or check |
| --- | --- | --- | --- |
| 1 | Code Structure and Design | Core records and pure rules are separate from Web pages and storage services. | Database entities are not returned to the UI. |
| 2 | Component Reusability | TaskEditor, TaskCard, DayPlanCard, ProgressMeter, and Notice have defined parameters and callbacks. | Reused on the relevant list, form, planner, and progress screens. |
| 3 | Cross-Platform Compatibility | .NET and browser-based UI; automated CI platform jobs. | Final platform results recorded below; emulation is not physical-device testing. |
| 4 | State Management | Scoped WorkspaceState, form EditContext, explicit refresh after saves, persisted availability. | Circuit state and saved database state have different lifetimes. |
| 5 | UI Design | Consistent spacing, type, colour, forms, cards, and navigation. | Screenshots in the final report show actual application screens. |
| 6 | User Experience | Clear empty states, validation, deletion confirmation, unsaved-change guards, retry feedback. | Forms and navigation are exercised in browser scenarios. |
| 7 | Responsiveness | Flexible grids, media queries, mobile navigation, stacked planner controls. | Viewport checks recorded below. |
| 8 | Core Functionality | Subject/task CRUD, completion, search, filters, availability, plans, and progress. | Complete workflow covered by integration and browser checks. |
| 9 | Advanced Features | Pure multi-day allocation, capacity limits, deterministic ordering, unscheduled-work reasons. | Uses Blazor server interactivity; no custom cloud service or role-based accounts. |
| 10 | Error Handling and Resilience | Service validation, database failure messages, atomic writes, foreign keys, protected deletion. | Tests include rollback and invalid input. |
| 11 | Efficiency | Deadline ordering plus a forward day cursor, read-only queries, short-lived contexts. | Repeatable planner probe measures a defined synthetic workload. |
| 12 | Scalability | Pure planner separated from storage and UI; probe checks larger task sets. | Local SQLite workspace, not a verified high-concurrency hosted service. |
| 13 | Resource Utilization | Contexts are disposed; no polling loop; plan calculation uses supplied snapshots. | Performance evidence is limited to measured scenarios. |
| 14 | Testing Coverage | Unit, SQLite integration, and browser scenarios. | Counts and measured coverage are recorded below. |
| 15 | Quality Assurance | Desktop/mobile viewport review, browser tests, form and failure-flow checks. | Physical phones/tablets were not used. |
| 16 | Documentation | README, user guide, developer contracts, report, defence notes, and commands. | The historical Part 1 report remains labelled as an earlier delivery. |
| 17 | Innovation | Combines coursework estimates and weekly capacity with explicit remaining-work explanations. | A student-focused implementation, without a claim that scheduling itself is new. |
| 18 | Creative Use of FP and Blazor | Immutable records, pure planner/statistics, explicit dates, LINQ, reusable Razor components. | Side effects remain in storage services. |
| 19 | Professionalism | Runnable repository, migrations, isolated tests, Git workflow, consistent documentation. | Intended deployment is a local course project; public hosting needs further work. |
| 20 | Presentation | Editable slides with architecture, comparison, real screens, results, and speaking notes. | Both teammates must practise the full flow and attend the required defence. |

## Recorded verification

| Check | Recorded result |
| --- | --- |
| Release build | 0 warnings and 0 errors |
| .NET suite | 56 tests passed |
| Local browser scenarios | 9 passed in Chrome and 9 in Edge |
| CI platform suite | Build and all 56 tests passed on Windows, Ubuntu, and macOS |
| Linux browser engines | Chromium, Firefox, and WebKit each passed all 9 scenarios |
| Coverage | Core and Web.Data: 90.30% lines, 87.80% branches; migrations excluded, async state machines included |
| Planner coverage | 100% lines and branches |
| Viewport review | 390 px mobile, 768 px tablet, and 1440 px desktop widths |
| Published build | Release output ran with a fresh database, migrations, sample data, planner, and progress |
| Keyboard check | Tab moved from Monday to Tuesday availability input in the published build |

The viewport and keyboard checks are targeted QA, not a full accessibility audit or physical-device certification.

The planner probe used 1,000, 10,000, and 50,000 tasks with a 90 day window and 30 minutes on Mondays only. Observed times were 2.8 ms, 21.1 ms, and 19.9 ms. Every case planned 390 minutes, preserved all remaining minutes, and respected capacity. These single diagnostic timings include warm-up and system variation. They are not timing thresholds or a concurrent-user load test.

## Repeating checks

Run `dotnet test StudyFlow.sln` for the automated .NET suite. Run `npm ci`, `npx playwright install chromium`, and `npm run test:e2e` for browser scenarios. See the repository workflow and performance probe for their exact configuration.

## Team preparation

Both students must be involved in programming and understand the whole project. Git history records actual commits, while the presentation division describes who introduces each area. A member who does not attend the mandatory defence receives 0 under the supplied requirements.
