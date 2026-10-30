# Data Model: Transparent Local Operations

One new table in the existing PostgreSQL database. No new database. Health for `/status` is memory in the app process, not a table. Groups, sessions, members, votes, and the catalog are unchanged.

## ActivityRecord

Table: `activity_records`

| Field | Type | Required | Rule |
| --- | --- | --- | --- |
| `Id` | uuid | yes | Primary key. |
| `Reference` | text | yes | Eight characters from `23456789ABCDEFGHJKMNPQRSTVWXYZ`, so it contains none of `0`, `O`, `1`, `I`, `L`, or `U`. One per submission. Shared by the `cast-vote` row and the `elect-facilitator` row from that submission. Safe to read aloud. |
| `Attempt` | text | yes | One of: `register`, `sign-in`, `sign-out`, `create-group`, `add-member`, `rename-member`, `remove-member`, `publish-catalog`, `create-session`, `set-topics`, `save-attendance`, `cast-vote`, `elect-facilitator`, `application-start`, `application-stop`, `health-ready`, `health-not-ready`. |
| `Outcome` | text | yes | `succeeded` or `refused`. A refused attempt has no second row that says succeeded. |
| `Reason` | text | no | Only `unknown-account` on a sign-in where no account matches. Absent for every other row. Never an email. |
| `AcceptedAt` | timestamptz | yes | When the product accepted the attempt. Rows are ordered by this. |
| `DurationMs` | integer | yes | Zero or positive. The whole attempt. |
| `StorageWaitMs` | integer | yes | Zero or positive. The part of `DurationMs` spent waiting on stored data. Zero when the attempt did not touch the database. |
| `AccountId` | uuid | no | Set when the attempt has an account. Not an email. |
| `GroupId` | uuid | no | Set when the attempt has a group. |
| `SessionId` | uuid | no | Set when the attempt has a session. |
| `MemberId` | uuid | no | Set when the attempt has a member. Not a display name. |
| `Tracks` | integer | no | Set only on a succeeded `publish-catalog`. |
| `Topics` | integer | no | Set only on a succeeded `publish-catalog`. |
| `Designs` | integer | no | Set only on a succeeded `publish-catalog`. |
| `ExportedAt` | timestamptz | no | Null until the collector has accepted the export. Stays null while the collector is down. |

Forbidden in every column: password, email, display name, answer text, rubric note, score, session cookie, catalog page body.

Indexes:

- Unique on `Reference` plus `Attempt`, so the vote and the election can share a reference and a second `cast-vote` cannot. The index does not by itself keep one reference per submission. Before the first insert of a new submission, the generator treats any existing reference as a collision.
- `AcceptedAt` for the 14-day delete and for time-range reads.
- `ExportedAt` where null, for the retry loop.

### Relationships

- `AccountId`, `GroupId`, `SessionId`, and `MemberId` are copies of existing ids. They are not foreign keys that block deleting a group by the documented erase, and they are not foreign keys that delete activity when a member is removed. The activity row outlives a removed member until the 14-day job.
- One submission has one `Reference`. The election submission has two rows with that reference.

### State

1. The attempt finishes. The row is inserted with `ExportedAt` null. The person is shown the reference when the attempt was refused.
2. The exporter sends the row. On acceptance it sets `ExportedAt`. On failure it leaves `ExportedAt` null and the attempt's outcome is unchanged.
3. A later pass sends rows that are still null. Grafana then shows them.
4. Fourteen days after `AcceptedAt`, the retention job deletes the row. The command no longer prints it. Group and session rows stay.

An attempt the person has not been shown an outcome for has no `succeeded` row yet.

### Validation

- `Outcome` is only `succeeded` or `refused`.
- `Reason` is only null or `unknown-account`.
- `DurationMs` and `StorageWaitMs` are `>= 0`. `StorageWaitMs` is `<= DurationMs`.
- `Tracks`, `Topics`, and `Designs` are null unless `Attempt` is `publish-catalog` and `Outcome` is `succeeded`.
- A `health-ready` or `health-not-ready` row is written only when the in-memory status changes.
- `application-start` is written once per process start. `application-stop` is written on a clean stop. A kill that cannot write leaves the earlier rows.

## Health snapshot

Not a table. Held by `HealthMonitor`.

| Field | In memory | Page text | Rule |
| --- | --- | --- | --- |
| Readiness | `ready`, `not-ready` | `ready`, `not ready` | `ready` only when the latest probe reached PostgreSQL. |
| Stored data | `reachable`, `down` | `reachable`, `down` | Named on the status page when down. |
| Use | `local-use-only`, `ready-for-a-group` | `local use only`, `ready for a group` | `ready-for-a-group` only when readiness is `ready` and none of the documented local-only settings still match. |
| Checked at | UTC timestamp | That same UTC timestamp | Shown on `/status`. |

The hyphenated values stay in memory. `/status` renders the page-text column.

The snapshot includes no group name, member name, email, answer, score, password, or catalog text.

## Local-only settings

Compared, never rendered:

| Setting | Printed default |
| --- | --- |
| Database password | `quorum` |
| Collector password | `quorum` |
| Grafana admin password | `quorum` |
| Prometheus password | `quorum` |
| Public base URL host | `localhost` or `127.0.0.1` |

Loki and Tempo have no password field.
