---
description: "Task list for transparent local operations"
---

# Tasks: Transparent local operations

**Input**: Design documents from `/specs/002-observability-local-deploy/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. `quickstart.md` makes `dotnet test tests/Quorum.App.Tests/Quorum.App.Tests.csproj` the acceptance check. The collector in those tests is a double, not a live Grafana. Write the tests for a story first and confirm they fail before the implementation tasks for that story.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested on its own.

**Out of scope for every task**: a second database, a repository interface, MediatR or another mediator, a phone or email or chat alert, a member-facing rank or streak or score board, sampling or dropping one of eight same-minute attempts, a password or basic-auth or tenant header on Loki or Tempo, a page of activity records on the group's address, and a public certificate vendor. Handlers keep using `QuorumDbContext`. `Program.cs` stays extension calls, then `app.Run()`. Do not add `OpenTelemetry.Instrumentation.EntityFrameworkCore` or `OpenTelemetry.Instrumentation.Runtime`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel when the tasks use different files and do not depend on an unfinished task. T028 through T034 are the exception: their `[P]` applies only to the wave that starts after T027. T027 is not in that wave
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5)
- Setup, foundational, and polish tasks have no story label

## Path Conventions

- Web app: `src/Quorum.App/`
- Tests: `tests/Quorum.App.Tests/`
- One ASP.NET Core process, Razor Pages under `Features/`, one `QuorumDbContext`, no repository layer, no MediatR
- Operator guide: `docs/run.md`
- Self-contained stack: `docker-compose.yml` and `deploy/`
- Debug stack: `docker-compose.debug.yml`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the telemetry packages and the app image. The solution, the web project, and PostgreSQL 17 already exist.

- [X] T001 [P] Add NuGet references on `src/Quorum.App/Quorum.App.csproj` for `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, and `OpenTelemetry.Instrumentation.Http`. Use a published stable version that supports `net10.0`. Do not add `OpenTelemetry.Instrumentation.EntityFrameworkCore` or `OpenTelemetry.Instrumentation.Runtime`. Do not add a second database client
- [X] T002 [P] Add `src/Quorum.App/Dockerfile`. Multi-stage build from `mcr.microsoft.com/dotnet/sdk:10.0` and runtime `mcr.microsoft.com/dotnet/aspnet:10.0`. Do not use the `latest` tag. Publish `Quorum.App`. Set `ASPNETCORE_URLS=http://+:8080`. The entrypoint is `dotnet Quorum.App.dll` so `docker compose exec quorum dotnet Quorum.App.dll activity <reference>` works. The image contains no Grafana, Loki, Prometheus, Tempo, or collector binary

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The activity row, the storage-wait duration, the short reference, and the only export client. Every story writes or reads these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 [P] Create `ActivityRecord` in `src/Quorum.App/Infrastructure/Logging/ActivityRecord.cs`. Table name `activity_records`. Fields, quoted from the data model: `Id` uuid required, primary key. `Reference` text required, eight characters from `23456789ABCDEFGHJKMNPQRSTVWXYZ` (none of `0`, `O`, `1`, `I`, `L`, or `U`), one per submission. `Attempt` text required, one of `register`, `sign-in`, `sign-out`, `create-group`, `add-member`, `rename-member`, `remove-member`, `publish-catalog`, `create-session`, `set-topics`, `save-attendance`, `cast-vote`, `elect-facilitator`, `application-start`, `application-stop`, `health-ready`, `health-not-ready`. `Outcome` text required, `succeeded` or `refused`. `Reason` text optional, only `unknown-account` on a sign-in where no account matches, absent for every other row, never an email. `AcceptedAt` timestamptz required. `DurationMs` integer required, zero or positive, the whole attempt. `StorageWaitMs` integer required, zero or positive, the part of `DurationMs` spent waiting on stored data, zero when the attempt did not touch the database. `AccountId` uuid optional, not an email. `GroupId` uuid optional. `SessionId` uuid optional. `MemberId` uuid optional, not a display name. `Tracks` integer optional, set only on a succeeded `publish-catalog`. `Topics` integer optional, set only on a succeeded `publish-catalog`. `Designs` integer optional, set only on a succeeded `publish-catalog`. `ExportedAt` timestamptz optional, null until the collector has accepted the export. Forbidden in every column: password, email, display name, answer text, rubric note, score, session cookie, catalog page body. Do not add a foreign key. `AccountId`, `GroupId`, `SessionId`, and `MemberId` are copies of existing ids
- [X] T004 [P] Create `src/Quorum.App/Infrastructure/Logging/ActivityReference.cs`. Generate eight characters from the alphabet `23456789ABCDEFGHJKMNPQRSTVWXYZ`, so a reference contains none of `0`, `O`, `1`, `I`, `L`, or `U`. This is the read-aloud alphabet, not standard Crockford base32. Before the first insert of a new submission, treat any existing `activity_records.Reference` as a collision and generate again, even when the attempt name would differ. A `cast-vote` row and an `elect-facilitator` row from the same submission reuse the reference the first insert already stored. The reference contains no email, password, or answer text
- [X] T005 [P] Create `src/Quorum.App/Infrastructure/Logging/StorageWait.cs` and `src/Quorum.App/Infrastructure/Logging/StorageWaitInterceptor.cs`. `StorageWait` is an `AsyncLocal` total for the current async flow. The interceptor implements `DbCommandInterceptor`, times each command, and adds that elapsed time to the total. It does not read, log, or export `CommandText`. Do not add the OpenTelemetry Entity Framework package
- [X] T006 [P] Create `src/Quorum.App/Infrastructure/Logging/TelemetryExtensions.cs` with `AddQuorumTelemetry`. Configure the OpenTelemetry SDK added in T001. Resource attribute `service.name` is `quorum`. Export OTLP/HTTP only to the configured `Telemetry:OtlpEndpoint`, with basic auth from `Telemetry:CollectorUser` and `Telemetry:CollectorPassword`. Do not register an exporter aimed at Loki, Prometheus, Tempo, or Grafana. Do not add a fallback endpoint. `IncludeFormattedMessage` is false. ASP.NET Core instrumentation may add the route path and must not add form fields, query strings, request bodies, cookies, or emails. Do not enable a sampler. Do not call this extension from a request path that waits on the collector. Leave the `Program.cs` call for T011
- [X] T007 Map `ActivityRecord` in `src/Quorum.App/Infrastructure/Logging/ActivityConfiguration.cs` and add `DbSet<ActivityRecord> ActivityRecords` on `src/Quorum.App/Infrastructure/Persistence/QuorumDbContext.cs`. Configure, and enforce in the model: `Outcome` is only `succeeded` or `refused`. `Reason` is only null or `unknown-account`. `DurationMs` and `StorageWaitMs` are `>= 0`. `StorageWaitMs` is `<= DurationMs`. `Tracks`, `Topics`, and `Designs` are null unless `Attempt` is `publish-catalog` and `Outcome` is `succeeded`. Unique index on `Reference` plus `Attempt`. That index lets `cast-vote` and `elect-facilitator` share a reference. One reference per submission is the T004 collision check, which rejects a reference that already exists on any row before a new submission's first insert. Index on `AcceptedAt`. Filtered index on `ExportedAt` where null. No foreign keys
- [X] T008 Add an EF Core migration under `src/Quorum.App/Infrastructure/Persistence/Migrations/` that creates `activity_records` with the columns, checks, and indexes from T007. The migration does not create a second database and does not alter group, session, member, vote, or catalog tables except for the new table sharing that database
- [X] T009 Register `StorageWaitInterceptor` from `src/Quorum.App/Infrastructure/Persistence/PersistenceExtensions.cs` and from `QuorumDbContext.Create`, and register it in `tests/Support/TestDoubles.cs` where `QuorumLab.Open` builds the context. In `PersistenceExtensions`, add a startup migrate that calls `Database.MigrateAsync` and, if PostgreSQL is unreachable, logs a safe failure without the connection string or password and lets the process continue. A missing `Quorum` connection string still fails at host build. Do not require a successful connection during host build. `Program.cs` only gains the extension call in T011
- [X] T010 Extend `src/Quorum.App/Infrastructure/Logging/SafeLog.cs` and add `src/Quorum.App/Infrastructure/Logging/ActivitySubmission.cs`. Keep `SafeLog` a singleton. Hold one short reference, one stopwatch, and the storage-wait total in an `AsyncLocal` submission so concurrent requests do not share a reference. `Begin` starts a submission when the current flow has none. `Write` inserts one `activity_records` row through `IServiceScopeFactory` and `QuorumDbContext` after the attempt's outcome is final, in its own `SaveChanges`, and returns without exporting. `Finish` does not insert. `CurrentReference` stays readable after `Finish` until the next `Begin` on that flow. `DurationMs` is the stopwatch. `StorageWaitMs` is the interceptor total, clamped so `StorageWaitMs <= DurationMs`. `ExportedAt` stays null. Reject an attempt name outside the T003 list, an outcome other than `succeeded` or `refused`, a reason other than null or `unknown-account`, and counts set when the attempt is not a succeeded `publish-catalog`. Update the `SafeLog` constructor and `tests/Support/TestDoubles.cs` so `QuorumLab.Open` still builds. Do not add a repository class
- [X] T011 Call `AddQuorumTelemetry` and the startup migrate from T009 in `src/Quorum.App/Program.cs` as extension methods beside the existing calls. Do not put OpenTelemetry options, Npgsql options, or activity-row inserts in `Program.cs`. Do not add `RunActivityCommandIfPresent` in this task

**Checkpoint**: The app still starts against PostgreSQL, `activity_records` exists, and a down database does not crash the process during migrate

---

## Phase 3: User Story 1 - See that Quorum is ready (Priority: P1) 🎯 MVP

**Goal**: An anonymous status page says ready only when stored data is reachable, says local use only while a documented local-only setting remains, and a self-contained start brings the app, PostgreSQL, and the tool set up together. A second self-contained start is refused and does not erase data.

**Independent Test**: Start a copy and read `/status` without an account. Stop stored data and see not ready within 30 seconds, naming stored data. Start stored data again and see ready, with one activity row for each change and none for an unchanged probe. Start a second self-contained copy while the first is running and see the refusal, with the first copy's data still present.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T012 [P] [US1] Add failing status tests in `tests/Quorum.App.Tests/StatusTests.cs` using `WebApplicationFactory`. Serves FR-006, FR-007, FR-008, FR-009, FR-010, and FR-022. `GET /status` with no cookie returns 200 and does not redirect to sign-in. The body contains `ready` or `not ready`, and contains none of a group name, member name, display name, email, answer text, rubric note, score, password, session cookie, catalog page text, or a Grafana URL. When the database password in the `Quorum` connection string is still `quorum`, the use line is `local use only` and the body does not say `ready for a group`. When the database password, `Telemetry:CollectorPassword`, `Telemetry:GrafanaAdminPassword`, and `Telemetry:PrometheusPassword` are all different from `quorum`, and `PublicBaseUrl` has a host other than `localhost` or `127.0.0.1`, and the probe has succeeded, the use line is `ready for a group`. A failed probe leaves the process running and the page says `not ready`, stored data `down`, and does not say `ready for a group`. One transition writes one `health-ready` or `health-not-ready` row. A repeated probe that does not change readiness writes nothing. Assert the production probe interval is 5 seconds and the probe timeout is 2 seconds by reading the monitor's defaults. Call the probe method from the test so the test does not sleep 30 seconds. Stopping the collector double does not by itself make the page say `not ready`

### Implementation for User Story 1

- [X] T013 [P] [US1] Implement `src/Quorum.App/Infrastructure/Logging/LocalOnly.cs`. Compare, and never render or log, these configured values to the printed defaults: database password `quorum` (the `Password` field of the `Quorum` connection string), collector password `quorum` (`Telemetry:CollectorPassword`), Grafana admin password `quorum` (`Telemetry:GrafanaAdminPassword`), Prometheus password `quorum` (`Telemetry:PrometheusPassword`), and `PublicBaseUrl` host `localhost` or `127.0.0.1`. A missing public host counts as local-only. Loki and Tempo have no password field and are not part of this check. The copy is ready for a group only when none of those settings still match
- [X] T014 [US1] Implement `src/Quorum.App/Infrastructure/Logging/HealthMonitor.cs` and register it from `src/Quorum.App/Infrastructure/Logging/LoggingExtensions.cs`. Probe `SELECT 1` every 5 seconds with a 2-second timeout. Keep the latest result in memory, not in a table. Readiness is `ready` only when the latest probe reached PostgreSQL. Before the first successful probe, readiness is not ready and stored data is down. A change between ready and not ready writes one `health-ready` or `health-not-ready` row through the T010 writer. An unchanged probe writes nothing. `application-start` is written once per process start. `application-stop` is written on a clean stop and is best-effort: a kill that cannot write leaves the earlier rows and does not block shutdown on the collector. The snapshot includes no group name, member name, email, answer, score, password, or catalog text. Expose the single probe method the T012 tests call. Store readiness as `ready` or `not-ready`, stored data as `reachable` or `down`, and use as `local-use-only` or `ready-for-a-group`. A down collector does not change readiness
- [X] T015 [US1] Add `src/Quorum.App/Features/Status/Status.cshtml` and `src/Quorum.App/Features/Status/Status.cshtml.cs` for anonymous `GET /status`. Do not inherit `SignedInPageModel`. Apply `[AllowAnonymous]`. The page reads the in-memory snapshot and does not query groups or activity. Show only these lines: readiness `ready` or `not ready`; stored data `reachable` or `down`; use `local use only` or `ready for a group`; checked at, the UTC timestamp of the latest finished probe. Render in-memory `not-ready` as `not ready`, `local-use-only` as `local use only`, and `ready-for-a-group` as `ready for a group`. `ready` appears only when stored data is reachable. `ready for a group` appears only when readiness is ready and `LocalOnly` says every documented setting was replaced. Otherwise the use line is `local use only`. The body contains none of: group name, member name, display name, email, answer text, rubric note, score, password, session cookie, catalog page text, Grafana URL. Do not link to Grafana, Loki, Prometheus, Tempo, or the collector
- [X] T016 [P] [US1] Add `deploy/local.env` with the printed defaults the guide will tell the operator to replace: `POSTGRES_PASSWORD=quorum`, `COLLECTOR_PASSWORD=quorum`, `GRAFANA_ADMIN_PASSWORD=quorum`, `PROMETHEUS_PASSWORD=quorum`, `PUBLIC_BASE_URL=http://localhost:8080`. Do not add a Loki password or a Tempo password
- [X] T017 [P] [US1] Add `deploy/loki.yaml`. Loki starts with auth disabled and no password. Retention is 336 hours. The in-network listen address is the Loki container port 3100. Do not add basic auth, a tenant gateway, or an nginx sidecar
- [X] T018 [P] [US1] Add `deploy/tempo.yaml`. Tempo starts with no password. OTLP listen is port 4317 inside the Docker network. Block retention is 336 hours. Do not add basic auth or a tenant gateway
- [X] T019 [P] [US1] Add `deploy/prometheus.yml`. Prometheus will be started with `--web.enable-remote-write-receiver`, `--storage.tsdb.retention.time=14d`, and a basic-auth `web.yml` generated at start from `PROMETHEUS_PASSWORD`. Do not put the password in this committed file. Remote write is the only metrics ingest. There is no scrape of Quorum that bypasses the collector
- [X] T020 [P] [US1] Add Grafana provisioning under `deploy/grafana/provisioning/` that turns anonymous access off and sets the admin password from `GRAFANA_ADMIN_PASSWORD`. Provision datasources for Loki with no password, Prometheus with the Prometheus basic auth, and Tempo with no password. Leave the Quorum dashboard panels for T036. Do not put a Quorum member sign-in on Grafana
- [X] T021 [P] [US1] Add `deploy/otel-collector.yaml` for image `otel/opentelemetry-collector-contrib`. The OTLP/HTTP receiver listens on 4318 and requires basic auth in htpasswd form, not plaintext. `deploy/up.sh` generates that file at start from `COLLECTOR_PASSWORD`. A receiver with no credential configured is not done. Logs, metrics, and traces pipelines exist so the container stays up. The attribute allowlist and the Loki, Prometheus, and Tempo exporters are finished in T036. Do not give Loki or Tempo a password in this file
- [X] T022 [US1] Replace the postgres-only `docker-compose.yml` with the self-contained stack: services `quorum`, `postgres`, `otel-collector`, `loki`, `prometheus`, `tempo`, and `grafana`. Keep the PostgreSQL service name `postgres`, image `postgres:17`, database `quorum`, user `quorum`, and volume name `quorum-postgres`. The app builds from `src/Quorum.App/Dockerfile`, container name `quorum`, and publishes port 8080 for the group. Tool ports and PostgreSQL publish on `127.0.0.1` only: collector `4318`, Grafana `3000`, Loki `3100`, Prometheus `9090`, Tempo `3200`, PostgreSQL `5432`. Inside the network Quorum's `Telemetry:OtlpEndpoint` is `http://otel-collector:4318`. Also pass `Telemetry:CollectorUser` `quorum`, `Telemetry:CollectorPassword`, `Telemetry:GrafanaAdminPassword`, `Telemetry:PrometheusPassword`, `PublicBaseUrl`, and connection string `Host=postgres;Port=5432;Database=quorum;Username=quorum;Password` from `deploy/local.env`. The collector reaches Loki, Prometheus, and Tempo by those service names. While writing this file, confirm a published tag for `grafana/grafana`, `grafana/loki`, `grafana/tempo`, `prom/prometheus`, and `otel/opentelemetry-collector-contrib`, and pin that tag. Do not use `latest`. Do not invent a tag. Do not add an image tag to `specs/002-observability-local-deploy/spec.md`. Publishing tool ports on `127.0.0.1` is the local default. Do not publish the tools on `0.0.0.0`
- [X] T023 [US1] Add `deploy/up.sh` and ignore `deploy/.run/` in `.gitignore`. The script runs `docker compose -p quorum -f docker-compose.yml` with `deploy/local.env`. It generates collector htpasswd and Prometheus `web.yml` under `deploy/.run/` and does not commit those files. If the `quorum` service is already running, it prints exactly `A self-contained deployment is already running. Refusing to start a second one. Stored data was not erased.` and exits non-zero. It does not run `docker compose down`, does not pass `-v`, and does not remove volumes. A debug compose project is not that running deployment

**Checkpoint**: `GET /status` is anonymous. Defaults say `local use only`. A second `deploy/up.sh` refuses and leaves the volume in place. User attempts are not recorded yet

---

## Phase 4: User Story 2 - Explain one attempt without private practice content (Priority: P1)

**Goal**: Each finished attempt writes one activity row. A refusal page shows that row's reference. The collector forwards the allowlisted fields to Loki, Prometheus, and Tempo, and Grafana is the screen that filters them. The request path does not wait on the collector.

**Independent Test**: Register, refuse a wrong password, refuse an unknown email, create a group, add, rename, and remove a member, publish the catalog, create a session, set topics, save attendance, and store a facilitator by votes. Each attempt has outcome and duration. The wrong-password page shows the same reference as its row. No row contains the password, the unknown email, an answer sentence, a rubric note, or a score. The vote that elects a facilitator shares one reference across `cast-vote` and `elect-facilitator`. Quorum pages do not link to the tools.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T024 [P] [US2] Add failing redaction tests in `tests/Quorum.App.Tests/ActivityRedactionTests.cs` using `QuorumLab` and PostgreSQL. Serves FR-011 through FR-016, FR-023, and FR-027. Register writes one `succeeded` `register` row with `AccountId` and no password. A wrong password writes one `refused` `sign-in` row with `AccountId`, `Reason` null, and no password. An unknown email writes one `refused` `sign-in` row with `Reason` exactly `unknown-account` and does not store the typed email or the password. Blank sign-in input is `refused` with `Reason` null and no email. Create-group, add-member, rename-member, and remove-member each write one `succeeded` row with `GroupId` and, when a member exists, `MemberId`, and with no display name and no email. A succeeded `publish-catalog` stores `Tracks`, `Topics`, and `Designs` only. A failed publication stores one `refused` row and null counts, and the row has no catalog page body. Eight distinct succeeded attempts in the same minute produce eight rows with eight references. The test does not call Grafana. Assert the command returns while `ExportedAt` is still null. Do not add a wall-clock timing assertion for the one-second bound. That bound is met because export is not on this path
- [X] T025 [P] [US2] Add failing election and reference tests in `tests/Quorum.App.Tests/ElectionActivityTests.cs`. Serves FR-011, FR-013, FR-016, and FR-017. A vote that stores a facilitator writes `cast-vote` and `elect-facilitator` with one reference, and a lookup by that reference returns those two rows and no other submission. A self-vote in a session of three or more writes one `refused` `cast-vote` row, writes no `succeeded` row, and the session page shows that same reference. The existing sentence `The email or password is not valid.` still appears for both unknown email and wrong password. The reference alphabet is `23456789ABCDEFGHJKMNPQRSTVWXYZ`. The page does not show another group's data or a stack dump. `set-topics` is the stored attempt name for saving topics
- [X] T026 [P] [US2] Add failing link and export-shape tests in `tests/Quorum.App.Tests/NoToolLinkTests.cs`. Serves FR-019 and FR-031. Render the shared layout and the account, group, session, catalog, and status pages. No response contains a link or URL to Grafana, Loki, Prometheus, Tempo, or the collector. A collector double that records the export sees only these attributes: `quorum.attempt`, `quorum.outcome`, `quorum.reason`, `quorum.reference`, `quorum.duration_ms`, `quorum.storage_wait_ms`, `quorum.account_id`, `quorum.group_id`, `quorum.session_id`, `quorum.member_id`, `quorum.tracks`, `quorum.topics`, `quorum.designs`, and `service.name`. Metric `quorum.attempts` has labels `attempt` and `outcome` only. Histograms are `quorum.attempt.duration` and `quorum.storage.wait`. One span per submission is named `quorum.attempt` and carries `quorum.reference`. The vote and the election from one submission share that span. The export URL is `Telemetry:OtlpEndpoint` and no request targets Loki, Prometheus, Tempo, or Grafana

### Implementation for User Story 2

- [X] T027 [US2] Add `src/Quorum.App/Features/Shared/_Reference.cshtml`. It prints the current submission reference and nothing else. It does not print a password, email, display name, answer, rubric note, score, or catalog body. Finish this before T028 through T034
- [X] T028 [P] [US2] Starts after T027. Record register and sign-in from `src/Quorum.App/Features/Accounts/AccountCommands.cs`, including the early returns for invalid registration, duplicate email, blank sign-in, unknown email, and wrong password. Unknown email uses `Reason` `unknown-account` and does not pass the typed email. Wrong password passes `AccountId` and does not pass the password. Show the reference from `src/Quorum.App/Features/Accounts/Register.cshtml`, `Register.cshtml.cs`, `SignIn.cshtml`, and `SignIn.cshtml.cs` through the T027 partial. Keep `AccountCommands.SignInFailureMessage` as `The email or password is not valid.` for both unknown email and wrong password. Keep the existing duplicate-email and invalid-registration sentences. One finished attempt writes one row. Do not export on this path
- [X] T029 [P] [US2] Starts after T027. Record `sign-out` from `src/Quorum.App/Features/Accounts/SignOut.cshtml.cs` with `AccountId` and no email. One succeeded row per sign-out
- [X] T030 [P] [US2] Starts after T027. Record `create-group` from `src/Quorum.App/Features/Groups/GroupCommands.cs` for both the refused and the succeeded path. Show the reference on `src/Quorum.App/Features/Groups/GroupList.cshtml` and `GroupList.cshtml.cs` when the create is refused. The row has `GroupId` on success, and no group name, display name, email, or password
- [X] T031 [P] [US2] Starts after T027. Record `add-member`, `rename-member`, and `remove-member` from `src/Quorum.App/Features/Groups/RosterCommands.cs`, including refusals that return before today's log call. Show the reference on `src/Quorum.App/Features/Groups/Members.cshtml` and `Members.cshtml.cs`. Store `GroupId` and `MemberId` when those ids exist. Do not store the display name
- [X] T032 [P] [US2] Starts after T027. Record `create-session` and `set-topics` from `src/Quorum.App/Features/Sessions/SessionCommands.cs`. The stored `Attempt` for saving topics is `set-topics`, including where today's log line says `save-topics`. Show the reference on `src/Quorum.App/Features/Sessions/SessionIndex.cshtml` and `SessionIndex.cshtml.cs` when the change is refused. Do not store topic titles, question text, or design text
- [X] T033 [P] [US2] Starts after T027. Record attendance and votes from `src/Quorum.App/Features/Sessions/Election.cs`. `save-attendance` is one row. `Begin` once for a vote. A refused vote, including a self-vote when the voting set has three or more people, writes one `refused` `cast-vote` and no `succeeded` row. A vote that does not elect writes one `cast-vote`. A vote that stores a facilitator writes `cast-vote` and `elect-facilitator` with that same reference and then `Finish` once. Show the reference on `src/Quorum.App/Features/Sessions/SessionDetail.cshtml` and `SessionDetail.cshtml.cs`. Do not store a score, an answer, or a rubric note
- [X] T034 [P] [US2] Starts after T027. Record `publish-catalog` from `src/Quorum.App/Features/Catalog/Publish/PublishCatalog.cs` on success, on a second publication, and on a failed source. Success stores `Tracks`, `Topics`, and `Designs` and no page body. Failure stores null counts. Keep the existing console sentences and add the reference to that console output. Do not print roadmap markdown
- [X] T035 [P] [US2] Add `src/Quorum.App/Infrastructure/Logging/IActivityExport.cs` and `src/Quorum.App/Infrastructure/Logging/ActivityExporter.cs`. Register the exporter as a hosted service from `src/Quorum.App/Infrastructure/Logging/LoggingExtensions.cs`. The request path does not call it. The service sends rows whose `ExportedAt` is null to `Telemetry:OtlpEndpoint` only, with the collector basic-auth header, and sets `ExportedAt` only after the collector accepts. On failure it leaves `ExportedAt` null and does not change the row's outcome. Rows that share a reference export as one `quorum.attempt` span. Each row still increments `quorum.attempts` and the two histograms. Attributes are the allowlist in T026. Tests replace `IActivityExport` with a double. A wrong collector credential refuses the send and the attempt still finishes
- [X] T036 [US2] Finish `deploy/otel-collector.yaml` so the collector forwards logs to `http://loki:3100/otlp` with no password, metrics by Prometheus remote write to `prometheus:9090` with Prometheus basic auth, and traces by OTLP to `tempo:4317` with no password. An attributes processor keeps only the T026 allowlist and drops every other attribute, including request bodies, SQL text, emails, and cookies. Add `deploy/grafana/provisioning/dashboards/dashboard.yml` and `deploy/grafana/provisioning/dashboards/quorum.json`. The dashboard title is `Quorum`. It shows succeeded and refused counts for the last hour from `quorum.attempts`, Loki rows filtered by reference, refused outcome, group id, session id, and time, and the Tempo trace for that reference including `quorum.storage_wait_ms`. Anonymous Grafana access stays off. Do not add a password to Loki or Tempo

**Checkpoint**: A refused sign-in shows a reference that matches one stored row. Export is not on the request path. The Quorum dashboard is provisioned. The session still has no machine command for a down collector

---

## Phase 5: User Story 3 - Keep the session independent of the inspection view (Priority: P2)

**Goal**: Sign-in and group changes finish while the collector is down. The row stays in PostgreSQL. The guide's command prints it. When the collector accepts again, that same row is sent and then shows in Grafana. Rows older than 14 days leave the command and the table. Groups stay.

**Independent Test**: Stop the collector. Sign in and create a group. Both succeed. Run the activity command on the machine and see both rows. The group's address has no page of those rows. Start the collector and see both rows exported. A row older than 14 days is not printed, and a group from that same day remains.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T037 [P] [US3] Add failing durability tests in `tests/Quorum.App.Tests/ActivityDurabilityTests.cs`. Serves FR-020 and FR-021. When the export double throws, the attempt still returns, the outcome is unchanged, and `ExportedAt` stays null. A later successful export sets `ExportedAt` and does not insert a second row. The retention operation deletes `activity_records` whose `AcceptedAt` is older than 14 days and leaves a `groups` row from that same day, along with sessions, members, votes, and catalog rows. The command `activity <reference>` prints every row with that reference, oldest first, in the block from `specs/002-observability-local-deploy/contracts/activity.md`: `reference`, `attempt`, `outcome`, `accepted_at`, `duration_ms`, `storage_wait_ms`, `account_id`, `group_id`, `session_id`, `member_id`, `tracks`, `topics`, `designs`, `reason`, using `-` for an absent value and `unknown-account` or `-` for reason. Exit 0 for a row inside 14 days. Exit 1 and the text `not found` when the reference is unknown or `AcceptedAt` is older than 14 days. The output contains no password, email, display name, answer, rubric note, score, or catalog page body

### Implementation for User Story 3

- [X] T038 [P] [US3] Make `src/Quorum.App/Infrastructure/Logging/ActivityExporter.cs` keep polling rows whose `ExportedAt` is null. A failed send leaves `ExportedAt` null and is tried on a later pass. A successful send sets `ExportedAt` to the current UTC time and does not delete the row. Do not mark the row exported before the send. Do not await this loop from a Razor request. A duplicate send of a reference is still one submission in Grafana because the span carries `quorum.reference`
- [X] T039 [P] [US3] Add `src/Quorum.App/Infrastructure/Logging/ActivityRetention.cs` and register it from `src/Quorum.App/Infrastructure/Logging/LoggingExtensions.cs`. Once an hour, delete `activity_records` where `AcceptedAt` is older than 14 days using `IClock`. A row exactly 14 days old stays. Do not delete groups, memberships, sessions, votes, topic progress, or catalog rows. Expose the one-shot method the T037 test calls so the test does not wait an hour
- [X] T040 [P] [US3] Add `RunActivityCommandIfPresent` in `src/Quorum.App/Infrastructure/Logging/ActivityCommand.cs`. Do not add that method to `LoggingExtensions.cs`, which T039 edits. Call it from `src/Quorum.App/Program.cs` immediately after `RunCatalogCommandIfPresent` and before `app.Run()`. When argv contains `activity`, read `activity_records` for that reference, print the T037 block, and `Environment.Exit` with 0 or 1. The command does not call Grafana, does not start the collector, and does not run the health monitor or the retention loop. Debug invocation is `dotnet run -- activity <reference>` from `src/Quorum.App`. Compose invocation is `docker compose exec quorum dotnet Quorum.App.dll activity <reference>`. There is no Razor page for this command

**Checkpoint**: A down collector does not refuse sign-in. The command prints the stored row. Rows older than 14 days are gone from the table and from the command. The group remains

---

## Phase 6: User Story 4 - Run locally and deploy for one group from the guide (Priority: P2)

**Goal**: `docs/run.md` is the guide a person who has not worked on Quorum can follow. Local start says local use only and keeps data across stop and start. The deploy path on one machine says ready for a group only after every local-only setting is replaced. Erase is the only documented wipe.

**Independent Test**: Follow only the guide. Locally, start, see local use only, create a group, restart, and see the group. On one machine, replace every local-only setting, see ready for a group, restart, and see the group. Stop without losing the group. Erase, start again, and see that group gone. Each numbered step matches the result printed in the guide.

### Implementation for User Story 4

- [X] T041 [P] [US4] Add `deploy/stop.sh`. It runs `docker compose -p quorum -f docker-compose.yml stop`. It does not pass `-v`, does not run `down`, and does not delete volumes. Groups, sessions, the catalog, and activity rows younger than 14 days remain
- [X] T042 [P] [US4] Add `deploy/erase.sh`. It is the only script that runs `docker compose -p quorum -f docker-compose.yml down -v`. It targets project `quorum` only. It does not run from `deploy/stop.sh` or `deploy/up.sh`. After it, the next `deploy/up.sh` has no previous group
- [X] T043 [US4] Write the self-contained local path, the one-machine deploy path, and the failure notes in `docs/run.md`, matching `specs/002-observability-local-deploy/contracts/guide.md`. Number every step and state the result of that step. Name Docker as the first prerequisite. Local path: `deploy/up.sh` starts the app, PostgreSQL, the collector, Loki, Prometheus, Tempo, and Grafana. `GET http://localhost:8080/status` says `ready` and `local use only`. Inside the network Quorum sends to `http://otel-collector:4318` and does not send straight to Grafana, Loki, Prometheus, or Tempo. Document `docker compose exec quorum dotnet Quorum.App.dll activity <reference>`. `deploy/stop.sh` then `deploy/up.sh` keeps the group and today's activity row. A second `deploy/up.sh` prints `A self-contained deployment is already running. Refusing to start a second one. Stored data was not erased.` and does not delete volumes. `deploy/erase.sh` is a separate step. Deploy path: same compose file, one machine, replace the database password, the collector password, the Grafana password, the Prometheus password, and set `PublicBaseUrl` to a host other than `localhost` or `127.0.0.1`. Result: `/status` says `ready` and `ready for a group`. Leaving any one of those unchanged keeps `local use only`. Loki and Tempo have no password. The path does not ask for a second machine, a paid account, a cluster, or a tool hosted outside this compose project. The Compose file publishes Grafana on `127.0.0.1:3000`. Each tool is on its own domain and port, which may be `127.0.0.1` or another domain, and is not the group's Quorum address. A certificate vendor is out of scope. For each tool, name the service and the localhost port the local file publishes: collector `otel-collector` and `127.0.0.1:4318`, Grafana `grafana` and `127.0.0.1:3000`, Loki `loki` and `127.0.0.1:3100`, Prometheus `prometheus` and `127.0.0.1:9090`, Tempo `tempo` and `127.0.0.1:3200`, PostgreSQL `postgres` and `127.0.0.1:5432`. Failure notes cover Docker missing or stopped, `/status` stored data `down`, the app port or a tool port already in use, Grafana empty while the activity command prints the row, and `/status` staying on `local use only` after a deploy. Say a forced kill may not write `application-stop` and that earlier rows remain. Leave the development debug section for T047

**Checkpoint**: A person can follow the local path and the deploy path from `docs/run.md` without the author. Debug attach is not in the guide yet

---

## Phase 7: User Story 5 - Debug against containers that are already running (Priority: P2)

**Goal**: `dotnet run` does not start the tool set. It sends only to the collector on `127.0.0.1:4318`. It does not use the service name `otel-collector` and does not fall back to that name when the port is closed.

**Independent Test**: Start the debug compose file so the collector's localhost port is open. Start `dotnet run`, register, and see that one row in Grafana. Confirm the session did not start the tool set and did not send straight to Loki, Prometheus, or Tempo. Stop the collector, register again, and confirm the attempt finishes and the activity command prints the row. Starting the collector later shows that row without a second register.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T044 [P] [US5] Add a failing debug-endpoint test in `tests/Quorum.App.Tests/DebugEndpointTests.cs`. Serves FR-029. The Development configuration sets `Telemetry:OtlpEndpoint` to `http://127.0.0.1:4318` and the database host to `localhost`. The bound options contain no `otel-collector` host, and no endpoint whose host is Loki, Prometheus, Tempo, or Grafana. When that localhost port is closed, the resolved endpoint is still `http://127.0.0.1:4318`

### Implementation for User Story 5

- [X] T045 [P] [US5] Add `docker-compose.debug.yml` with compose project name `quorum-debug`. It starts `postgres`, `otel-collector`, `loki`, `prometheus`, `tempo`, and `grafana`. It does not start the app. Host ports are `127.0.0.1` only: PostgreSQL `5432`, collector `4318`, Grafana `3000`, Loki `3100`, Prometheus `9090`, Tempo `3200`. Reuse the config files under `deploy/`. Loki and Tempo still have no password. Use the published tags confirmed while writing `docker-compose.yml`. Do not invent a tag and do not use `latest`. Use a volume other than `quorum-postgres` so this file is not a second self-contained deployment and `deploy/erase.sh` does not wipe it. Do not call `otel-collector` from a host URL. The collector's published host port is `127.0.0.1:4318`
- [X] T046 [P] [US5] Set `Telemetry:OtlpEndpoint` to `http://127.0.0.1:4318` in `src/Quorum.App/appsettings.Development.json`, and set `Telemetry:CollectorUser`, `Telemetry:CollectorPassword`, `Telemetry:GrafanaAdminPassword`, `Telemetry:PrometheusPassword`, and `PublicBaseUrl` to the printed defaults. Keep the existing `Quorum` connection string on `Host=localhost;Port=5432`. In `src/Quorum.App/Infrastructure/Logging/TelemetryExtensions.cs`, send only to the configured endpoint. Do not substitute `http://otel-collector:4318` when port 4318 is closed. Do not add a second exporter toward Grafana, Loki, Prometheus, or Tempo. `dotnet run` must not start `docker-compose.debug.yml`
- [X] T047 [US5] Add the development debug path to `docs/run.md` without removing the local or deploy sections. Steps, each with its result: `docker compose -f docker-compose.debug.yml up` starts PostgreSQL and the tool set and does not start the app. From `src/Quorum.App`, `dotnet run` uses `http://127.0.0.1:4318` and the localhost database. It does not start those containers and does not call `otel-collector`. A register row shows in Grafana after the collector accepts it. Stop the collector and register again: the attempt still finishes, `dotnet run -- activity <reference>` prints the row, and `/status` can still say `ready`. Start the collector again and that row appears in Grafana without a second register. Repeat the service name and localhost port of each tool. State that this session is not a second self-contained deployment and is not refused by `deploy/up.sh`

**Checkpoint**: A debug session attaches to containers that are already running. The self-contained stack still uses `http://otel-collector:4318`

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Confirm the stories work together and the deferred scope stayed out

- [X] T048 [P] Confirm `src/Quorum.App/Features/Shared/_Layout.cshtml` and the pages under `src/Quorum.App/Features/` do not link to Grafana, Loki, Prometheus, Tempo, or the collector. Confirm `src/Quorum.App/Quorum.App.csproj` does not reference `OpenTelemetry.Instrumentation.EntityFrameworkCore` or `OpenTelemetry.Instrumentation.Runtime`. Confirm `deploy/loki.yaml`, `deploy/tempo.yaml`, and `docker-compose.yml` set no password for Loki or Tempo and publish those ports on `127.0.0.1` only. Confirm there is no repository interface, no second `DbContext`, and no sampler. Confirm handlers still take `QuorumDbContext`
- [X] T049 Run `dotnet test tests/Quorum.App.Tests/Quorum.App.Tests.csproj` from the repository root and confirm the checks in `specs/002-observability-local-deploy/quickstart.md`. This command does not start Compose. The self-contained, debug, and deploy paths in that quickstart stay manual

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup. Blocks every user story
- **User Story 1 (Phase 3)**: Depends on Foundational. No dependency on later stories
- **User Story 2 (Phase 4)**: Recording depends on Foundational. The dashboard task edits deploy files created in User Story 1
- **User Story 3 (Phase 5)**: Depends on the exporter from User Story 2
- **User Story 4 (Phase 6)**: Stop and erase depend on the compose project from User Story 1. The guide names the activity command from User Story 3
- **User Story 5 (Phase 7)**: Debug compose reuses deploy config from User Story 1. The debug section edits `docs/run.md` after User Story 4
- **Polish (Phase 8)**: Depends on the stories being present

### User Story Dependencies

- **User Story 1 (P1)**: First shippable slice. Anonymous status, local-only detection, and one self-contained start
- **User Story 2 (P1)**: Activity rows, refusal references, and Grafana. Automated tests need no live Grafana
- **User Story 3 (P2)**: Retry, 14-day delete, and the machine command. Needs the exporter
- **User Story 4 (P2)**: The operator guide for local and deploy. Needs the start script and the machine command
- **User Story 5 (P2)**: Debug attach. Needs the guide file and the deploy configs

### Within Each User Story

- Tests are written and fail before that story's implementation
- The activity row and the writer exist before command call sites
- T027 finishes before T028 through T034. Those seven tasks may run together only after T027. T035 does not use the partial
- A story is independently testable at its checkpoint

### Parallel Opportunities

- T001 and T002 can run together
- T003, T004, T005, and T006 can run together
- T016 through T021 can run together
- T024, T025, and T026 can run together
- T028 through T034 can run together only after T027. Their `[P]` marker is that wave, not a wave with T027. T035 does not use the partial and can run beside them
- T038, T039, and T040 can run together
- T041 and T042 can run together
- T044, T045, and T046 can run together

---

## Parallel Example: User Story 1

```bash
# Tests first:
Task: "T012 StatusTests in tests/Quorum.App.Tests/StatusTests.cs"

# Deploy files together, after Foundational:
Task: "T016 deploy/local.env"
Task: "T017 deploy/loki.yaml"
Task: "T018 deploy/tempo.yaml"
Task: "T019 deploy/prometheus.yml"
Task: "T020 deploy/grafana/provisioning/"
Task: "T021 deploy/otel-collector.yaml"
```

## Parallel Example: User Story 2

```bash
# Tests together:
Task: "T024 ActivityRedactionTests in tests/Quorum.App.Tests/ActivityRedactionTests.cs"
Task: "T025 ElectionActivityTests in tests/Quorum.App.Tests/ElectionActivityTests.cs"
Task: "T026 NoToolLinkTests in tests/Quorum.App.Tests/NoToolLinkTests.cs"

# T027 first. It is not in the parallel wave:
Task: "T027 Features/Shared/_Reference.cshtml"

# T028 through T034 together, only after T027:
Task: "T028 AccountCommands and account pages"
Task: "T029 SignOut"
Task: "T030 GroupCommands and GroupList"
Task: "T031 RosterCommands and Members"
Task: "T032 SessionCommands and SessionIndex"
Task: "T033 Election and SessionDetail"
Task: "T034 PublishCatalog"

# Does not use the partial:
Task: "T035 ActivityExporter"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: `GET /status` without an account, local use only on the printed defaults, not ready within 30 seconds when stored data is down, and a second `deploy/up.sh` refused with data still present
5. Demo the ready signal before recording attempts

### Incremental Delivery

1. Setup and Foundational, then User Story 1, is the ready signal
2. User Story 2 adds one reference per submission and the Grafana screen
3. User Story 3 keeps the session running when that screen is down
4. User Story 4 is the guide for local and one-machine deploy
5. User Story 5 is the debug attach path
6. Each story adds value without breaking the previous story

### Parallel Team Strategy

With multiple developers:

1. The team completes Setup and Foundational together
2. Once Foundational is done, one developer takes User Story 1 while another writes the User Story 2 tests
3. User Story 2 dashboard work waits for the User Story 1 deploy files
4. User Story 3 starts when the exporter exists
5. The guide is last among the story phases so it names commands that already exist

---

## Notes

- [P] tasks use different files and do not depend on an unfinished task. T028 through T034 are the exception: their `[P]` applies only to the wave that starts after T027. T027 is not in that wave
- [Story] maps a task to a user story. Setup, Foundational, and Polish have no story label
- Each user story is independently testable at its checkpoint
- Verify tests fail before implementing that story
- Commit after each task or logical group
- Stop at any checkpoint to validate that story on its own
- The stored attempt name for saving topics is `set-topics`
- Loki and Tempo have no password
- The reference alphabet is `23456789ABCDEFGHJKMNPQRSTVWXYZ`. One submission has one reference. `cast-vote` and `elect-facilitator` from that submission share it
- SC-008 is met by writing the activity row and returning before export. Do not add a wall-clock timing test
- Each tool is on its own domain and port, which may be `127.0.0.1` or another domain, and is not the group's Quorum address. The local Compose file publishes tools on `127.0.0.1`. Debug stays on localhost. A certificate vendor is out of scope
- While writing `docker-compose.yml`, confirm a published image tag. Do not use `latest`. Do not invent a tag in the spec

## Phase 9: Convergence

- [X] T050 Make the stored-data line on `GET /status` name stored data. A down probe renders text that includes both `stored data` and `down`. A reached probe includes `stored data` and `reachable`. Keep `not ready`, `ready`, `local use only`, and `ready for a group`. Add no group, member, secret, or catalog text. per US1/AC3 (partial)
- [X] T051 Change the Quorum Grafana logs panel so one filter can require a reference, a refused outcome, a group id, and a session id together with the dashboard time range. A refused filter must not also return succeeded rows. Group id and session id must match the exported `quorum.group_id` and `quorum.session_id` attributes, which are not in the log line `reference=... attempt=... outcome=...`. An empty filter must not hide rows. per US2/AC10 (partial)
- [X] T052 Emit `quorum.attempts` so each exported row adds one to the last-hour count. Aggregate each batch by `attempt` and `outcome`, and send a delta or a running cumulative total that `sum(increase(quorum_attempts_total[1h]))` can count. Do not send a fresh cumulative value of 1 for every row on the same series, and do not give those rows one shared timestamp that collapses them. Three succeeded rows and one refused row in an hour must display as 3 and 1. per US2/AC11 (partial)
- [X] T053 Stamp exported logs, spans, and metrics from `AcceptedAt`, and do not send a row whose `AcceptedAt` is already older than 14 days. A row exported after the collector was down must leave Grafana 14 days after it was accepted, not 14 days after the export. Leave `ExportedAt` unset on a skipped row so retention still deletes it. Use the same 14-day cutoff as `ActivityRetention`. per FR-021 (partial)

## Phase 10: Convergence

- [X] T054 Make `quorum.attempts` reach Prometheus as a cumulative series that `sum(increase(quorum_attempts_total[1h]))` can count. `ActivityExporter` sends that sum with aggregation temporality 1 (delta). Collector image `otel/opentelemetry-collector-contrib:0.164.0` drops a delta sum in `prometheusremotewrite` (`isValidAggregationTemporality` accepts only cumulative sums), and `deploy/otel-collector.yaml` has no delta-to-cumulative processor. Three succeeded rows and one refused row in an hour must display as 3 and 1. Keep labels `attempt` and `outcome` only. Do not send a fresh cumulative value of 1 for every row on the same series, and do not give those rows one shared timestamp that collapses them. per US2/AC11 (partial)
- [X] T055 Make the Quorum dashboard trace panel return the `quorum.attempt` span for the selected reference, including stored-data wait and the span duration. `deploy/grafana/provisioning/dashboards/quorum.json` requires `span.quorum.storage_wait_ms >= 0`. `OtlpActivityExport` writes `quorum.storage_wait_ms` as a string. Tempo stores string and integer attributes in separate columns, so that numeric comparison does not select the span. per FR-031 (partial)

## Phase 11: Convergence

- [X] T056 Make the Quorum dashboard Succeeded and Refused panels display 3 and 1 when three succeeded attempts and one refused attempt were accepted in the last hour. `sum(increase(quorum_attempts_total{outcome="succeeded"}[1h]))` and the refused twin do not. `ActivityExporter` posts one reference at a time, so three succeeded rows on different attempt names become three series, and Prometheus `increase()` returns 0 for a series with one sample. Three rows that share an attempt name become cumulative samples 1, 2, 3 after `delta_to_cumulative`, and `increase()` counts only the rise after the first sample. Keep labels `attempt` and `outcome` only. Do not send a fresh cumulative value of 1 for every row on the same series, and do not give those rows one shared timestamp that collapses them. per US2/AC11 (partial)
