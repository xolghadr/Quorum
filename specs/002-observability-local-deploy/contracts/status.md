# Contract: Status page

`GET /status` is anonymous. It does not redirect to sign-in. Quorum pages do not link to Grafana, Loki, Prometheus, Tempo, or the collector.

The page shows only:

| Line | Values |
| --- | --- |
| Readiness | `ready` or `not ready` |
| Stored data | `reachable` or `down` |
| Use | `local use only` or `ready for a group` |
| Checked at | UTC timestamp of the latest probe |

In-memory names use hyphens, as in [data-model.md](../data-model.md): `not-ready`, `local-use-only`, and `ready-for-a-group`. The page renders the spaced phrases in the table above.

`ready` appears only when stored data is reachable. `ready for a group` appears only when readiness is `ready` and the database password, collector password, Grafana password, Prometheus password, and public base URL host are all different from the defaults in [data-model.md](../data-model.md). Otherwise the use line is `local use only`.

While stored data is down, readiness is `not ready`, stored data is `down`, and the use line is not `ready for a group`. A copy that still has a local-only setting also says `local use only`.

The body contains none of: group name, member name, display name, email, answer text, rubric note, score, password, session cookie, catalog page text, Grafana URL.

A repeated probe that does not change readiness adds no activity row. A change writes one `health-ready` or `health-not-ready` row.

Stopping Grafana or the collector does not by itself make this page say `not ready`.
