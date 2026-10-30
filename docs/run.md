# Run Quorum

Docker is the first prerequisite. Install Docker, then start the Docker daemon. Result: `docker version` prints a client and a server version.

Quorum and the inspection tools run from this repository. The group uses the Quorum address. Grafana, Loki, Prometheus, Tempo, and the collector each have their own port. Loki and Tempo have no password.

## Local path

1. From the repository root, run `deploy/up.sh`.
   Result: the app, PostgreSQL, the collector, Loki, Prometheus, Tempo, and Grafana are running. Inside the network Quorum sends to `http://otel-collector:4318`. It does not send straight to Grafana, Loki, Prometheus, or Tempo.

2. Open `http://localhost:8080/status`.
   Result: the page says `ready` and `local use only`. Stored data says `reachable`.

3. Register one account and read the short reference on a refused attempt, or the stored reference for the register row.
   Result: `docker compose exec quorum dotnet Quorum.App.dll activity <reference>` prints that `register` row. The command does not open Grafana.

4. Create a group, then run `deploy/stop.sh`, then run `deploy/up.sh` again.
   Result: the group is still there, and today's activity row is still printed. `deploy/stop.sh` does not delete volumes.

5. Run `deploy/up.sh` again while that stack is already running.
   Result: the script prints `A self-contained deployment is already running. Refusing to start a second one. Stored data was not erased.` and exits non-zero. Volumes stay.

6. Run `deploy/erase.sh` only when you mean to wipe this stack.
   Result: project `quorum` is removed with its volumes. The next `deploy/up.sh` has no previous group. `deploy/erase.sh` is not part of stop or start.

These are the local addresses. Each tool is on its own port, which is not the address the group uses for Quorum.

| Service | Port |
| --- | --- |
| collector `otel-collector` | `127.0.0.1:4318` |
| Grafana `grafana` | `127.0.0.1:3000` |
| Loki `loki` | `127.0.0.1:3100` |
| Prometheus `prometheus` | `127.0.0.1:9090` |
| Tempo `tempo` | `127.0.0.1:3200` |
| PostgreSQL `postgres` | `127.0.0.1:5432` |

The app listens on port `8080`. Sign in to Grafana at `http://127.0.0.1:3000` with the admin password from `deploy/local.env`. Anonymous Grafana access is off. A certificate vendor is out of scope.

## Deploy path

This is the same Compose file on one machine. It does not ask for a second machine, a paid account, a cluster, or a tool hosted outside this Compose project.

1. Copy `deploy/local.env` and replace every printed default: `POSTGRES_PASSWORD`, `COLLECTOR_PASSWORD`, `GRAFANA_ADMIN_PASSWORD`, and `PROMETHEUS_PASSWORD`. Set `PUBLIC_BASE_URL` to an absolute URL whose host is not `localhost` or `127.0.0.1`.
   Result: Loki still has no password and Tempo still has no password.

2. Run `deploy/up.sh`.
   Result: `/status` says `ready` and `ready for a group`. The group opens the app's address. Grafana stays on `127.0.0.1:3000` unless you give Grafana its own domain later. That domain is not the group's Quorum address. The same is true for the collector, Loki, Prometheus, Tempo, and PostgreSQL: each one keeps the service name and the localhost port in the table above, or another domain that is not the group's Quorum address.

3. Leave any one of those four passwords as `quorum`, or leave `PUBLIC_BASE_URL` on `localhost` or `127.0.0.1`, and start again.
   Result: `/status` stays on `local use only`.

4. Run `deploy/stop.sh` and then `deploy/up.sh`.
   Result: the group and today's activity row remain. Erase is still the separate `deploy/erase.sh` step.

## Failure notes

| What you see | What to do |
| --- | --- |
| Docker is not installed, or the daemon is stopped | Install Docker or start the daemon, then run the same step again. |
| `/status` says stored data `down` | PostgreSQL is not ready. `docker compose -p quorum -f docker-compose.yml ps` shows the `postgres` service. Wait until it is healthy, then open `/status` again. Do not erase the volume. |
| The app port `8080`, or a tool port in the table above, is already in use | Stop the other listener on that port. Stored data is not erased. Then run the start step again. |
| Grafana shows nothing and `docker compose exec quorum dotnet Quorum.App.dll activity <reference>` prints the row | The collector is down, or the collector password does not match. The attempt already finished. Start the collector again. The same row is sent. You do not register a second time. |
| `/status` stays on `local use only` after a deploy | One printed default is still in `deploy/local.env`. Replace the database password, the collector password, the Grafana admin password, the Prometheus password, and set `PUBLIC_BASE_URL` to a host other than `localhost` or `127.0.0.1`. |
| The process was killed and no `application-stop` row appears | A forced kill may not write `application-stop`. Earlier rows remain. |

## Debug path

This session attaches to containers that are already running. It is not a second self-contained deployment, and `deploy/up.sh` does not refuse it. `deploy/erase.sh` does not remove this volume.

1. From the repository root, run `docker compose -f docker-compose.debug.yml up`.
   Result: PostgreSQL and the tool set start. The app does not start. Host ports are `127.0.0.1` only: PostgreSQL `5432`, collector `4318`, Grafana `3000`, Loki `3100`, Prometheus `9090`, Tempo `3200`. The services are `postgres`, `otel-collector`, `loki`, `prometheus`, `tempo`, and `grafana`.

2. From `src/Quorum.App`, run `dotnet run`.
   Result: the app uses `http://127.0.0.1:4318` and the database on `localhost` port `5432`. It does not start those containers. It does not call the service name `otel-collector`. A closed collector port does not change that address.

3. Register one account.
   Result: after the collector accepts the row, Grafana at `http://127.0.0.1:3000` shows that reference on the Quorum dashboard. The admin password is the printed default `quorum`.

4. Stop only the collector container. Register again.
   Result: the attempt still finishes. `dotnet run -- activity <reference>` from `src/Quorum.App` prints the row. `/status` can still say `ready`. The page does not wait on the collector.

5. Start the collector again.
   Result: that same row appears in Grafana. You do not register a third time.

The debug addresses match the local table: collector `otel-collector` on `127.0.0.1:4318`, Grafana `grafana` on `127.0.0.1:3000`, Loki `loki` on `127.0.0.1:3100`, Prometheus `prometheus` on `127.0.0.1:9090`, Tempo `tempo` on `127.0.0.1:3200`, and PostgreSQL `postgres` on `127.0.0.1:5432`. The app reaches the collector at `http://127.0.0.1:4318`, not at `http://otel-collector:4318`.
