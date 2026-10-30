# Contract: Operator guide

The guide file is `docs/run.md`. A person who has not worked on Quorum can follow it without the author. Steps are numbered. Each step states the result.

## Self-contained local path

1. Install Docker. The guide names that prerequisite first.
2. From the repository root, run `deploy/up.sh`.
3. Result: the app, PostgreSQL, the collector, Loki, Prometheus, Tempo, and Grafana are running. `GET /status` says `ready` and `local use only`. Inside the network, Quorum sends to `http://otel-collector:4318`.
4. Register one account. `docker compose exec quorum dotnet Quorum.App.dll activity <reference>` prints that `register` row.
5. Stop with `deploy/stop.sh` and start again with `deploy/up.sh`. The group is still there, and today's activity row is still printed.
6. `deploy/erase.sh` is a separate step. After it, the next start has no previous group.

A second `deploy/up.sh` while that stack is running prints the guide's refusal and does not delete volumes.

## Deploy path

Same Compose file, one machine, env file with every local-only setting replaced: database password, collector password, Grafana password, Prometheus password, and a public base URL whose host is not `localhost` or `127.0.0.1`. Loki and Tempo still have no password.

Result: `/status` says `ready` and `ready for a group`. The group opens the app's address. The Compose file publishes Grafana on `127.0.0.1:3000`. Each tool is on its own domain and port, which may be that loopback address or another domain, and is not the address the group uses for Quorum. A certificate vendor is out of scope. The path does not ask for a second machine, a paid account, or a tool hosted outside this Compose project.

Leaving any local-only setting unchanged keeps `/status` on `local use only`.

## Development debug path

1. `docker compose -f docker-compose.debug.yml up`. This starts PostgreSQL and the tool set. It does not start the app.
2. Host ports are `127.0.0.1` only: Postgres `5432`, collector `4318`, Grafana `3000`, Loki `3100`, Prometheus `9090`, Tempo `3200`.
3. From `src/Quorum.App`, `dotnet run` uses `http://127.0.0.1:4318` and the localhost database. It does not start those containers and does not call `otel-collector`.
4. A register row shows in Grafana after the collector accepts it.
5. Stop the collector and register again. The attempt still finishes. `dotnet run -- activity <reference>` prints the row. `/status` can still say `ready`.
6. Start the collector again. That row appears in Grafana without a second register.

## Failure notes

| What the operator sees | Next action the guide gives |
| --- | --- |
| Docker is not installed or the daemon is stopped | Install or start Docker, then run the step again. |
| `/status` says stored data `down` | The Postgres container is not ready. The guide says which command shows it. |
| The app port or a tool port is already in use | The guide names the port and says to stop the other listener. Data is not erased. |
| Grafana shows nothing and the activity command prints the row | The collector is down or the collector password does not match. The session is not blocked. |
| `/status` stays on `local use only` after a deploy | At least one printed default is still in the env file. The guide lists the four secrets and the address. |
