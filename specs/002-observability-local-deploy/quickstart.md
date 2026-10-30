# Quickstart: Transparent Local Operations

Validation for this feature. The operator instructions are [contracts/guide.md](./contracts/guide.md) and, once written, `docs/run.md`. Shapes are in [data-model.md](./data-model.md). Do not treat this file as the operator guide.

## Preconditions

- .NET 10 SDK
- Docker, for the Compose checks
- PostgreSQL 17 available the way `tests/Quorum.App.Tests` already reaches it

## Automated checks

From the repository root:

```text
dotnet test tests/Quorum.App.Tests/Quorum.App.Tests.csproj
```

Those tests must cover, with a collector double rather than a live Grafana:

| Check | Expected |
| --- | --- |
| Register, wrong-password sign-in, unknown-email sign-in | One row each. Unknown email stores `unknown-account` and no email. No row contains the password. |
| Vote that elects a facilitator | `cast-vote` and `elect-facilitator` share one reference. |
| Refused self-vote in a session of three or more | One `refused` row, no `succeeded` row, and the page shows that reference. |
| `GET /status` with no cookie | `ready` or `not ready` without a redirect. Body has no group name, email, or password. |
| Database password still `quorum` | Status use line is `local use only`. |
| All four secrets and the public host replaced | Status use line is `ready for a group` when the probe succeeds. |
| Exporter throws | The attempt still returns, `exported_at` stays null, and a later successful export sets it. |
| Rows older than 14 days | The retention job deletes them. A group row from the same day remains. |
| Quorum pages | No link to Grafana, Loki, Prometheus, Tempo, or the collector. |

`dotnet test` does not start the Compose stack.

## Self-contained path

Follow `docs/run.md` once it exists, or [contracts/guide.md](./contracts/guide.md) until then.

1. `deploy/up.sh`
2. Open `http://localhost:8080/status`. Expect `ready` and `local use only`.
3. Register. Copy the reference from the activity command. Expect one `register` block, outcome `succeeded`.
4. Sign in to Grafana at `http://127.0.0.1:3000` with the printed admin password. Expect that reference on the Quorum dashboard.
5. Open Grafana with a wrong password. Expect no dashboard data.
6. `deploy/stop.sh`, then `deploy/up.sh`. Expect the same group and the same reference.
7. Run `deploy/up.sh` again while it is up. Expect the refusal text and the same data afterward.

## Debug path

1. `docker compose -f docker-compose.debug.yml up`
2. `dotnet run --project src/Quorum.App`
3. Register. Expect the app to use `127.0.0.1:4318`, and expect the row in Grafana.
4. Stop the collector container only. Register again. Expect the page to finish, `/status` still able to say `ready`, and `dotnet run --project src/Quorum.App -- activity <reference>` to print the row.
5. Start the collector. Expect the second reference in Grafana without registering a third time.

## Deploy path

Copy the env file, replace the database password, collector password, Grafana password, Prometheus password, and set the public host to something other than `localhost`. Run `deploy/up.sh`. Expect `/status` to say `ready for a group`. Leave one default in place and expect `local use only`.

Loki and Tempo stay without a password. The local Compose file publishes their ports on `127.0.0.1`. A tool may also be reached on another domain that is not the group's Quorum address.
