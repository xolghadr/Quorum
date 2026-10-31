# Research: Invited Members

## Decision: Amend the constitution to 1.3.0 before implementation

**Rationale**: The spec requires every voting member to have an account, each person to act only for themselves, and the elected facilitator to plan the following session. Constitution 1.2.0 still allowed a display name with no account, told one person to seat the room, and had the facilitator set the topics of the session they were elected in. A plan that ignored that would fail the constitution gate. Version 1.3.0 is a minor amendment, the same kind as 1.2.0. Quorum of two, the 2-to-8 room, and no standing facilitator stay. `specs/001-group-catalog` is named in the migration note and is superseded on those points by this feature. Its file is not rewritten here.

**Alternatives considered**: Leave 1.2.0 and record a complexity exception. Rejected because the constitution overrides conflicting specs, so the feature could not ship. A major bump was rejected because quorum of two is unchanged and 1.2.0 already used a minor bump for the facilitator-topic rule.

## Decision: Store an invitation by normalized email, not by account id

**Rationale**: The spec allows an invitation before that email has an account. `Account.NormalizeEmail` already trims and lowercases. The invitation stores that key. When the person registers or signs in, the home query matches `Account.Email` to pending rows. No mail library is added. Creating the row is the whole delivery.

**Alternatives considered**: Refuse unknown emails. The spec answer rejects that. Store the raw typed email. Rejected because letter case must not hide the invitation. Create the account at invite time. Rejected because no password exists yet and no message can carry one.

## Decision: The home is `/groups`, and sign-in lands there when an invitation is pending

**Rationale**: The product has no separate dashboard. `/groups` already lists the signed-in account's groups. Pending invitations go at the top of that page, with accept and decline. Today, one group sends sign-in to `/catalog`, which would hide a new invitation. If any pending invitation exists, sign-in goes to `/groups` instead. With none, the current landing stays.

**Alternatives considered**: A new `/dashboard` route. Rejected because it duplicates `/groups`. Show invitations only on `/catalog`. Rejected because an account with no group never opens the catalog.

## Decision: Cap is active account memberships plus pending invitations

**Rationale**: The creator is one active membership with an account, so the creator is not counted a second time. Name-only rows have a null `AccountId` and are not members, so they do not count. The check runs again on accept. Accepting replaces one pending row with one membership, so a normal accept does not grow the count. A race that would pass 8 refuses the accept and leaves the invitation pending.

**Alternatives considered**: Count every membership row, including name-only seats. Rejected because those seats are not members and would shrink the real cap. Count only accepted members and allow unlimited pending invites. Rejected by the spec.

## Decision: Keep name-only rows, and keep the display-name unique index

**Rationale**: Past sessions point at those membership ids. Deleting the rows would break the names on old attendance and votes. New attendance, voting, member lists, and the cap ignore `AccountId == null`. The unique index on `(GroupId, DisplayNameKey)` where `RemovedAt` is null still includes those rows. Accepting a display name that a leftover seat already uses is refused as "already used". That is stricter than "current account members only" and avoids a special delete path.

**Alternatives considered**: Delete name-only rows in a migration. Rejected because session history would lose the name. Auto-remove the name-only row when an account accepts that name. Rejected because two different people could be collapsed into one history.

## Decision: Replace the attendance list post with one member marking themselves

**Rationale**: `SessionAttendance` already stores one row per membership. The page stops posting other members' ids. The signed-in account's active membership in the open group is the only id the command accepts. Before the first vote, the attendance list is whoever has a row. The first vote still copies those rows into `SessionVoter`. A later arrival still gets an attendance row and does not enter `SessionVoter`.

**Alternatives considered**: Keep the checkbox list and only change who may submit it. Rejected because the spec forbids one member marking another present.

## Decision: Leave or removal drops an unvoted voter out of `SessionVoter`

**Rationale**: `SessionVoter` is the set that must finish. If that member has no `SessionVote`, delete their `SessionVoter` row. Existing `SessionVote` rows stay, including votes whose candidate is the departed member, so that person can still be the stored facilitator. They then cannot plan the next session, which `FR-022` already gives to the group creator. If fewer than two `SessionVoter` rows remain, do not set `FacilitatorMembershipId`. If they already have a vote, leave both the voter row and the vote. Marking oneself absent after voting stays the old rule and does not delete the voter row.

**Alternatives considered**: Block leave until the vote is cast. Rejected by the spec. Void ballots for the departed member. Rejected because votes already cast stay.

## Decision: Every new session is created with 2 or 3 topics, by the creator or the latest elected facilitator

**Rationale**: Today a later session is created empty by any member, and topics are set after election. The spec moves topic choice to creation time. `SessionCommands.CreateAsync` requires a valid topic list for every session, not only the first. The actor must be the group's `CreatorAccountId`, or the active membership that is `FacilitatorMembershipId` on the highest-numbered session in the group that already has a facilitator. Anyone else is refused. Replacing topics stays allowed only for `CreatedByAccountId`, and only while that session's own `FacilitatorMembershipId` is null. After election, that list is frozen. The new facilitator uses create, not replace, for the following session. The group creator and that facilitator may each attempt the next create. Only one session may have a null facilitator at a time, so the second attempt stores nothing.

**Alternatives considered**: Let any past facilitator keep creating sessions. Rejected because "the current session" in the spec is the latest elected one, and several creators would race. Let ordinary members create an empty session. Rejected by the clarified topic rule.

## Decision: Record the same activity shape for the new attempts

**Rationale**: `specs/002-observability-local-deploy` already stores one row per accepted or refused command. New attempt names are `invite-member`, `cancel-invitation`, `accept-invitation`, `decline-invitation`, `leave-group`, and `mark-attendance`. The existing `save-attendance` list command goes away. Rows still omit email, display name, and vote text. The reference still returns on the refusal page.

**Alternatives considered**: A second audit table. Rejected because one database and the existing activity record already cover it.
