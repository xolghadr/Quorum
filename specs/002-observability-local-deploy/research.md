# Research: Transparent Local Operations

## Decision: One process exports OTLP only to the collector

**Decision**: Add the OpenTelemetry .NET SDK to `Quorum.App`. Logs, metrics, and traces leave the process through one OTLP/HTTP exporter. In Compose the endpoint is `http://otel-collector:4318`. In a debug session it is `http://127.0.0.1:4318`. The exporter adds the collector's basic-auth header. There is no exporter aimed at Loki, Prometheus, Tempo, or Grafana.

**Rationale**: The spec says Quorum sends only to the collector, production uses the Docker service name, and development uses the localhost port. OTLP/HTTP on 4318 is the collector's standard HTTP receiver and is easy to stub in tests. The collector image is `otel/opentelemetry-collector-contrib` because the basic-auth extension and the Prometheus remote-write exporter ship in the contrib build. While writing `docker-compose.yml`, confirm a published tag and pin it. Do not use `latest`. Do not invent a tag in the spec.

**Alternatives considered**: Three exporters inside the app would ignore the collector. gRPC on 4317 is equivalent and harder to fake with `HttpClient`. Grafana Alloy would replace the collector the spec names.

## Decision: The activity row is the record that survives

**Decision**: `activity_records` is a table in the existing PostgreSQL database. The request writes that row and returns. `ActivityExporter`, a hosted service in the same process, sends rows whose `exported_at` is null and sets `exported_at` only after the collector accepts the batch. A stopped collector leaves the row in place. The next successful pass sends it, and Grafana then shows it. The guide's command reads this table by reference.

**Rationale**: The spec requires 14 days and a restart, and it forbids a second database. Process text does not survive a restart. Waiting for the collector on the request would break the one-second bound when the collector is down. One stored row is the source of truth. Export is at-least-once if the process dies after a successful send and before `exported_at` is saved. Grafana groups by `quorum.reference`, so a duplicate export is still one attempt. The command prints one row.

**Alternatives considered**: Exporting only from the OpenTelemetry pipeline and hoping the SDK buffer survives a restart fails the 14-day rule. A dedicated outbox database is the second database the spec declined. Marking exported before the send would hide a down collector.

## Decision: SafeLog grows into the writer

**Decision**: Keep `SafeLog` as the type command classes already construct. Extend it so every finished attempt in the spec's list writes one `activity_records` row, including refusals that return before today's `_log.Command` call. A scoped submission holds one short reference, one stopwatch, and the storage-wait total. The vote that stores a facilitator writes `cast-vote` and `elect-facilitator` with that same reference.

**Rationale**: The call sites already exist for the successful commands. A new mediator would violate the project's "no mediator" rule. An MVC filter would miss `publish-catalog`, which does not pass through a page.

**Alternatives considered**: A result filter on Razor pages misses the publish command and the health transition. A Wolverine middleware is a mediator this app removed.

## Decision: Storage wait is a duration, not SQL text

**Decision**: A `DbCommandInterceptor` adds each command's elapsed time to a scoped `StorageWait`. It does not read or export the command text. `ActivityRecord.StorageWaitMs` is that total. The OpenTelemetry EF instrumentation package is not added.

**Rationale**: The spec asks how much of the attempt waited on stored data. SQL text can carry values that must not leave the process. The interceptor sees duration without logging the statement.

**Alternatives considered**: `OpenTelemetry.Instrumentation.EntityFrameworkCore` records `db.statement` by default. Wrapping only `SaveChangesAsync` misses the reads that happen before the save.

## Decision: Short reference

**Decision**: Eight characters from the alphabet `23456789ABCDEFGHJKMNPQRSTVWXYZ`, so a reference contains none of `0`, `O`, `1`, `I`, `L`, or `U`. One submission has one reference. The unique index is on `Reference` plus `Attempt`, so `cast-vote` and `elect-facilitator` from that submission can share it and a second `cast-vote` cannot. Before the first insert of a new submission, any existing reference is a collision and generation runs again. Refusal pages, the activity command, and Grafana all search that value.

**Rationale**: The person in the room reads it aloud. Dropping those six characters avoids the look-alikes that standard Crockford base32 still emits (`0` and `1`). Eight characters are enough for one instance's 14-day volume.

**Alternatives considered**: Standard Crockford base32 (`0123456789ABCDEFGHJKMNPQRSTVWXYZ`) is 32 symbols and still emits `0` and `1`, which are easy to mishear as `O` and `I`. A unique index on `Reference` alone would block the election row from sharing the vote's reference. A full UUID is accurate and unpleasant to read aloud. A monotonic integer leaks how many attempts the instance has served.

## Decision: `/status` stays up when PostgreSQL does not

**Decision**: `HealthMonitor` probes `SELECT 1` every 5 seconds with a 2-second timeout and keeps the latest result in memory. `GET /status` is an anonymous Razor page that reads that memory. It does not query groups or activity. Startup calls `Database.Migrate()` and, if PostgreSQL is unreachable, logs a safe failure and continues. A change between ready and not ready writes one activity row. An unchanged probe writes nothing.

**Rationale**: The spec gives 30 seconds and forbids the page from requiring sign-in or showing practice content. A page that queries PostgreSQL on every load cannot render while stored data is down. Five seconds plus a two-second timeout fits inside 30 seconds.

**Alternatives considered**: ASP.NET health checks that fail the process when the database is down would take the session with them. Writing a row on every probe floods the 14-day table.

## Decision: Local-only means four secrets and the address

**Decision**: The printed defaults are the database password `quorum`, the collector password `quorum`, the Grafana admin password `quorum`, the Prometheus password `quorum`, and a public base URL whose host is `localhost` or `127.0.0.1`. The app compares the configured values to those defaults. If any match, `/status` says local use only and does not say ready for a group. The comparison values are not rendered. Loki and Tempo have no password and are not part of this check.

**Rationale**: The spec says the status stays on local use only until every documented local-only setting is replaced. The app can see the database password, the collector header, the Grafana password, and the Prometheus password because Compose passes those four into the app as well as into the tools. The user told planning to leave Loki and Tempo passwords unset.

**Alternatives considered**: A single `LocalOnly=true` flag can be flipped while the Grafana password is still `quorum`, which fails the spec. Reading Grafana's database from the app adds a client the app should not have.

## Decision: Tool ports and passwords

**Decision**: The local Compose file publishes tool ports on `127.0.0.1`. That publish address is the local default. Each tool is on its own domain and port, which may be `127.0.0.1` or another domain, and is not the address the group uses for Quorum. Debug host ports stay on `127.0.0.1`. A certificate vendor is out of scope. The app's port is the one the group uses. Service DNS names inside the Compose network are `otel-collector`, `loki`, `prometheus`, `tempo`, and `grafana`.

| Tool | In-network address | Host port for debug | Credential |
| --- | --- | --- | --- |
| Collector OTLP/HTTP | `otel-collector:4318` | `127.0.0.1:4318` | Basic auth, password from the env file |
| Grafana | `grafana:3000` | `127.0.0.1:3000` | Admin password from the env file, anonymous access off |
| Prometheus | `prometheus:9090` | `127.0.0.1:9090` | Basic auth from `web.yml`, remote write enabled |
| Loki | `loki:3100` | `127.0.0.1:3100` | None |
| Tempo | `tempo:3200` | `127.0.0.1:3200` | None |
| PostgreSQL | `postgres:5432` | `127.0.0.1:5432` | Existing `quorum` / `quorum` local default |

The collector's `attributes` processor keeps the allowlist in [contracts/telemetry.md](./contracts/telemetry.md) and drops anything else. Grafana is provisioned with datasources for Loki, Prometheus, and Tempo, and with one dashboard that filters on reference, outcome, group, session, and time, and shows the last hour's succeeded and refused counts.

**Rationale**: The spec separates each tool's domain and port from the group's address and asks for Grafana and Prometheus passwords plus collector credentials. The local file publishes on `127.0.0.1` so passwordless Loki and Tempo are not on the group's address by default. Another domain remains allowed and still gets no Loki or Tempo password. While writing `docker-compose.yml`, confirm a published tag. Do not use `latest`. Do not invent a tag in the spec.

**Alternatives considered**: A reverse proxy in front of Loki and Tempo would be a sixth product and would add the password the user said to skip. Publishing the tools on `0.0.0.0` would put them on the network the group uses.

## Decision: Two Compose files and three scripts

**Decision**: `docker-compose.yml` is the self-contained stack: app, PostgreSQL, collector, Loki, Prometheus, Tempo, Grafana. The app's OTLP endpoint is `http://otel-collector:4318`. `docker-compose.debug.yml` starts PostgreSQL and the tool set only, with host ports on `127.0.0.1`, and does not start the app. `deploy/up.sh` runs the self-contained file and exits with the guide's message if that app container is already running. It does not remove volumes. `deploy/stop.sh` stops containers and leaves volumes. `deploy/erase.sh` is the only script that runs `docker compose down -v`.

**Rationale**: The spec splits a self-contained deployment from a debug session that attaches to containers already running. Compose's own `up` is idempotent and would not refuse a second start. A script can. `down -v` on the normal stop would violate the restart rule.

**Alternatives considered**: One Compose file with profiles hides the debug path in a flag the guide would still have to explain. Letting `docker compose up` count as the second-start refusal is false, because Compose reuses the running project.

## Decision: Retention is 14 days in every copy

**Decision**: `ActivityRetention` deletes `activity_records` older than 14 days, once an hour, in the app process. Loki's retention period is 336 hours. Tempo's block retention is 336 hours. Prometheus's TSDB retention is 14 days. Groups, sessions, members, votes, and the catalog are not deleted by this job.

**Rationale**: The spec applies 14 days to Grafana and to the guide's command. The command reads PostgreSQL, so the row must go. The stores behind Grafana must drop the export on the same horizon or the screen would outlive the command.

**Alternatives considered**: Keeping Loki forever and hiding old rows only in the dashboard would fail a direct Loki query on the operator's machine. Deleting group data after 14 days would destroy the product.

## Decision: The guide is `docs/run.md`

**Decision**: The operator guide lives at `docs/run.md`. It has three numbered paths: self-contained local, one-machine deploy, and development debug. Each step states the result the operator should see. The failure notes cover a missing Docker engine, stored data not ready, a taken port, a stopped collector, and local-only settings left on a group-facing copy. `quickstart.md` is the implementer's check, not a substitute for that guide.

**Rationale**: The spec requires a guide a person who has not worked on Quorum can follow. The spec directory is the planning artifact. `docs/run.md` is the file the guide itself can tell that person to open.

**Alternatives considered**: Putting the only copy under `specs/` makes the operator depend on the planning tree. A README section would mix the license and the run steps.
