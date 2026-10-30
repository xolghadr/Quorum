# Contract: Activity record and the machine command

The stored shape is [data-model.md](../data-model.md). This contract is what a person sees and what the command prints.

## Refusal page

When a register, sign-in, sign-out, group, member, catalog publish, session, attendance, or vote attempt is refused, the page shows the same `Reference` as the stored row. The reference contains no email, password, or answer text. The page does not show another group's data and does not show a stack dump.

A sign-in for an unknown account stores `Reason=unknown-account` and does not store the typed email or the password. A wrong password for a known account stores `AccountId` and does not store the password. The page copy for those two failures stays the message the product already shows.

A vote that also stores a facilitator writes `cast-vote` and `elect-facilitator` with one reference. Looking up that reference returns those two rows and no other submission.

## Command

From a debug session, in `src/Quorum.App`:

```text
dotnet run -- activity <reference>
```

From the self-contained deployment:

```text
docker compose exec quorum dotnet Quorum.App.dll activity <reference>
```

`docs/run.md` names these as the one command for each path. The command reads `activity_records`. It does not call Grafana.

Exit 0 prints every row with that reference, oldest first, one block each:

```text
reference: <reference>
attempt: <attempt>
outcome: succeeded|refused
accepted_at: <utc>
duration_ms: <n>
storage_wait_ms: <n>
account_id: <uuid or ->
group_id: <uuid or ->
session_id: <uuid or ->
member_id: <uuid or ->
tracks: <n or ->
topics: <n or ->
designs: <n or ->
reason: <unknown-account or ->
```

Exit 1 prints `not found` when the reference is unknown or the row is older than 14 days. The output never contains a password, email, display name, answer, rubric note, score, or catalog page body.

A second self-contained start is not this command. Debug `dotnet run` while the tool containers are up is not a second deployment.
