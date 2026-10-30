# Implementation Plan: Transparent Local Operations

**Branch**: `002-observability-local-deploy` | **Date**: 2026-10-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-observability-local-deploy/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

The operator who did not write Quorum can start one self-contained deployment, read whether it is ready, and match a failed attempt to a short reference without answer text, rubric notes, or scores. A debug session does not start that stack. It sends to an OpenTelemetry collector that is already running on localhost.

Quorum stays one ASP.NET Core process and one PostgreSQL database. Each finished attempt in the spec's list is one row in that database. The request path writes the row and returns. A background loop in the same process exports the row to the collector and retries while the collector is down. The collector is the only export destination. It forwards logs to Loki, metrics to Prometheus, and traces to Tempo. Grafana is the screen the operator signs in to. Loki and Tempo have no password. Grafana, Prometheus, and the collector do.

`/status` is an anonymous page. It says ready or not ready, names stored data when that part is down, and says local use only until the documented database password, collector credential, Grafana password, Prometheus password, and local-only address have all been replaced. The guide is `docs/run.md`.

## Technical Context

**Language/Version**: C# on .NET 10 (ASP.NET Core)

**Primary Dependencies**: ASP.NET Core Razor Pages, Entity Framework Core, Npgsql, and the OpenTelemetry .NET SDK (`OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`). Export uses OTLP/HTTP to the collector. No mediator, no second database client, no SPA toolchain.

**Storage**: PostgreSQL 17, the existing `Quorum` database. Activity rows live in that database. Loki, Prometheus, and Tempo keep the exported copy for 14 days. They are not a system of record.

**Testing**: xUnit and `WebApplicationFactory` in `tests/Quorum.App.Tests`. Domain rules for redaction, local-only detection, and the short reference are pure tests. The activity row, the 14-day delete, and the refusal page are integration tests against PostgreSQL. The collector is a test double. The full Docker path is the manual check in `quickstart.md`.

**Target Platform**: Linux. One app process and one database on one machine. Local debug uses the same app on the host, with Postgres and the tool set already running in Docker and published on `127.0.0.1`.

**Project Type**: Server-rendered web application inside a modular monolith, plus a Compose file for the tool set.

**Performance Goals**: An ordinary attempt that already finished in under 1 second still finishes in under 1 second with recording on, for a session of eight. The export to the collector does not run on that wait. The automated check is that the attempt returns while `ExportedAt` is still null. There is no wall-clock timing test. The operator finds an attempt's outcome and duration in under 1 minute in Grafana or with the guide's command. `/status` shows stored data down within 30 seconds.

**Constraints**: Logs, traces, metrics, `/status`, and the activity command must not contain answer text, rubric notes, scores, passwords, session cookies, emails, or display names. Quorum sends only to the collector. A session still runs when the collector or Grafana is stopped. Loki and Tempo have no password. No phone, email, or chat alert. No member-facing rank or score board. No second database and no repository layer.

**Scale/Scope**: One group of 2 to 8 people on one machine, and the acceptance case of a second group on that same instance. Activity rows are kept for 14 days. This is not a multi-machine collector deployment.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Result | Evidence |
| --- | --- | --- |
| I. Quorum of two, no standing facilitator | Pass | This feature does not change session rules, group size, or who is elected. |
| II. Everyone practices | Pass | No leaderboard, streak, or score board. Activity rows store no scores. |
| III. Content is data, read from the database | Pass | Catalog pages are unchanged. Activity rows are new rows in the same database. |
| IV. Group-scoped from the first table | Pass | An activity row may store `GroupId`, `SessionId`, `AccountId`, and `MemberId`. It stores no display name and no email. Member pages do not list activity. The machine command is the operator's, not a member query. |
| IV. No billing, SSO, marketplace, or mobile | Pass | Tool passwords are local to this deployment. There is no external account. |
| V. A person present uses the web UI, no adapter required | Pass | `/status` is a page on the app. Refusal pages show the short reference. Grafana is the operator's window, not a step the group must open. |
| V. No media hosting | Pass | No media. |
| Privacy | Pass | Allowlist of exported attributes in [contracts/telemetry.md](./contracts/telemetry.md). SQL text is not exported. `/status` shows no group, member, or practice content. |
| Out of scope items | Pass | No katas, points, video, billing, SSO, or alerts. The 45-minute runner is untouched. |
| One process, one database, no repository layer | Pass with justification | The session path is still `Quorum.App` and PostgreSQL. The tool set is extra containers. See Complexity Tracking. Handlers still use `QuorumDbContext` directly. |
| Delivery bar for this spec | Pass | The person served is the operator, who may also facilitate. The outcome is a ready signal before the session and a reference the room can read aloud when a step fails. |

Re-checked after Phase 1. The data model adds columns on the existing database only. The contracts do not put Grafana on the group's address. The gate results stand.

## Project Structure

### Documentation (this feature)

```text
specs/002-observability-local-deploy/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── status.md
│   ├── activity.md
│   ├── telemetry.md
│   └── guide.md
└── tasks.md             # task list from /speckit-tasks
```

### Source Code (repository root)

```text
src/Quorum.App/
├── Program.cs                         # extension calls, activity command, app.Run()
├── Features/
│   └── Status/
│       ├── Status.cshtml              # GET /status, anonymous
│       └── Status.cshtml.cs
├── Infrastructure/
│   └── Logging/
│       ├── SafeLog.cs                 # grows into the activity writer the commands already call
│       ├── ActivityRecord.cs
│       ├── ActivityConfiguration.cs
│       ├── StorageWait.cs             # scoped wait total
│       ├── StorageWaitInterceptor.cs  # duration only, no SQL text
│       ├── ActivityExporter.cs        # same process, retries unexported rows
│       ├── ActivityRetention.cs       # deletes rows older than 14 days
│       ├── HealthMonitor.cs           # probe loop, in-memory status, transition rows
│       ├── LocalOnly.cs               # compares configured secrets to the printed defaults
│       └── ActivityCommand.cs         # `activity <reference>` prints one stored row
└── Dockerfile                         # app image used by Compose

deploy/
├── local.env                          # printed defaults, local use only
├── otel-collector.yaml
├── loki.yaml
├── tempo.yaml
├── prometheus.yml
├── grafana/provisioning/              # datasources and one Quorum dashboard
├── up.sh                              # refuses a second self-contained start
├── stop.sh                            # stop, keep volumes
└── erase.sh                           # the only script that deletes volumes

docs/run.md                            # the operator guide

docker-compose.yml                     # self-contained stack
docker-compose.debug.yml               # postgres + tool set, 127.0.0.1 ports, no app container
```

**Structure Decision**: One web project, as today. `Program.cs` gains `RunActivityCommandIfPresent()` next to the catalog command and still ends with `app.Run()` for the normal path. Pages stay under `Features/` because `AddRazorPages` uses that root. Recording, export, health, and retention stay in `Infrastructure/Logging` beside `SafeLog`, which the command classes already call. That folder is not a second process. Compose files and tool config stay in `deploy/` and the repository root so the guide can point at them. Tests stay in `tests/Quorum.App.Tests`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Grafana, Loki, Prometheus, Tempo, and the OpenTelemetry collector run beside the app | The spec requires a self-contained deployment where the operator answers readiness, outcome, duration, and one shared reference in Grafana. The app and PostgreSQL cannot draw that screen. | Process text fails the 14-day restart rule. A second database would copy Loki. Sending straight to Loki, Prometheus, and Tempo skips the collector the user required. |
| Loki and Tempo have no password | The user directed planning to leave those passwords unset. | Inventing a proxy or a password file would add a service the tool set does not include. The local Compose file publishes their ports on `127.0.0.1`, separate from the address the group uses. Another domain remains allowed and still gets no Loki or Tempo password. A certificate vendor is out of scope. |
