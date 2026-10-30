---
description: "Task list for the group catalog feature"
---

# Tasks: Group catalog

**Input**: Design documents from `/specs/001-group-catalog/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. `quickstart.md` makes `dotnet test` the acceptance check for this feature. Write the tests for a story first and confirm they fail before the implementation tasks for that story.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested on its own.

**Out of scope for every task**: a voting countdown, a session schedule, Telegram, a calendar, reminders, the 45-minute practice blocks, pairing, rubric scores, billing, SSO, and code katas. The election ends only when every voter has cast a choice. A missing vote leaves the facilitator empty. Do not store a meeting time.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Setup and foundational tasks have no story label

## Path Conventions

- Web app: `src/Quorum.App/`
- Tests: `tests/Quorum.Catalog.Tests/`, `tests/Quorum.Sessions.Tests/`, `tests/Quorum.Groups.Tests/`, `tests/Quorum.App.Tests/`
- One ASP.NET Core process, Razor Pages, one `QuorumDbContext`, no repository layer, no MediatR

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the solution the plan describes

- [X] T001 Create `Quorum.sln`, the .NET 10 web project `src/Quorum.App/Quorum.App.csproj`, and xUnit projects `tests/Quorum.Catalog.Tests/Quorum.Catalog.Tests.csproj`, `tests/Quorum.Sessions.Tests/Quorum.Sessions.Tests.csproj`, `tests/Quorum.Groups.Tests/Quorum.Groups.Tests.csproj`, and `tests/Quorum.App.Tests/Quorum.App.Tests.csproj`. Serves the plan's source layout: one web project and the four test projects
- [X] T002 Add NuGet references on `src/Quorum.App/Quorum.App.csproj` for Entity Framework Core and Npgsql.EntityFrameworkCore.PostgreSQL. Use the ASP.NET Core `PasswordHasher` from the shared framework. Do not add a second hashing library. Add `Microsoft.EntityFrameworkCore.Design` and `Microsoft.AspNetCore.Mvc.Testing` where the test projects need them. Serves the plan's primary dependencies
- [X] T003 [P] Add `docker-compose.yml` at the repository root running PostgreSQL 17 with database `quorum`, and add a localhost connection string in `src/Quorum.App/appsettings.Development.json`. Serves the plan's storage: one PostgreSQL 17 database for local development

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared types, the database, cookie sign-in, and logging rules that every story uses

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create `AccountId`, `GroupId`, `MemberId`, and `SessionId` in `src/Quorum.App/SharedKernel/`, plus `ContentKey` and `IClock` in `src/Quorum.App/SharedKernel/IClock.cs`. Add `SystemClock` in `src/Quorum.App/Infrastructure/Time/SystemClock.cs` returning UTC. Register it from `src/Quorum.App/Infrastructure/Time/TimeExtensions.cs`. `Program.cs` calls that extension and does not register the clock inline. Serves the plan's `SharedKernel`
- [X] T005 Create `src/Quorum.App/Infrastructure/Persistence/QuorumDbContext.cs` as the only data access type. Handlers use this context directly. No repository class. Register the context and Npgsql from `src/Quorum.App/Infrastructure/Persistence/PersistenceExtensions.cs`. `Program.cs` calls that extension. Serves the constitution rule of one database context and no repository layer
- [X] T006 Keep `src/Quorum.App/Program.cs` as calls to extension methods, then `app.Run()`. Do not put cookie options, Npgsql setup, clock registration, logging setup, `AddRazorPages`, `MapRazorPages`, or publish-command parsing in `Program.cs`. Add `src/Quorum.App/Infrastructure/Auth/AuthExtensions.cs` for cookie authentication and the auth middleware. The cookie is HttpOnly, SameSite=Lax, and Secure when the request is HTTPS. It uses a sliding expiration of 14 days and ends at sign-out of that browser only. Add `src/Quorum.App/Infrastructure/Web/WebExtensions.cs` and call it for `AddRazorPages` and `MapRazorPages`. Call the persistence and time extensions from T004 and T005. Leave the logging call for T007. Do not reference Keycloak, MediatR, or a second web project. Serves FR-004 and FR-026
- [X] T007 Add `src/Quorum.App/Infrastructure/Logging/SafeLog.cs` and register it from `src/Quorum.App/Infrastructure/Logging/LoggingExtensions.cs`. Add the call to that extension in `Program.cs` in this task. Logs may include account id, group id, session id, topic key, and command name, and must not include section markdown, question text, design markdown, email, display names, or a password. Serves the plan's privacy constraint
- [X] T008 Add the first EF Core migration under `src/Quorum.App/Infrastructure/Persistence/Migrations/` so `dotnet ef database update` creates an empty application database. Serves the plan's storage before any feature migration

**Checkpoint**: The app starts, connects to PostgreSQL, and has no feature pages yet

---

## Phase 3: User Story 1 - Open a topic's answer key (Priority: P1) 🎯 MVP

**Goal**: An account registers, creates a group, and reads the shared catalog after one publication of the senior-backend roadmap

**Independent Test**: Publish the roadmap once. Create two accounts and two groups. The first account opens one track, one topic's answer key and questions, and one worked design. The second account sees none of the first group's name or members and can open that same topic.

### Tests for User Story 1

- [X] T009 [P] [US1] Add failing parser and publish tests in `tests/Quorum.Catalog.Tests/PublishCatalogTests.cs`. A topic is any heading whose block contains `**Level:**`, including `###` under `## Good to have`. `## Good to have` itself is not a topic. The six `designs/*.md` files are worked designs, not topics. Known sections are `in-one-minute`, `when-it-matters`, `what-it-costs`, `failure`, `learn`, `review`, `interview`, and `exercise`. Review numbered items and interview numbered items, or one interview paragraph, become questions. Empty text is dropped. The same kind and text is stored once per topic. A failed publish rolls back. A second publish writes nothing and exits 2. Assert 7 tracks, 72 topics, and 6 designs by counting child rows. `CatalogPublication` has no stored track, topic, or design total
- [X] T010 [P] [US1] Add failing account and group tests in `tests/Quorum.Groups.Tests/AccountGroupTests.cs`. Serves FR-001 through FR-007 and FR-022. Email is unique ignoring case. A password shorter than 10 or longer than 128 is refused and creates no account. The stored value is a hash, not the plaintext, and two hashes of the same password differ because each carries its own salt. No salt column is written. Group name is 1 to 60 characters after trimming. Display name is 1 to 40 characters after trimming. Creating a second group does not copy members, sessions, or pick counts. A group refuses a ninth membership, counting the creator

### Implementation for User Story 1

- [X] T011 [P] [US1] Create `Account` in `src/Quorum.App/Features/Accounts/Account.cs`. Serves FR-001, FR-002, and FR-003. Email is required and unique ignoring case. Reject a password shorter than 10 or longer than 128 characters before hashing. Hash an accepted password with ASP.NET Core `PasswordHasher`. That hasher generates a unique salt per password and stores the salt inside the hash string. Do not add a salt column. Never store or log the plaintext password
- [X] T012 [P] [US1] Create `Group` and `Membership` in `src/Quorum.App/Features/Groups/Group.cs` and `src/Quorum.App/Features/Groups/Membership.cs`. Serves FR-005, FR-006, and FR-022. `Group.Name` is required, trimmed, 1 to 60 characters. `CreatorAccountId` is required and is not unique. The group stores no facilitator. Creating a group inserts the creator membership. A group has at most 8 memberships, counting the creator, and a ninth is refused. `Membership.DisplayName` is required, trimmed, 1 to 40 characters, unique per group ignoring case. `AccountId` is optional and unique inside a group when set. A roster row with a null `AccountId` belongs to one group only
- [X] T013 [P] [US1] Create catalog entities in `src/Quorum.App/Features/Catalog/CatalogEntities.cs` for `CatalogPublication`, `Track`, `Topic`, `TopicSection`, `Question`, and `WorkedDesign`. `CatalogPublication` stores `Id`, `SourceKey`, and `CompletedOn` only. `SourceKey` for this publication is `senior-backend-roadmap`. `Question.Fingerprint` is unique per topic for kind plus normalized text. `WorkedDesign.Key` comes from the file name
- [X] T014 [US1] Map those entities on `src/Quorum.App/Infrastructure/Persistence/QuorumDbContext.cs` and add a migration under `src/Quorum.App/Infrastructure/Persistence/Migrations/`
- [X] T015 [US1] Implement `src/Quorum.App/Features/Catalog/Publish/RoadmapParser.cs` and `src/Quorum.App/Features/Catalog/Publish/PublishCatalog.cs`. Register the command from `src/Quorum.App/Features/Catalog/Publish/PublishExtensions.cs`. `Program.cs` calls that extension and does not parse the command itself. There is no `ICatalogSeedSource`. `RoadmapParser` is the one seed path. A later source can add rows with its own source key. `dotnet run --project src/Quorum.App -- publish-catalog --source <dir>` commits one transaction and exits 0. A completed publication exits 2 with no writes. A bad source exits 1 and rolls back. Pages never read the roadmap directory
- [X] T016 [US1] Add register, sign-in, and sign-out pages under `src/Quorum.App/Features/Accounts/` matching `specs/001-group-catalog/contracts/pages.md`. Duplicate email says the email is already in use and does not sign anyone in. Wrong password and unknown email return the same message and stay signed out
- [X] T017 [US1] Add group create and group list pages under `src/Quorum.App/Features/Groups/`. `POST /groups` takes a group name and the account's display name in that group, creates the creator membership, and opens that group's catalog. An account that already has a group may create another. `POST /groups/{groupId}/open` refuses a group the account does not belong to
- [X] T018 [US1] Add catalog pages under `src/Quorum.App/Features/Catalog/Browse/` for `/catalog`, `/catalog/tracks/{trackKey}`, `/catalog/topics/{topicKey}`, and `/catalog/designs/{designKey}`. Serves FR-008 through FR-019. No completed publication shows the not-available state, not an empty catalog. `GET /catalog` shows `CatalogPublication.CompletedOn` as the catalog's last modification date, the day that one publication completed. Render only published sections, in the standard order. Show each question once, labeled Review or Interview. A design page has no control that puts it on a session. The owner of a group can open these pages. Do not require extra roster names before the catalog opens

**Checkpoint**: Two accounts can each open a group and read the same seeded topic. Neither sees the other's group name

---

## Phase 4: User Story 2 - Create sessions and elect a facilitator (Priority: P1)

**Goal**: The group creator sets the first session's topics. Later sessions get topics only after that session elects a facilitator. Pick counts and reviewed marks stay inside the group

**Independent Test**: Create the first session with 3 topics and see each count become 1. Replace those topics before any vote and do not count a topic twice. Create a second session with no topics. The people present at the first vote become the voters and candidates. One facilitator is elected. The signed-in member records 2 topics, and a topic already counted goes from 1 to 2. Production chooses a tied leader with equal chance. The test replaces that choice, and a later read returns that person. A later session elects someone when the previous facilitator is absent. Creating a session before publication writes nothing. A late arrival has no vote. Another group sees none of the sessions or counts.

### Tests for User Story 2

- [X] T019 [P] [US2] Add failing session tests in `tests/Quorum.Sessions.Tests/SessionRulesTests.cs`. Serves FR-029, FR-030, FR-031, FR-032, FR-033, FR-034, FR-035, FR-036, FR-041, FR-043, and FR-045. Creating a session before a completed publication exists writes nothing, and the catalog stays readable (FR-032). A session id from another group is not found and writes nothing (FR-029). Session 1 is created with 2 or 3 topics and no attendance, and each topic's pick count increases by 1. Replacing session 1 topics before a facilitator exists adds 1 only for a topic that was not already on the session and never lowers a count. A later session is created with no topics and no attendance. The first save on that later session, after its facilitator is stored, adds 1 pick for each topic, including a topic that already has a pick count (FR-038), recorded by a signed-in member of the group. A second save on that same later session adds 1 only for a topic that was not already on the session and never lowers a count (FR-036). An account that is not a member writes nothing (FR-035). A later session still elects a facilitator when the previous session's facilitator is not in the new voting set (FR-043). Attendance before the first vote must be 2 to 8 current group members or the save leaves the previous list unchanged. After the first vote, attendance may drop below 2, must not add a person who is not a current member, and must not exceed the group. The group never has more than 8 members, so attendance never exceeds 8 (FR-045). The first accepted vote copies that attendance into the voting set. A later arrival is present and is not a voter or a candidate. A voter removed from attendance keeps the vote. A second submission for the same voter leaves the first choice. A self-vote is accepted when the voting set has exactly two people and refused when it has three or more. A missing vote leaves the facilitator empty. When every voter has voted and one candidate leads, that candidate is the facilitator and further votes are refused. When every voter has voted and two or more candidates share the highest count, the test replaces the production choice with one of those leaders and a later read returns that same member (FR-034). One topic, four or more, a duplicate, a design id, or an unknown id writes nothing. A track is reviewed only when it has at least one topic and every topic has pick count at least 1. No test may use a voting timer or a scheduled meeting time

### Implementation for User Story 2

- [X] T020 [P] [US2] Create `Session`, `SessionAttendance`, `SessionVoter`, `SessionVote`, `SessionTopic`, and `TopicProgress` in `src/Quorum.App/Features/Sessions/SessionEntities.cs`. `Session.Number` starts at 1 per group. `Session.CreatedOn` is the UTC calendar date from `IClock`. `FacilitatorMembershipId` is empty until election succeeds and must be in the voting set. `TopicProgress` is unique on group plus topic. `PickCount` starts at 0 and never decreases. Reviewed is derived from `PickCount >= 1`, not a stored flag. There is no `ScheduledFor` column and no vote-deadline column
- [X] T021 [US2] Map the session entities on `src/Quorum.App/Infrastructure/Persistence/QuorumDbContext.cs` and add a migration under `src/Quorum.App/Infrastructure/Persistence/Migrations/`
- [X] T022 [US2] Implement create-session and topic-save commands in `src/Quorum.App/Features/Sessions/SessionCommands.cs`. Serves FR-030, FR-031, FR-032, FR-033, FR-035, and FR-036. If no completed publication exists, create-session writes nothing and the catalog stays readable. A session id that belongs to another group is not found and writes nothing. The group creator creates session 1 with 2 or 3 distinct topics. A member account of the group creates each later session with no topics. Several sessions may share a calendar date. Only the creator may replace session 1 topics, and only before a facilitator is stored. After election, session 1 topics stay. A later session accepts 2 or 3 topics only after its facilitator is stored, recorded by a signed-in member of that group. The elected facilitator may be a roster name. The first save on a later session adds 1 pick for every included topic. A replacement on that same session adds 1 only for a topic newly added. An account that is not a member of the group cannot write the session
- [X] T023 [US2] Implement attendance, votes, and election in `src/Quorum.App/Features/Sessions/Election.cs`. Serves FR-034, FR-043, and FR-045. Before the first vote, attendance save succeeds only for 2 to 8 current members of the group. After the first vote, attendance may drop below 2. It must not add a non-member and must not exceed the group's memberships. The group never has more than 8 members, so attendance never exceeds 8. The first accepted vote copies attendance into `SessionVoter` and then stores that vote. Later attendance changes do not add or remove voters, candidates, or votes. Voter and candidate must both be in the voting set. One vote per voter. A second submission for that voter is refused. In a set of two, the candidate may be the voter. In a larger set, a self-vote is refused. When every voter has voted and one candidate has strictly more votes than every other candidate, store that facilitator and lock votes. When every voter has voted and the highest count is shared, production chooses one of those leaders with equal probability, stores that member, and locks votes. Tests replace that choice with one of the leaders so the stored person is repeatable. Until every voter has voted, the facilitator stays empty. A later session still elects from its own voting set when the previous facilitator is absent. Do not start a timer
- [X] T024 [US2] Add session pages under `src/Quorum.App/Features/Sessions/` for `GET /sessions`, `POST /sessions`, `POST /sessions/{sessionId}/attendance`, `POST /sessions/{sessionId}/topics`, and `POST /sessions/{sessionId}/votes`, matching `specs/001-group-catalog/contracts/pages.md`. Serves FR-029 through FR-036 and FR-045. A session id from another group is not found. No publication leaves sessions unavailable and the catalog link working. The chooser lists the open group's never-picked topics first, then picked topics, keeping published order inside each of those groups. Show the open group's pick count and reviewed mark only

**Checkpoint**: One group can create two sessions, elect a facilitator, and see pick counts. A second group still shows count 0 for the same topic

---

## Phase 5: User Story 3 - Return to the same group (Priority: P2)

**Goal**: The same account comes back to the same groups, members, catalog, sessions, and pick counts. A wrong password opens nothing. The catalog remains when the roadmap directory is gone

**Independent Test**: Sign out, fail one sign-in with a wrong password, sign in correctly, and confirm the same group, the catalog's last modification date, topic, and members. Remove or hide the roadmap directory and confirm the same topic still opens.

### Tests for User Story 3

- [X] T025 [P] [US3] Add failing return-visit tests in `tests/Quorum.App.Tests/ReturnVisitTests.cs` using the web application factory. Serves FR-010, FR-011, FR-024, FR-025, FR-026, and FR-029. Signing out clears that browser's cookie and shows no catalog, member, session, or answer-key text. A direct request while signed out redirects to sign-in and shows none of that text. A direct request for another group's session id is not found. A wrong password and an unknown email return the same message and stay signed out. Signing in again shows the same memberships, the catalog's last modification date, sessions, pick counts, and reviewed marks. Two browsers signed in to the same account are one membership and, once voting has opened, one voter. Signing out of one browser leaves the other signed in. Browse pages do not read the roadmap directory

### Implementation for User Story 3

- [X] T026 [US3] Finish sign-out and the generic sign-in failure in `src/Quorum.App/Features/Accounts/`. Serves FR-025 and FR-026. Cookie options stay in `src/Quorum.App/Infrastructure/Auth/AuthExtensions.cs`, as wired in T006. Do not add cookie configuration to `Program.cs`. Sign-out deletes only the cookie for the browser that posted it. Never log the password
- [X] T027 [US3] Guard catalog and session reads in `src/Quorum.App/Features/Catalog/Browse/` and `src/Quorum.App/Features/Sessions/` so a signed-out request redirects to sign-in before any group or catalog text is rendered. Confirm `PublishCatalog` is the only type that accepts a roadmap path

**Checkpoint**: A signed-out visitor sees the sign-in page. The same account sees the same rows after signing in again, with the roadmap directory unavailable

---

## Phase 6: User Story 4 - Record member display names (Priority: P3)

**Goal**: The account that owns the group keeps a roster of display names. Those people do not sign in. The owner can read the catalog. The group never has more than 8 members, counting the owner

**Independent Test**: In a group whose catalog is already published, add two members, reject a duplicate name, reject a ninth member, rename one, remove one, and confirm a topic still opens for the owner.

### Tests for User Story 4

- [X] T028 [P] [US4] Add failing roster tests in `tests/Quorum.Groups.Tests/RosterTests.cs`. Serves FR-020, FR-021, and FR-022. A display name of only spaces, or longer than 40 characters, is refused. Uniqueness ignores case and surrounding spaces. The same name may be added again after removal. Removing a member takes them off the roster and does not change session topic lists or pick counts. A session where they were already marked present, or where they were already a voter, still lists them. They cannot be marked present on a later session. The creator membership may be renamed and may not be removed. The owner can open a published topic. A group that already has 8 memberships, counting the creator, refuses a ninth name. A member id from another group is not found

### Implementation for User Story 4

- [X] T029 [US4] Implement add, rename, and remove in `src/Quorum.App/Features/Groups/RosterCommands.cs` with the rules in T028
- [X] T030 [US4] Add the member pages under `src/Quorum.App/Features/Groups/` for `GET /members`, `POST /members`, `POST /members/{memberId}/rename`, and `POST /members/{memberId}/remove`, matching `specs/001-group-catalog/contracts/pages.md`. There is no action that attaches an existing account to a group that account did not create, and no member sign-in

**Checkpoint**: The roster changes without changing the catalog text or another group's names

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Confirm the stories work together and the deferred scope stayed out

- [X] T031 [P] Add the shared Razor UI layout at `src/Quorum.App/Features/Shared/_Layout.cshtml`. This is the HTML shell the catalog, session, and member pages share. It is not the backend folder layout in `plan.md`. Those feature folders stay where the plan puts commands and pages. The layout keeps those pages readable when the browser window is narrow. Do not add a screen-reader standard beyond that. Serves the spec assumption that pages stay readable in a narrow browser window
- [X] T032 Run the quickstart checks in `specs/001-group-catalog/quickstart.md` with `dotnet test` and one manual `publish-catalog` against a local roadmap checkout. Serves the quickstart acceptance check. Confirm the publication row has no stored totals, the catalog page shows `CompletedOn` as the last modification date, a session cannot be created before that publication, a group cannot exceed 8 members, a password outside 10 to 128 is refused, and the password hash is not plaintext. Confirm that no task added a voting timer, a `ScheduledFor` column, Telegram, or the 45-minute runner

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup. Blocks every user story
- **User Story 1 (Phase 3)**: Depends on Foundational. No dependency on later stories
- **User Story 2 (Phase 4)**: Depends on User Story 1, because a session needs a group, a creator membership, and published topics
- **User Story 3 (Phase 5)**: Depends on User Story 1. The check that sessions survive sign-in also depends on User Story 2
- **User Story 4 (Phase 6)**: Depends on User Story 1. The removal rules that mention a session also depend on User Story 2
- **Polish (Phase 7)**: Depends on the stories being present

### User Story Dependencies

- **User Story 1 (P1)**: First shippable slice. Catalog and accounts
- **User Story 2 (P1)**: Sessions, election, and pick counts. Requires the catalog
- **User Story 3 (P2)**: Return visit and the second browser. Requires accounts. Full session return requires User Story 2
- **User Story 4 (P3)**: Roster pages. Requires the group. Session-linked removal requires User Story 2

### Within Each User Story

- Tests are written and fail before that story's implementation
- Entities before commands
- Commands before Razor pages
- A story is independently testable at its checkpoint

### Parallel Opportunities

- T003 can run alongside T001 and T002
- T009 and T010 can run together
- T011, T012, and T013 can run together
- T019 can be written while User Story 1 implementation is still in progress, after Foundational is done
- T025 and T028 can be written in parallel after their story prerequisites exist
- T031 can run beside the last story pages

---

## Parallel Example: User Story 1

```bash
# Tests together, after Foundational:
Task: "T009 PublishCatalogTests in tests/Quorum.Catalog.Tests/PublishCatalogTests.cs"
Task: "T010 AccountGroupTests in tests/Quorum.Groups.Tests/AccountGroupTests.cs"

# Entities together:
Task: "T011 Account in src/Quorum.App/Features/Accounts/Account.cs"
Task: "T012 Group and Membership in src/Quorum.App/Features/Groups/"
Task: "T013 Catalog entities in src/Quorum.App/Features/Catalog/CatalogEntities.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2
2. Complete Phase 3
3. Stop and run the catalog publish tests and the two-account catalog check
4. Demo: register, create a group, publish once, open a topic

### Incremental Delivery

1. Setup and Foundational
2. User Story 1: the seeded catalog
3. User Story 2: sessions, election, and pick counts
4. User Story 3: return visit and two browsers
5. User Story 4: roster pages
6. Polish: quickstart and the narrow window

### Parallel Team Strategy

After Foundational:

- One person takes User Story 1
- After the catalog entities exist, another person can write the failing session tests for User Story 2
- User Story 4 page work waits until group membership exists

---

## Notes

- A voting time limit and a session schedule belong to a later feature. Do not add them here
- The same account on two browsers is one membership and one voter
- "Account" is the person who signs in. "Facilitator" is only the member elected on a session. The group stores no facilitator
- A group never has more than 8 members, counting the owner. Attendance cannot exceed that group
- Setup tasks T001–T008 and polish tasks T031–T032 name the plan section or requirement they serve. T031 is the shared Razor UI layout, not the backend folder layout
- `Program.cs` calls extension methods and ends with `app.Run()`. `AddRazorPages` and `MapRazorPages` live in `Infrastructure/Web/WebExtensions.cs`. The logging call is added when T007 creates `LoggingExtensions`
- Commit after each task or logical group
- Stop at any checkpoint and test that story alone

## Phase 8: Convergence

- [X] T033 On GET `/sessions`, link each topic title to `/catalog/topics/{topicKey}` in `src/Quorum.App/Features/Sessions/SessionIndex.cshtml` per contracts/pages.md (partial)
