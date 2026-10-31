# Quickstart: Invited Members

Proves the feature against a running Quorum and PostgreSQL. Page behavior is [contracts/pages.md](./contracts/pages.md). Stored shape is [data-model.md](./data-model.md).

## Prerequisites

- The app and PostgreSQL from [docs/run.md](../../../docs/run.md).
- The catalog published once (`publish-catalog`), so a session can choose topics.
- Two extra accounts besides the group creator. The third can be registered after the invitation exists. Check 5 needs one further accepted member, so the creator and the facilitator who planned the next session can both stay away while two other members hold it.

## Checks

1. **Invite with no message.** As an accepted member who is not the creator, invite an email that has no account. Confirm no mail is sent. On `/members`, that inviter sees the pending invitation, including the email, and can cancel it. Their `/groups` page does not list it. `/groups` lists an invitation only for the signed-in account's own email. Sign in as an unrelated account and confirm that invitation is absent from `/groups` and from `/members`.
2. **Register, then see it.** Register the invited email and sign in. Land on `/groups`. The invitation shows the group name and the inviter's display name, and not the group's sessions. Accept with a display name. The group opens. A second browser signed in as that account is the same one member.
3. **Cap and bad invites.** Fill the group to 8 counting pending invitations. The next invite is refused. Inviting yourself, an existing member, or a duplicate pending email is refused. Decline one invitation and confirm the email can be invited again.
4. **Hold without the creator.** As the creator, create the first session with 2 or 3 topics, then sign out. As two other accepted members, open that session, mark only yourselves present, and vote. A facilitator is elected. The creator is not present and has no vote. Neither of those members can change the topics or create a session.
5. **Plan the next session.** As the elected facilitator, still with the creator signed out, create the next session with 2 topics. Another member sees those topics and the pick counts. Replacing the list before that next session elects changes the topics once. While that new session has not elected a facilitator, a second create by the creator or by that facilitator is refused and the pick counts stay as they were. Then the creator and the facilitator who planned it both sign out and are not marked present. Two other accepted members hold that session, vote, and elect a new facilitator. The topic list stays as the planner left it. The creator and the previous facilitator are not present and have no vote.
6. **Leave during voting.** With three voters and only one vote stored, remove or leave one member who has not voted. The other two can finish and elect a facilitator. With only two voters, the same leave elects nobody, and the stored vote remains. The departed account gets not found on that session.
7. **Isolation.** While one group is open, the other group's members, invitations, sessions, and pick counts are not on the page.

## Tests

From the repository root, after the implementation exists:

```text
dotnet test tests/Quorum.App.Tests --filter "FullyQualifiedName~Invitation|FullyQualifiedName~OwnAttendance|FullyQualifiedName~SessionCreate"
```

The invitation and session-create tests need PostgreSQL. A pure rule test covers display-name length, the cap count, and "fewer than two voters elects nobody" without the database.

## Done

- Checks 1 through 7 match the contract.
- `dotnet test` for the filtered names passes.
- A name-only seat left from before this feature does not appear on Members and cannot be marked present.
