# Implementation Plan: Group Catalog

**Branch**: `001-group-catalog` | **Date**: 2026-10-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-group-catalog/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

An account registers, creates a group, and reads a shared catalog that was filled once from the senior-backend roadmap: 7 tracks, 72 topics, their answer keys and questions, and 6 worked designs. The catalog page shows that publication's last modification date. A password is 10 to 128 characters and is stored as a salted hash. A group never has more than 8 members, counting the creator. The same account may create another group and use a different display name there. The group creator creates the first session and sets its 2 or 3 topics. No session is created before the publication exists. Later sessions are created without topics. The first vote freezes the voters and candidates as the 2 to 8 people present then. Later attendance may drop below 2 and cannot exceed the group. That change leaves the voting set in place. Each session elects its own facilitator from the people in that session. The previous facilitator does not have to attend. A signed-in member of the group records the topics that facilitator sets. The facilitator may be a roster name with no account. A tie stores one of the leaders. Production chooses with equal chance, and tests replace that choice. A group may hold more than one session in the same week. Each group keeps its own pick counts and reviewed marks. Another group cannot see those, or the member list.

The app is one ASP.NET Core process. `Program.cs` calls extension methods and ends with `app.Run()`. Cookie setup lives in `Infrastructure/Auth`. `AddRazorPages` and `MapRazorPages` live in `Infrastructure/Web`. Persistence, time, logging, and the publish command each register themselves the same way. Pages are server-rendered. One PostgreSQL database holds the catalog and the group data. A one-shot publish command reads the roadmap markdown and commits the catalog in a single transaction. The publication row stores the source key and the date, not the row totals. After that, pages read the database only. Session rules live in that feature and do not know how the markdown was parsed. The 45-minute blocks are not in this feature.

## Technical Context

**Language/Version**: C# on .NET 10 (ASP.NET Core)

**Primary Dependencies**: ASP.NET Core Razor Pages, Entity Framework Core, Npgsql, and ASP.NET Core `PasswordHasher`. The hasher stores a unique salt inside each hash. There is no salt column and no plaintext password. The sign-in cookie is HttpOnly, SameSite=Lax, and Secure on HTTPS. No mediator, no identity-provider SDK, no SPA toolchain.

**Storage**: PostgreSQL 17. One database. Feature handlers use one `DbContext` directly. No repository layer.

**Testing**: xUnit and `WebApplicationFactory`. Domain rules are pure tests. Group isolation and the publish transaction are integration tests against PostgreSQL.

**Target Platform**: Linux server. One app process and one database on one small virtual machine. Local development uses the same pair.

**Project Type**: Server-rendered web application inside a modular monolith.

**Performance Goals**: A topic page and a session page are readable in under 1 second for one account on the small VM. The human tasks in the spec stay inside 3 minutes (open an answer key) and 2 minutes (create the first session with three topics and open their keys). The catalog is 72 topics and 6 designs, not a large search problem.

**Constraints**: No second publication in this feature. No live session, Telegram, billing, SSO, media hosting, or catalog editing. Logs must not contain answer-key text, question text, or member display names. The roadmap is read only by the publish command.

**Scale/Scope**: Tens of accounts, not thousands. Two groups are the acceptance case, including one account in both. 72 topics, 6 designs, a few hundred questions. A group has many sessions, not one current week.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Result | Evidence |
| --- | --- | --- |
| I. Quorum of two, no standing facilitator | Pass | `Group` and `Account` have no facilitator column. A group has at most 8 members, so the room never grows past 8. A session stores the member elected from that session's voting set. Every session elects its own facilitator. The previous session's facilitator does not have to attend, and that absence does not block the new election. This feature does not run the 45-minute blocks. |
| II. Everyone practices | Pass | The answer key is on the topic page. Pick counts are per topic for a group, not a ranking of people. No leaderboard. |
| III. Content is data, read from the database | Pass | The publish command is the one sync. Runtime pages query PostgreSQL. `RoadmapParser` is the one seed path for this feature. A later source can add rows with its own source key without a session-rule change. Sessions depend on topic ids, not on markdown or the roadmap URL. |
| III. First catalog is the senior-backend roadmap, not the only possible catalog | Pass | Rows carry a source key. A later source can insert more topics without a session-rule change. This feature does not author those rows. |
| IV. Group-scoped from the first table | Pass | Memberships, sessions, votes, and topic progress include `GroupId` and are queried with it. The curated catalog is shared product content, which matches the spec and the rule that the owner offers that catalog. A second group is another row. One account may have a membership in each. |
| IV. No billing, SSO, marketplace, or mobile | Pass | Not in this plan. |
| V. A person present uses the web UI, no adapter required | Pass | Register, catalog, sessions, votes, and members are Razor pages. Telegram is absent. The publish command is the preparation step in the spec, not a channel anyone needs on the night. |
| V. No media hosting | Pass | Designs and topics are stored text. |
| Privacy | Pass | Answer text and display names are not logged. Member names, sessions, and pick counts are filtered by group. |
| Out of scope items | Pass | No katas, points, video, billing, or SSO. No 45-minute runner. |
| One process, one database, no repository layer | Pass | Handlers use `QuorumDbContext`. PostgreSQL is the one database, not a second product service. |
| Delivery bar for this spec | Pass | The person served is the signed-in account. The outcome is an answer key, sessions, an elected facilitator, and that session's topics. Pairing, the odd-count ring, the clock, and rubric scores belong to the live-session feature. |

## Project Structure

### Documentation (this feature)

```text
specs/001-group-catalog/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── pages.md
│   └── publish-catalog.md
└── tasks.md             # created later by /speckit-tasks
```

### Source Code (repository root)

```text
src/Quorum.App/
├── Program.cs                  # extension calls, then app.Run()
├── SharedKernel/
│   ├── AccountId.cs
│   ├── GroupId.cs
│   ├── MemberId.cs
│   ├── SessionId.cs
│   ├── IClock.cs
│   └── ContentKey.cs
├── Features/
│   ├── Accounts/
│   ├── Groups/
│   ├── Catalog/
│   │   ├── Browse/
│   │   └── Publish/
│   │       └── PublishExtensions.cs
│   ├── Sessions/
│   └── Shared/
│       └── _Layout.cshtml
└── Infrastructure/
    ├── Auth/
    │   └── AuthExtensions.cs
    ├── Persistence/
    │   ├── QuorumDbContext.cs
    │   └── PersistenceExtensions.cs
    ├── Time/
    │   ├── SystemClock.cs
    │   └── TimeExtensions.cs
    ├── Logging/
    │   ├── SafeLog.cs
    │   └── LoggingExtensions.cs
    └── Web/
        └── WebExtensions.cs

tests/
├── Quorum.Catalog.Tests/
├── Quorum.Sessions.Tests/
├── Quorum.Groups.Tests/
└── Quorum.App.Tests/
```

**Structure Decision**: One web project. Each feature folder owns its pages, commands, and EF configurations. That folder tree is the backend layout. `Features/Shared/_Layout.cshtml` is the shared Razor UI shell those pages use so a narrow browser window stays readable. It is not another backend module. `SharedKernel` holds only identity types, the clock, and content keys. `Infrastructure` holds the single `DbContext`. It is not a second service. `Program.cs` calls extension methods and ends with `app.Run()`. Cookie options and auth middleware live in `Infrastructure/Auth/AuthExtensions.cs`. `AddRazorPages` and `MapRazorPages` live in `Infrastructure/Web/WebExtensions.cs`. Persistence, time, and logging each expose their own extension and are called the same way. The logging call is added in `Program.cs` when that extension exists. The publish command is registered from `Features/Catalog/Publish`, not parsed inside `Program.cs`. There is no `ICatalogSeedSource` in this feature. `RoadmapParser` is the one seed path. Tests sit beside the features they lock. `Sessions` stores the meeting, its members, the election, and its topics. The 45-minute runner is not created in this feature.

## Complexity Tracking

No constitution violations. This table is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --- | --- | --- |
| | | |

## Post-design Constitution Check

Re-checked after `research.md`, `data-model.md`, `contracts/`, and `quickstart.md`.

| Gate | Result | Evidence |
| --- | --- | --- |
| Runtime reads the database, not the roadmap | Pass | Pages and session commands use topic ids stored by the one publication. The roadmap path appears only in the publish command. |
| Second group needs no table split | Pass | `GroupId` is on membership, session, and topic progress. Catalog tables have no `GroupId`. An account has one membership per group. |
| No second client or public API | Pass | Contracts are Razor routes plus one operator command. No JSON API and no Telegram adapter. |
| Session rules do not depend on the parser | Pass | The parser writes catalog rows. Pick counts and track-reviewed are computed from `TopicProgress` and topic-to-track links. Publication totals are counts of child rows. |
| Logs and privacy | Pass | Contracts forbid logging answer text, question text, and display names. |
| Scope | Pass | Designs are readable and cannot be session topics. Code katas are not a content kind in this schema. The 45-minute runner is not a folder in this plan. |

Gate result: pass. No design change required.
