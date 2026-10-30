# Contract: Telemetry path

Quorum exports logs, metrics, and traces only to the OpenTelemetry collector. It does not open a connection to Loki, Prometheus, Tempo, or Grafana.

| Path | Collector endpoint | Auth |
| --- | --- | --- |
| Self-contained Compose | `http://otel-collector:4318` | Basic auth from the env file |
| Debug session | `http://127.0.0.1:4318` | The same basic auth |

A missing or wrong collector password refuses the export. The attempt still finishes and the row stays with `exported_at` null. The debug session does not switch to `otel-collector` when port 4318 is closed.

The collector then forwards:

| Signal | Destination | Notes |
| --- | --- | --- |
| Logs | `http://loki:3100/otlp` | No password |
| Metrics | Prometheus remote write on `prometheus:9090` | Prometheus basic auth |
| Traces | Tempo OTLP on `tempo:4317` | No password |

Resource attribute `service.name` is `quorum`.

## Allowlist

Exported attributes are only:

- `quorum.attempt`
- `quorum.outcome`
- `quorum.reason`
- `quorum.reference`
- `quorum.duration_ms`
- `quorum.storage_wait_ms`
- `quorum.account_id`
- `quorum.group_id`
- `quorum.session_id`
- `quorum.member_id`
- `quorum.tracks`
- `quorum.topics`
- `quorum.designs`
- `service.name`

The collector drops any other attribute, including request bodies, SQL text, emails, and cookies. ASP.NET Core instrumentation may add the route path. It must not add form fields or query strings.

Metrics are the counter `quorum.attempts` (labels `attempt`, `outcome` only) and the histograms `quorum.attempt.duration` and `quorum.storage.wait`. Those labels stay low-cardinality. Group and account ids are not metric labels.

One trace span per submission is named `quorum.attempt` and carries `quorum.reference`. The vote and the election from one submission share that trace as well as the reference.

## Grafana

Anonymous access is off. The operator signs in with the Grafana admin password. One provisioned dashboard shows:

- Succeeded and refused counts for the last hour, from Prometheus.
- Rows filtered by reference, refused outcome, group id, session id, and time, from Loki.
- The trace for that reference, from Tempo, including `quorum.storage_wait_ms`.

A wrong Grafana password shows no rows. A wrong Prometheus password shows no counts. Loki and Tempo have no password. The local Compose file and the debug Compose file publish their host ports on `127.0.0.1`. A tool's address may be that loopback port or another domain, and is not the group's Quorum address.

## Retention

Loki 336 hours, Tempo block retention 336 hours, Prometheus 14 days. That matches the 14-day delete of `activity_records`.
