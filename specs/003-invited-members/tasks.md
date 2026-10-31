---
description: "Task list for invited members"
---

# Tasks: Invited Members

**Input**: Design documents from `/specs/003-invited-members/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/pages.md, quickstart.md

**Tests**: Included. `quickstart.md` makes `dotnet test` the acceptance check, and the constitution requires test-first coverage for session state and group scoping. Write the new tests for a story first and confirm they fail before the implementation tasks for that story.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested on its own.

**Out of scope for every task**: a mail library or any outbound message, a second process, a second database, a repository layer, MediatR, an identity-provider SDK, a SPA, SSO, billing, Telegram, media, the 45-minute runner, pairing, and rubric scores. Do not rewrite `specs/001-group-catalog`. Do not revert constitution 1.3.0. `Program.cs` stays extension calls, then `app.Run()`. Handlers use `QuorumDbContext` directly. Logs and activity rows must not contain an email address, a display name, a password, answer text, or a vote.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel when the tasks use different files and do not depend on an unfinished task. T002 and T004 are one wave. T008 and T009 are the exception: their `[P]` applies only to the wave that starts after T007. T010 and T011, T013 and T014, and T017 and T018 are new test files and may start once Phase 2 is done, including while another story's implementation is still open
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Setup, foundational, and polish tasks have no story label

## Path Conventions

- Web app: `src/Quorum.App/`
- Tests: `tests/Quorum.Groups.Tests/`, `tests/Quorum.Sessions.Tests/`, `tests/Quorum.App.Tests/`
- One ASP.NET Core process, Razor Pages under `Features/`, one `QuorumDbContext`, no repository layer, no MediatR

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Keep the solution the plan already describes. No new project and no new package.

- [X] T001 Confirm `src/Quorum.App/Quorum.App.csproj` gains no mail package and no identity-provider package. Leave the existing test projects `tests/Quorum.Groups.Tests/`, `tests/Quorum.Sessions.Tests/`, `tests/Quorum.Catalog.Tests/`, and `tests/Quorum.App.Tests/` as the only test projects. Serves the plan's primary dependencies: ASP.NET Core Razor Pages, Entity Framework Core, Npgsql, and the existing cookie sign-in

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The invitation row, the cap count, and the activity names every story records

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 [P] Create `Invitation` in `src/Quorum.App/Features/Groups/Invitation.cs`. Add `InvitationId` in `src/Quorum.App/SharedKernel/` by the same Guid wrapper pattern as `MemberId`. Quote the data model: `Id` is a new Guid. `GroupId` is the group being offered. `EmailKey` is the invited address, trimmed and lowercased, at most 320 characters. `InvitedByAccountId` is the accepted member who created it. `CreatedAt` is when it was created. `AcceptedAt` is set when a membership is created from it and is null before that. `DeclinedAt` is set when the invited person declines. `CancelledAt` is set when the inviter or the group creator cancels it. Exactly one of `AcceptedAt`, `DeclinedAt`, and `CancelledAt` may be set. All null means pending. In the same file, add `GroupCap.Fits(int activeAccountMemberships, int pendingInvitations)` using `Group.MemberMax`. Quote: `active account memberships + pending invitations <= 8`. The creator is one of the active account memberships. Name-only rows are not in the sum. Active account memberships are rows with `AccountId` not null and `RemovedAt` null. Pending invitations are rows with `AcceptedAt`, `DeclinedAt`, and `CancelledAt` all null
- [X] T003 Map `Invitation` from T002 in `src/Quorum.App/Features/Groups/GroupConfiguration.cs` with `InvitationConfiguration`, and add `DbSet<Invitation> Invitations` on `src/Quorum.App/Infrastructure/Persistence/QuorumDbContext.cs`. `ApplyConfigurationsFromAssembly` already loads that configuration. `EmailKey` is required and has max length 320. Foreign keys: `GroupId` references `Groups`, `InvitedByAccountId` references `Accounts`, both `DeleteBehavior.Restrict`. Add a unique filtered index on `(GroupId, EmailKey)` where `AcceptedAt`, `DeclinedAt`, and `CancelledAt` are all null, so one pending row exists per `(GroupId, EmailKey)`. Add a check that zero or one of those three timestamps is set. Add an EF Core migration under `src/Quorum.App/Infrastructure/Persistence/Migrations/`. The migration creates `Invitations` only. It does not delete membership rows whose `AccountId` is null. It does not add a second database
- [X] T004 [P] Add these attempt names to `ActivityRecord.Attempts` in `src/Quorum.App/Infrastructure/Logging/ActivityRecord.cs`: `invite-member`, `cancel-invitation`, `accept-invitation`, `decline-invitation`, `leave-group`, `mark-attendance`. Leave the existing names, including `save-attendance` and `add-member`, in the set until the story tasks stop recording them. A new row still forbids a password, an email, a display name, answer text, a vote, a rubric note, a score, a session cookie, and catalog page body

**Checkpoint**: `Invitations` migrates on startup. The cap helper exists. The new attempt names are legal. No page invites anyone yet

---

## Phase 3: User Story 1 - Join a group from an invitation (Priority: P1) 🎯 MVP

**Goal**: An accepted member invites an email. No message is sent. That person sees the invitation on `/groups` after they register or sign in, accepts with a display name, and opens the group

**Independent Test**: With three registered people, an accepted member who is not the group creator invites the third person's email while the creator is signed out. No message arrives. The third person signs in, sees the invitation on `/groups` with the group name and the inviter's display name, accepts it with a display name, and then sees that group. A person who was not invited sees none of it. The inviting member cannot remove the new member. The group creator can.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T005 [P] [US1] Add failing pure rule tests in `tests/Quorum.Groups.Tests/InvitationRulesTests.cs`. Do not open `PostgresDatabase`. Display name uses `Membership.DisplayNameAccepted`: blank and a name longer than 40 characters are refused, and a trimmed name of 1 character and of 40 characters is accepted. `GroupCap.Fits` is true for 7 active account memberships plus 1 pending invitation and for 8 plus 0, and false for 8 plus 1. The method has no name-only argument. Quote: `active account memberships + pending invitations <= 8`
- [X] T006 [P] [US1] Add failing invitation tests in `tests/Quorum.App.Tests/InvitationTests.cs` so `FullyQualifiedName~Invitation` matches `specs/003-invited-members/quickstart.md`. Use the web application factory and PostgreSQL. An accepted member who is not the creator invites an email that has no account, while the creator is signed out, and no message is sent. Letter case in the email does not hide the invitation. After that person registers, sign-in lands on `/groups`. The page shows the group name and the inviter's display name, and shows none of that group's sessions, votes, members, or pick counts. Accepting a display name of 1 to 40 characters that is not already used opens the group and sets `AccountId` on the new membership. A blank name, a name longer than 40 characters, or a name already used in the group ignoring letter case leaves the invitation pending. Accepting when 8 active account memberships already exist leaves the invitation pending. The form refuses the inviter's own email, an email that already belongs to an active member, a second pending invitation for the same email in that group, and a blank email or an email `Account.EmailAccepted` rejects. A group at 8 counting active account memberships and pending invitations refuses another invite. Decline removes the invitation from the home and allows a new pending row for that email. The inviter can cancel their own pending invitation. An ordinary member cannot cancel an invitation they did not create. The group creator can cancel one that another member created. The inviter sees the invited email and cancel on `/members`. The group creator sees every pending email in the group on `/members`. Another accepted member sees neither the row nor the email on `/members` or `/groups`. The inviter's `/groups` page does not list the invitation they sent. While the invitation is pending, the invited account requests `/members`, `/sessions`, `/sessions/{sessionId}` for a session in that group, and `/catalog/topics/{topicKey}`. The session address is not found and shows no session content. The other three responses show none of that group's member names, sessions, votes, or pick counts. With no membership, `/members`, `/sessions`, and the topic page follow the existing group gate and do not open that group. An unrelated account sees none of the invitation and none of the group. One account that is an accepted member of two groups, with a different display name in each, opens one group. While that group is open, the page shows none of the other group's members, invitations, sessions, votes, or pick counts. Activity rows for `invite-member`, `accept-invitation`, `decline-invitation`, and `cancel-invitation` store no email, no display name, and no vote. A refused attempt still returns the activity reference. With no pending invitation, sign-in keeps today's landing: no group opens `/groups/new`, one group opens `/catalog`, and more than one opens `/groups`

### Implementation for User Story 1

- [X] T007 [US1] Implement invite, cancel, accept, and decline in `src/Quorum.App/Features/Groups/InvitationCommands.cs`, and register the type from `src/Quorum.App/Infrastructure/Web/WebExtensions.cs`. `Program.cs` does not register it. Validate the email with `Account.EmailAccepted` and store `Account.NormalizeEmail` (trim, then `ToLowerInvariant`) in `EmailKey`. Refuse a blank email or an email `Account.EmailAccepted` rejects, the inviter's own email, an email that already belongs to an active account membership in that group, and an email that already has a pending invitation for that group. Refuse an invite when `GroupCap.Fits` would be false. Any accepted member may invite, including while the group creator is signed out. Cancel sets `CancelledAt` only for the inviter's own pending row or, when the actor is the group creator, for any pending row in the group. Decline sets `DeclinedAt` only for the signed-in account's own pending email. Accept requires `Membership.DisplayNameAccepted` and a `DisplayNameKey` (`ToLowerInvariant` of the trimmed name) that does not match an active membership in that group, including a leftover name-only row. A bad name leaves the row pending. When active account memberships are already 8, accept leaves the row pending. A successful accept sets `AcceptedAt`, inserts one `Membership` with `AccountId` set, and does not store a second link. A declined or cancelled email is invited again only as a new row. Record `invite-member`, `cancel-invitation`, `accept-invitation`, and `decline-invitation` through the existing activity writer. Do not write the email or the display name into the row or the log
- [X] T008 [P] [US1] Show pending invitations on `src/Quorum.App/Features/Groups/GroupList.cshtml` and load them in `src/Quorum.App/Features/Groups/GroupList.cshtml.cs`. Match `EmailKey` to `Account.NormalizeEmail` of the signed-in account, with `AcceptedAt`, `DeclinedAt`, and `CancelledAt` all null. List those rows above the account's groups. Do not list an invitation the signed-in account sent. Those rows belong on `/members`. Each row shows the group name and the inviter's display name in that group, and does not show that group's sessions, votes, members, or pick counts. Add `src/Quorum.App/Features/Groups/AcceptInvitation.cshtml` at `/groups/invitations/{invitationId:guid}/accept` and `src/Quorum.App/Features/Groups/DeclineInvitation.cshtml` at `/groups/invitations/{invitationId:guid}/decline`, with their page models in `src/Quorum.App/Features/Groups/InvitationPosts.cshtml.cs`. Accept posts a display name. A blank name, a longer name, or a name already used leaves the invitation pending and says the name was not accepted. A full group leaves the invitation pending and says the group is full. A successful accept opens that group the same way `src/Quorum.App/Features/Groups/GroupOpen.cshtml.cs` does: `SignInSession.SignInAsync` with that group id, then redirect to `/catalog`. In `src/Quorum.App/Features/Accounts/SignIn.cshtml.cs` and `src/Quorum.App/Features/Accounts/Register.cshtml.cs`, land on `/groups` when any pending invitation exists for that email. Otherwise keep today's landing, including register with no invitation still redirecting to `/groups/new`. A refusal shows the activity reference and does not show an email address
- [X] T009 [P] [US1] Replace the add-by-display-name form on `src/Quorum.App/Features/Groups/Members.cshtml` with an email invite that any accepted member can submit. `src/Quorum.App/Features/Groups/Members.cshtml.cs` lists only active memberships whose `AccountId` is not null, and posts the invite. Add `src/Quorum.App/Features/Groups/CancelInvitation.cshtml` at `/members/invitations/{invitationId:guid}/cancel`. On `Members.cshtml`, render a pending invitation and its email only for the member who created it and for the group creator. The inviter sees cancel on their own pending invitation. The group creator sees cancel on every pending invitation in the group. Another accepted member sees neither the row nor the email. Keep `POST /members/{memberId}/remove` for the creator only. `RosterCommands.AddAsync` in `src/Quorum.App/Features/Groups/RosterCommands.cs` refuses every new name-only seat, records `add-member` as refused, and stores no display name. Do not delete historical name-only rows. Leave `RenameAsync` in place for an active account membership the creator renames. That is the existing rename recorded in the spec assumptions and in `contracts/pages.md`. Do not add a new rename story, and do not offer rename for a name-only row. Update callers that expected `AddAsync` to succeed: `tests/Quorum.Groups.Tests/RosterTests.cs`, `tests/Quorum.Groups.Tests/AccountGroupTests.cs`, `tests/Quorum.Sessions.Tests/SessionRulesTests.cs`, `tests/Quorum.App.Tests/ActivityDurabilityTests.cs`, `tests/Quorum.App.Tests/ActivityRedactionTests.cs`, `tests/Quorum.App.Tests/ElectionActivityTests.cs`, and `tests/Quorum.App.Tests/NoToolLinkTests.cs`. Seed an account membership, or a historical name-only row written straight to `Memberships`, instead of calling `AddAsync`. Leave session create and the attendance-list assertions as they stand until T012 and T015

**Checkpoint**: User Story 1 works on its own. A second account can join from `/groups` without a message. Name-only seats are not members. Session rules are still the old ones

---

## Phase 4: User Story 2 - Hold a session without the group creator (Priority: P1)

**Goal**: Two accepted members mark only themselves present and cast only their own votes while the group creator is signed out. They elect one facilitator. The creator is not present and has no vote

**Independent Test**: The group creator creates a session and chooses its topics, then signs out. Two other accepted members open that session, mark themselves present, vote, and see one facilitator elected. The group creator's name is not present and has no vote. Neither member creates the session or changes its topics.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T010 [P] [US2] Add failing attendance and vote tests in `tests/Quorum.Sessions.Tests/OwnAttendanceTests.cs`. Seed account memberships directly so this story does not require the invite pages. `mark-attendance` accepts only the signed-in account's own membership id. Posting another member's id, the creator's id, or a name-only membership id changes no attendance row. Two accepted members can each mark themselves present while the creator is signed out. The creator is absent and has no vote unless the creator marked themselves. The first vote copies `SessionAttendance` into `SessionVoter` and refuses when fewer than 2 accepted members are present, leaving no `SessionVoter` rows. A later arrival gets an attendance row and does not enter `SessionVoter`. `CastVoteAsync` stores a vote only when `VoterMembershipId` is the signed-in account's membership, that id is in `SessionVoter`, and the candidate is in `SessionVoter`. A set of exactly two accepts a self-vote. A set of three or more refuses a self-vote. One account on two calls is one attendance row and one voter. A second vote by the same voter is refused. A voter who marks themselves absent after voting stays in `SessionVoter` and keeps the vote. An account that is not an accepted member gets not found and writes nothing. The activity attempt is `mark-attendance`, and the row stores no display name
- [X] T011 [P] [US2] Add failing page tests in `tests/Quorum.App.Tests/OwnAttendanceTests.cs` so `FullyQualifiedName~OwnAttendance` matches the quickstart filter. Two signed-in accepted members open a session the creator already gave topics, with the creator signed out. Each page shows one present control for that member and one vote form for that member after voting is open. The page does not render a checkbox list of the roster or a Record vote form for another person. Submitting another member's id changes nothing. A signed-out request for the session redirects to sign-in and shows no session content. An account that has not accepted a place in the group, including an account with only a pending invitation, gets not found and sees no session content

### Implementation for User Story 2

- [X] T012 [US2] Change `SaveAttendanceAsync` in `src/Quorum.App/Features/Sessions/Election.cs` so the command marks only the signed-in account's own active membership. The attempt name is `mark-attendance`. Stop recording `save-attendance`. Setting that member present inserts one `SessionAttendance` row. Setting that member absent deletes only that row. Other members' rows stay. A posted id that is not the signed-in membership, including a name-only row (`AccountId` null), changes nothing and is refused. The first accepted vote still copies current `SessionAttendance` into `SessionVoter`, and only for memberships with `AccountId` set and `RemovedAt` null. Fewer than 2 or more than 8 present members do not open a voting set. `CastVoteAsync` refuses a `VoterMembershipId` that is not the signed-in account's membership. Keep the existing self-vote, one-vote, late-arrival, and tie rules. Update the attendance and vote call sites in `src/Quorum.App/Features/Sessions/SessionPosts.cshtml.cs`, `src/Quorum.App/Features/Sessions/AttendancePost.cshtml`, `src/Quorum.App/Features/Sessions/SessionDetail.cshtml`, `src/Quorum.App/Features/Sessions/SessionDetail.cshtml.cs`, `tests/Quorum.Sessions.Tests/SessionRulesTests.cs`, `tests/Quorum.Groups.Tests/RosterTests.cs`, `tests/Quorum.App.Tests/ActivityDurabilityTests.cs`, `tests/Quorum.App.Tests/ElectionActivityTests.cs`, and `tests/Quorum.App.Tests/NoToolLinkTests.cs`. Each voter in those tests acts through their own account. Leave the later-session-created-empty assertions in `SessionRulesTests.cs` for T015. On the session page, show present and not present for the signed-in member only, and show one vote form only when that member is in `SessionVoter` and has not voted. The member list on that page is active account memberships

**Checkpoint**: Two account members can hold a session that already has topics while the creator is signed out. Creating the next session is still the old rule

---

## Phase 5: User Story 3 - The elected facilitator plans the next session (Priority: P1)

**Goal**: The facilitator of the highest-numbered session that already has a facilitator creates the following session and chooses its 2 or 3 topics. A facilitator of an earlier session does not keep that right. The group creator can still create a session and choose its topics when every existing session already has a facilitator. While any session has no facilitator, that create is refused. An ordinary member cannot. A session that already has topics can be held while its creator and the previous facilitator are away

**Independent Test**: After two members elect a facilitator while the creator is signed out, that facilitator creates the next session and puts two topics on it. Those topics show a pick count for the group. Later, two other accepted members hold that session without the facilitator and without the creator, and elect a new facilitator.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T013 [P] [US3] Add failing session-create tests in `tests/Quorum.Sessions.Tests/SessionCreateTests.cs`. Every create inserts the session together with 2 or 3 `SessionTopic` rows. Distinct topic ids, no worked design. One topic, four or more, a duplicate, a worked design, or an unknown id writes no session and does not change pick counts. The actor must be the group's `CreatorAccountId`, or the active membership that is `FacilitatorMembershipId` on the highest-numbered session in that group which already has a facilitator. An ordinary member is refused. The first session can be created only by the group creator, with its topics. The group creator may also create a later session with topics while signed in. The elected facilitator may create the following session while the creator is signed out, and each newly included topic's pick count increases by 1. A facilitator who has left is not active, so only the group creator can create the next session. A second create is refused while any session in the group has `FacilitatorMembershipId` null, for both the group creator and the facilitator who created the waiting session. The refused create writes no session and does not change a pick count. Two creates submitted together store one session and refuse the other. Replacement is allowed only when `CreatedByAccountId` is the signed-in account and that session's `FacilitatorMembershipId` is null. A topic added in the replacement is counted once. A topic removed from the list does not lose a count. The same topic is not counted twice in one session. After `FacilitatorMembershipId` is set, the topic list stays, including when someone other than the session creator submits a replacement. Another group's session id is not found and writes nothing. After the facilitator creates the following session, two other accepted members hold it while the group creator and that facilitator are both signed out and not marked present. They elect a new facilitator from the people present. The planned topics stay. The creator and the previous facilitator have no attendance row and no vote in that session
- [X] T014 [P] [US3] Add failing page tests in `tests/Quorum.App.Tests/SessionCreateTests.cs` so `FullyQualifiedName~SessionCreate` matches the quickstart filter. The group creator, and the facilitator of the highest-numbered session that already has a facilitator, see a create form that requires 2 or 3 topics. Other members do not see it, and a direct submit writes no session and no pick-count change. The facilitator elected on the session being viewed does not see a control to replace that session's topics unless they are also that session's creator and it has not elected yet. Before election, that session's creator can replace the 2 or 3 topics. After election, the list on the page stays. While a session is waiting for its election, the group creator and the current facilitator do not see the create form, and a direct submit writes no session and no pick-count change

### Implementation for User Story 3

- [X] T015 [US3] Change `CreateAsync` and `SaveTopicsAsync` in `src/Quorum.App/Features/Sessions/SessionCommands.cs`. Remove the branch that stores a later session with no topics and the branch that refuses topics on a later create. A session is inserted only together with 2 or 3 `SessionTopic` rows. Who may insert a session, quoted from the data model: the group creator, for the first session and for any later session; otherwise, the active membership that is `FacilitatorMembershipId` on the highest-numbered session in that group which already has a facilitator. An ordinary member is refused. A facilitator who has left is not active, so only the group creator can create the next one. `CreateAsync` refuses when the group already has a session with `FacilitatorMembershipId` null. Add the filtered unique index from `data-model.md` in this story's migration, after checking existing rows. Keep that index out of the `Invitations` migration in T003. No completed publication still writes nothing. Replacement is allowed only when `CreatedByAccountId` is the signed-in account and `FacilitatorMembershipId` is null. Keep the pick-count rules: a topic newly included increments that group's `TopicProgress` once, a topic removed from a replacement does not decrement, and the same topic is not counted twice in one session. Update the create and topic assertions in `tests/Quorum.Sessions.Tests/SessionRulesTests.cs` that still expect a later session with zero topics, a create with topics to be refused after session 1, or any member to set topics after election. Keep the attendance and vote expectations from T012
- [X] T016 [US3] Show the create form with the topic chooser on `src/Quorum.App/Features/Sessions/SessionIndex.cshtml` and `src/Quorum.App/Features/Sessions/SessionIndex.cshtml.cs` only to the group creator and to the active membership that is `FacilitatorMembershipId` on the highest-numbered session that already has a facilitator. The form requires 2 or 3 topics for every session, including the first. Hide the form that posts an empty next session. Hide the create form while any session in the group has `FacilitatorMembershipId` null. On `src/Quorum.App/Features/Sessions/SessionDetail.cshtml` and `src/Quorum.App/Features/Sessions/SessionDetail.cshtml.cs`, show the replace control only when `CreatedByAccountId` is the signed-in account and `FacilitatorMembershipId` is null. The facilitator elected on the session being viewed uses the index create form for the following session and does not gain a replace control on the current session unless they created it and it has not elected. A refusal shows the activity reference and does not change the list on the page

**Checkpoint**: The elected facilitator can plan the next session while the creator is away. Two other members can hold that planned session. User Stories 1 and 2 still pass

---

## Phase 6: User Story 4 - Leave, remove, and keep past sessions (Priority: P2)

**Goal**: The creator can remove a member. A member other than the creator can leave. Past sessions still show the people who were there. An unvoted member drops out of the live voting set. A member who already voted stays a voter for that session and loses group access

**Independent Test**: After a member has attended one session, the creator removes them. That session still lists them. They cannot open the group or vote in a later session. A different member leaves on their own and loses the same access. With three voters and one vote stored, the member who has not voted can leave and the other two can finish. With two voters, that leave elects nobody.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T017 [P] [US4] Add a failing pure rule test in `tests/Quorum.Sessions.Tests/VotingSetRulesTests.cs`. Do not open `PostgresDatabase`. Fewer than two remaining voters does not elect a facilitator. Two or more remaining voters can. Quote: if fewer than two `SessionVoter` rows remain, leave `FacilitatorMembershipId` null
- [X] T018 [P] [US4] Add failing leave and remove tests in `tests/Quorum.Groups.Tests/LeaveRemoveTests.cs` and `tests/Quorum.Sessions.Tests/LeaveVotingSetTests.cs`. The creator removes an accepted member who already attended: that past session still lists them and their vote, and a later request for the group session is not found. Another accepted member cannot remove them. A member who is not the creator can leave, and the result matches removal. The creator cannot leave and cannot be removed. The creator can invite that email again, and a free display name accepts them, while past sessions stay unchanged. Voting set: three voters, one vote stored, and the member who has not voted leaves or is removed. That member's `SessionVoter` row is deleted, votes already cast stay, including a vote that names them as the candidate, and the other two can elect a facilitator. Two voters, and the one who has not voted leaves: no facilitator is stored, and the stored vote stays. A member who already voted, then leaves, keeps the voter row and the vote, and then gets not found on the session. Before the first vote, leave deletes their `SessionAttendance` row. Marking oneself absent after voting does not drop the voter row. A departed member who still has votes for them may be stored as facilitator and then cannot plan the next session. The `leave-group` activity row stores no email and no display name

### Implementation for User Story 4

- [X] T019 [US4] Add leave for a member who is not the creator, and keep remove for the creator, in `src/Quorum.App/Features/Groups/RosterCommands.cs`. The creator's leave and self-remove are refused, and the creator stays a member. On leave or remove, apply the data-model rules in `src/Quorum.App/Features/Sessions/Election.cs`: no vote yet and a `SessionVoter` row exists, delete that voter row and do not delete `SessionVote` rows that name them as the candidate; already voted, keep the voter row and the vote; before the first vote, delete their `SessionAttendance` row if present. After the change, if fewer than two `SessionVoter` rows remain, leave `FacilitatorMembershipId` null. If every remaining voter has a vote and at least two remain, store the facilitator with the same tie rule as today. A stored facilitator who is no longer an active member cannot create the next session. Record `leave-group` for leave. Remove keeps `remove-member`. Neither row contains an email or a display name. They cannot mark themselves present or vote again unless they accept a new invitation
- [X] T020 [US4] On `src/Quorum.App/Features/Groups/Members.cshtml`, show Leave on the signed-in member's own row when that member is not the creator, and show Remove only for the creator on other members. The creator has no Leave control. A member who is not the creator does not see Remove. Add `src/Quorum.App/Features/Groups/LeaveGroup.cshtml` at `/members/leave`, with the page model beside the existing member posts in `src/Quorum.App/Features/Groups/MemberPosts.cshtml.cs`. After leave or remove, that account's session address for the group is not found and the page shows no session content. Past sessions still show the departed member's display name and any stored vote. A refusal shows the activity reference

**Checkpoint**: Membership can end without erasing a session that already happened. The voting set shrinks only for a member who had not voted. Stories 1 through 3 still pass

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Confirm the attempt names, the quickstart, and the leftover name-only seat

- [X] T021 Confirm `ActivityRecord.Attempts` in `src/Quorum.App/Infrastructure/Logging/ActivityRecord.cs` contains `invite-member`, `cancel-invitation`, `accept-invitation`, `decline-invitation`, `leave-group`, and `mark-attendance`. Confirm no command still records `save-attendance`. Update any test that still expects a succeeded name-only `add-member` or a `save-attendance` row, including `tests/Quorum.App.Tests/ActivityRedactionTests.cs` and `tests/Quorum.App.Tests/ActivityExportShapeTests.cs` if those assertions fail. Activity rows for this feature still omit email, display name, and votes
- [X] T022 Run the checks in `specs/003-invited-members/quickstart.md`. From the repository root, run `dotnet test tests/Quorum.App.Tests --filter "FullyQualifiedName~Invitation|FullyQualifiedName~OwnAttendance|FullyQualifiedName~SessionCreate"` and the new tests in `tests/Quorum.Groups.Tests/` and `tests/Quorum.Sessions.Tests/`. Confirm checks 1 through 7 match `specs/003-invited-members/contracts/pages.md`. Check 1 passes only when the inviter cancels from `/members` and the outgoing invitation is absent from that inviter's `/groups`. Confirm no mail is sent. Confirm a name-only seat left from before this feature does not appear on Members and cannot be marked present. SC-001 is the accept-and-open path in that quickstart. There is no separate timing harness for the home or the session page

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup. Blocks every user story
- **User Stories (Phase 3+)**: Depend on Foundational
- **Polish (Phase 7)**: Depends on the user stories that are in the increment being checked

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational. No dependency on US2, US3, or US4
- **User Story 2 (P1)**: Tests T010 and T011 start after Foundational and seed memberships themselves. Implementation T012 waits until T009 has finished editing `SessionRulesTests.cs` and the activity tests, because T012 edits those same files
- **User Story 3 (P1)**: Tests T013 and T014 start after Foundational. Implementation T015 waits until T012, because both edit `SessionRulesTests.cs`. T016 waits until T015
- **User Story 4 (P2)**: Tests T017 and T018 start after Foundational. Implementation T019 waits until T012, because it changes the voting set inside `Election.cs`, and until T009, because it changes `RosterCommands.cs`. T020 waits until T019

### Within Each User Story

- Tests are written and fail before that story's implementation
- Invitation storage from Phase 2 comes before invitation commands
- Commands come before the pages that post them
- A story's checkpoint passes before the next story's implementation starts when both edit the same file

### Parallel Opportunities

- T002 and T004 can run together
- T005 and T006 can run together
- After T007, T008 and T009 can run together
- After Phase 2, the new test files T010, T011, T013, T014, T017, and T018 can run together with US1 implementation, as long as they do not edit `SessionRulesTests.cs`, `RosterCommands.cs`, `Election.cs`, or `ActivityRecord.cs`
- T012, T015, and T019 stay sequential because they share `SessionRulesTests.cs` and `Election.cs`

---

## Parallel Example: User Story 1

```bash
# Tests first, different files:
Task: "Add failing pure rule tests in tests/Quorum.Groups.Tests/InvitationRulesTests.cs"
Task: "Add failing invitation tests in tests/Quorum.App.Tests/InvitationTests.cs"

# After InvitationCommands exists:
Task: "Show pending invitations on src/Quorum.App/Features/Groups/GroupList.cshtml"
Task: "Replace the add-by-display-name form on src/Quorum.App/Features/Groups/Members.cshtml"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run `InvitationRulesTests` and `InvitationTests`. An invited person can accept and open the group with no message

### Incremental Delivery

1. Setup + Foundational
2. User Story 1, the join path. This is the spec's MVP
3. User Story 2, hold a session without the creator
4. User Story 3, the elected facilitator plans the next session. US1 through US3 together are the plan's delivery bar: two members who are not the creator attend, vote, elect a facilitator, and that facilitator plans the next session while the creator is away
5. User Story 4, leave and remove
6. Polish runs the quickstart filter

### Parallel Team Strategy

1. Finish Setup and Foundational together
2. Then one person can take US1 while another writes the new US2 and US3 test files
3. US2 implementation starts after T009. US3 implementation starts after T012. US4 implementation starts after T012 and T009

---

## Notes

- `[P]` tasks use different files and do not depend on an unfinished task, except the T008 and T009 wave named above
- Story labels map to spec.md. Setup, foundational, and polish tasks have no story label
- Verify the new tests fail before implementing that story
- Commit after each task or logical group
- Stop at any checkpoint to validate that story on its own
- Avoid editing `SessionRulesTests.cs` from two stories at once
