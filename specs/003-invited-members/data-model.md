# Data Model: Invited Members

Stored in the existing PostgreSQL database. Identifiers stay the Guid values the app already uses for accounts, groups, members, and sessions.

## Invitation

A pending place in one group for one email. It is not a message.

| Field | Meaning |
| --- | --- |
| Id | New Guid. |
| GroupId | The group being offered. |
| EmailKey | The invited address, trimmed and lowercased, at most 320 characters. |
| InvitedByAccountId | The accepted member who created it. |
| CreatedAt | When it was created. |
| AcceptedAt | Set when a membership is created from it. Null before that. |
| DeclinedAt | Set when the invited person declines. |
| CancelledAt | Set when the inviter or the group creator cancels it. |

Exactly one of `AcceptedAt`, `DeclinedAt`, and `CancelledAt` may be set. All null means pending.

Uniqueness: one pending row per `(GroupId, EmailKey)`. A declined or cancelled email may be invited again as a new row.

Relationships: `GroupId` references `Groups`. `InvitedByAccountId` references `Accounts`. Acceptance creates one `Membership` and does not store a second link beyond `AcceptedAt`.

## Membership (existing, rules change)

`AccountId` stays nullable only so old name-only rows can remain. Every membership created by this feature sets `AccountId`. A row with `AccountId` null and `RemovedAt` null is not a member: it is hidden from the member list, the cap, attendance, voting, and election.

Active account memberships keep the unique indexes already on `(GroupId, DisplayNameKey)` and `(GroupId, AccountId)` where `RemovedAt` is null. A new member's display name is 1 to 40 characters and must not match an active `DisplayNameKey` in that group, including a leftover name-only row.

The group creator's membership is unchanged: it is created with the group and cannot be removed.

## Cap

`active account memberships + pending invitations <= 8`.

The creator is one of the active account memberships. Name-only rows are not in the sum.

## Session (existing, create rule changes)

`CreatedByAccountId` is the person who chose the topics: the group creator, or the facilitator who planned this session. `FacilitatorMembershipId` is still the person elected inside this session, and it is not copied onto the group.

A session is inserted only together with 2 or 3 `SessionTopic` rows. Distinct topic ids, no worked design, same pick-count rules as today: a topic newly included increments that group's `TopicProgress` once, a topic removed from a replacement does not decrement, and the same topic is not counted twice in one session.

Replacement is allowed only when `CreatedByAccountId` is the signed-in account and `FacilitatorMembershipId` is null.

Who may insert a session:

- The group creator, for the first session and for any later session.
- Otherwise, the active membership that is `FacilitatorMembershipId` on the highest-numbered session in that group which already has a facilitator.

An ordinary member is refused. A facilitator who has left is not active, so only the group creator can create the next one.

A session is inserted only when the group has no session, or when every existing session already has a facilitator. While one session has `FacilitatorMembershipId` null, another insert is refused and no pick count changes. Two inserts submitted together store one session. The race is closed by a filtered unique index on `Sessions (GroupId)` where `FacilitatorMembershipId` is null. Add that index only after confirming each existing group has at most one such row. Do not delete a session to make the index fit.

## Attendance, voters, and votes (existing tables, new commands)

`SessionAttendance` is one row per membership that marked themselves present. The command accepts only the signed-in account's own membership id.

`SessionVoter` is still copied from attendance at the first vote. Later attendance does not add a voter. A voter who marks themselves absent after voting stays in `SessionVoter`, and their vote stays.

Leave or removal:

- No vote yet, and a `SessionVoter` row exists: delete that voter row. Do not delete `SessionVote` rows that name them as the candidate.
- Already voted: keep the voter row and the vote.
- Before the first vote: delete their `SessionAttendance` row if present. They cannot mark themselves present again unless they accept a new invitation.
- After the change, if fewer than two `SessionVoter` rows remain, leave `FacilitatorMembershipId` null. If every remaining voter has a vote and at least two remain, store the facilitator with the same tie rule as today.

`SessionVote` is inserted only when `VoterMembershipId` is the signed-in account's membership, that id is in `SessionVoter`, and the candidate is in `SessionVoter`.

## State: invitation

```text
pending --accept--> accepted (membership created)
pending --decline--> declined
pending --cancel--> cancelled
declined --invite again--> new pending row
cancelled --invite again--> new pending row
```

Accept requires a free display name. A bad name leaves the row pending. Accept when active account memberships are already 8 leaves the row pending.

## State: member in a live voting set

```text
in SessionVoter, no vote --leave or remove--> dropped from SessionVoter
in SessionVoter, vote stored --leave or remove--> stays a voter, loses group access
```

## What is not stored

No mailbox id, no message body, and no standing facilitator on the group. Email keys are not written to activity rows or logs.
