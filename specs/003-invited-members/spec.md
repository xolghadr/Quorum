# Feature Specification: Invited Members

**Feature Branch**: `003-invited-members`

**Created**: 2026-10-31

**Status**: Draft

**Input**: User description: "I want each group member be a real user, can be invited through its email address (no email sends, just show invitations in their dashboard) can participate into sessions, vote and plan the next session if they are elected as a session coordinator. IT HAS TO BE POSSIBLE TO HOLD SESSIONS EVEN IF GROUP CREATOR IS ABSENT."

Person served: a group member who is not the group creator. Session outcome: that member can join the group, take part in a session, cast their own vote, and, once elected facilitator, plan the next session while the group creator is away.

## Clarifications

### Session 2026-10-31

- Q: When someone is elected facilitator, which session are they allowed to plan? → A: They plan the following meeting by creating it and choosing its 2 or 3 topics. The current session's topics are chosen by the person who created that session, and that person is either the group creator or the facilitator who planned it. Other members do not choose topics.
- Q: During a session, who marks a member present and who casts that member's vote? → A: Each member is signed in and acts only for themselves. One member cannot mark another present or cast another member's vote.
- Q: Who is allowed to invite someone into the group by email? → A: Any accepted member can invite by email. Only the group creator can remove a member or cancel an invitation that another member created. The member who created an invitation can cancel that invitation.
- Q: Can a member invite an email address that does not have an account yet? → A: Yes. The invitation appears on that person's home after they register with that email. No message is sent.
- Q: If a member leaves the group or is removed after voting has opened but before they cast a vote, can the others still finish the election? → A: Yes. If they have not voted, they drop out of this session's voting set. Votes already cast stay. The election finishes when every remaining voter has voted, and only if at least two voters remain. If fewer than two remain, no facilitator is elected.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Join a group from an invitation (Priority: P1)

A person who already studies with a group, or who is about to, gets a place in that group by email address. Nothing is sent to an inbox. The next time they are signed in, the invitation is waiting on their home. They accept, choose the name they will use in that group, and the group is theirs to open. Until they accept, the group's sessions stay hidden.

**Why this priority**: No later session works for a second person until that person is a real member. The current name-only seat does not let them sign in.

**Independent Test**: With three registered people, an accepted member who is not the group creator invites the third person's email while the creator is signed out. No message arrives in an inbox. The third person signs in, sees the invitation on their home, accepts it with a display name, and then sees that group. A person who was not invited sees none of it. The inviting member cannot remove the new member. The group creator can.

**Acceptance Scenarios**:

1. **Given** a signed-in accepted member and an email address that can be registered, **When** that member invites that email to the group, **Then** a pending invitation exists for that group and that email. This includes a member who is not the group creator, and it includes doing so while the group creator is signed out. No email and no other message is sent.
2. **Given** a pending invitation for an email that already has an account, **When** that person signs in, **Then** their home lists the invitation with the group name and the inviter's display name in that group.
3. **Given** a pending invitation for an email that has no account yet, **When** that person later registers with that email and signs in, **Then** the same invitation is on their home. Letter case in the email does not hide it.
4. **Given** a pending invitation, **When** the invited person accepts and chooses a display name of 1 to 40 characters that is not already used in the group, **Then** they are a member of that group under that name and can open the group. The invitation is no longer pending.
5. **Given** a person has a pending invitation and has not accepted it, **When** they try to open that group's members, sessions, votes, or pick counts, **Then** none of that is shown.
6. **Given** a pending invitation, **When** the invited person declines it, **Then** they are not a member, the invitation is gone from their home, and an accepted member may invite that email again.
7. **Given** an invited person submits a blank display name, a name longer than 40 characters, or a name already used in the group ignoring letter case, **When** they try to accept, **Then** they are not added and the invitation stays pending.
8. **Given** an accepted member invites their own email, an email that is already a member, an email that already has a pending invitation for that group, or a blank email or an email `Account.EmailAccepted` rejects, **When** they submit the invitation, **Then** no invitation is created.
9. **Given** active account memberships plus pending invitations already equal 8, **When** any accepted member tries to invite another email, **Then** no invitation is created. The creator is one of those memberships. A name-only row is not in the count.
10. **Given** accepting would make more than 8 members, **When** the invited person tries to accept, **Then** they are not added, the invitation stays pending, and they can see that the group is full.
11. **Given** a pending invitation, **When** the member who created it cancels it, or the group creator cancels an invitation that another member created, **Then** it disappears from the invited person's home, that person is not a member, and the place can be used for another invitation. An ordinary member cannot cancel an invitation they did not create.
12. **Given** account A invited account B into A's group, **When** account C is signed in, **Then** C does not see that invitation, and C sees none of A's group.
13. **Given** an account is an accepted member of two groups, **When** one of those groups is open, **Then** the other group's members, invitations, sessions, votes, and pick counts are not shown. The account's display name in the second group may differ.
14. **Given** a pending invitation, **When** the member who created it or the group creator opens the members page, **Then** that person sees the invited email and a cancel action. **When** another accepted member opens the members page or the home, **Then** that member sees neither the row nor the email. The inviter's home does not list the invitation they sent.

---

### User Story 2 - Hold a session without the group creator (Priority: P1)

Two accepted members meet while the group creator is signed out and not present. Each of them marks their own attendance and casts their own vote. The people present elect one facilitator from among themselves. The creator's empty seat is not required and is not counted.

**Why this priority**: A group that can meet only when its creator is at the keyboard does not have a session. This is the failure the member invitation has to remove.

**Independent Test**: The group creator creates a session and chooses its topics, then signs out. Two other accepted members open that session, mark themselves present, vote, and see one facilitator elected. The group creator's name is not present and has no vote. Neither member creates the session or changes its topics.

**Acceptance Scenarios**:

1. **Given** a published catalog and a session whose topics were chosen by the group creator or by the facilitator who created it, **When** the group creator is signed out and two accepted members open that session, **Then** they can hold it. An ordinary member cannot create a session or choose its topics.
2. **Given** that session and the creator still absent, **When** each of two accepted members marks themselves present, **Then** the attendance list is those two members. Neither member can mark the other person, or the creator, present.
3. **Given** two to eight accepted members are present and no vote has been cast, **When** each present member casts their own vote for one present member, and one candidate has strictly more votes than each other, **Then** the voters and candidates are the people who were present at the first vote, and that candidate is the facilitator. The creator is not a voter unless the creator marked themselves present and voted.
4. **Given** a member has not voted, or has already voted, **When** another member tries to mark them present or submit a vote for them, **Then** attendance and the vote are unchanged. No vote is stored for a member who did not cast it while signed in.
5. **Given** the same member is signed in on two browsers, **When** either browser marks attendance or votes, **Then** the member is one person on the attendance list and one voter.
6. **Given** only one accepted member is present because the creator is away, **When** that member tries to open voting, **Then** no voting set is created and no facilitator is elected.
7. **Given** a voting set of exactly two people, **When** one of them votes for themselves, **Then** the vote is accepted.
8. **Given** a voting set of three or more people, **When** a member votes for themselves, **Then** that vote is refused.
9. **Given** every present voter has voted and two or more candidates share the highest count, **When** the ballots are checked, **Then** one of those candidates is stored as the facilitator, each of them had an equal chance, and a later visit shows that same person.
10. **Given** a member who has not accepted an invitation, or a display name with no account, **When** someone tries to include them in attendance or as a candidate, **Then** they are not included.
11. **Given** the creator is signed out, **When** an accepted member opens this group's session, **Then** the session is shown. A signed-out person who opens it directly is asked to sign in and sees no session content. An account that has not accepted a place in the group is told the session is not found and sees no session content.

---

### User Story 3 - The elected facilitator plans the next session (Priority: P1)

The person elected facilitator in this session plans the next one. They choose its two or three topics. They do not have to be the group creator, and the creator does not have to be present. When that next meeting happens, the people who are actually there can hold it and elect their own facilitator even if both the creator and the previous facilitator are away.

**Why this priority**: Election has to change what the member is allowed to do. Otherwise a second login can vote and still cannot prepare the group's next meeting.

**Independent Test**: After two members elect a facilitator while the creator is signed out, that facilitator creates the next session and puts two topics on it. Those topics show a pick count for the group. Later, two other accepted members hold that session without the facilitator and without the creator, and elect a new facilitator.

**Acceptance Scenarios**:

1. **Given** a session whose facilitator has been elected, and that person is an accepted member, **When** they plan the next session with 2 or 3 distinct topics, **Then** the next session exists, those topics are on it in the chosen order, and each included topic's pick count increases by 1. The creator does not have to be signed in.
2. **Given** an accepted member who is not the group creator and is not the facilitator of the highest-numbered session that already has a facilitator, **When** they try to create a session or set its topics, **Then** nothing is created and no pick count changes. The group creator may still create a session and choose its topics when every existing session already has a facilitator. While any session has no facilitator, the creator's create is refused and no pick count changes. A facilitator of an earlier session does not keep the right to create.
3. **Given** the elected facilitator plans the next session with 1 topic, with 4 topics, with the same topic twice, or with a worked design, **When** they submit it, **Then** the next session is not given that list and no pick count changes.
4. **Given** the next session's topics were planned, **When** the facilitator replaces that list with 2 other topics before that next session has elected its own facilitator, **Then** only the new topics are on it. A topic added in the replacement is counted once. A topic removed from the list does not lose a count.
5. **Given** the next session already has topics, and both the group creator and the facilitator who planned it are absent, **When** at least two accepted members mark themselves present and vote, **Then** they elect a facilitator for that session from the people present. The planned topics stay, and the absent creator does not block the election.
6. **Given** the group has no session yet, **When** the group creator creates the first session with 2 or 3 distinct topics, **Then** that session has those topics. An ordinary member cannot create it and cannot choose the topics.
7. **Given** a session whose topics were chosen by its creator, **When** someone other than that creator tries to change the topics, **Then** the topics stay as they were. The facilitator elected in that session plans the following session and does not gain the right to replace the current one.
8. **Given** the elected facilitator is no longer an accepted member and has not created the next session, **When** an ordinary member tries to create it or set its topics, **Then** nothing is saved. The group creator may create that session and choose its topics.
9. **Given** account A has planned a next session, **When** account B views a different group, **Then** B sees none of A's session, topics, or pick counts.
10. **Given** the following session already exists and its facilitator has not been elected, **When** the group creator or the facilitator who was allowed to create it tries to create another session, **Then** nothing is created and no pick count changes.

---

### User Story 4 - Leave, remove, and keep past sessions (Priority: P2)

The creator can remove a member. A member other than the creator can leave. Past sessions still show the people who were actually there. The removed person cannot walk back into a later session or open the group.

**Why this priority**: Membership has to be reversible, but a session that already happened must stay understandable. Removal is not required to hold the first invited session.

**Independent Test**: After a member has attended one session, the creator removes them. That session still lists them. They cannot open the group or vote in a later session. A different member leaves on their own and loses the same access.

**Acceptance Scenarios**:

1. **Given** an accepted member who is not the group creator, **When** the group creator removes them, **Then** they no longer see the group, its later sessions, or its invitations. Sessions they already attended still list them, and their vote in those sessions stays. Another accepted member cannot remove them.
2. **Given** an accepted member who is not the creator, **When** they leave the group, **Then** the result is the same as a removal.
3. **Given** the group creator, **When** they try to remove themselves or leave the group, **Then** the creator stays a member.
4. **Given** a removed member, **When** they open a session address from that group, **Then** the session is not found and no session content is shown.
5. **Given** a member was removed, **When** the creator invites that email again and the person accepts with a display name that is free in the group, **Then** they are a member again. Past sessions are unchanged.
6. **Given** a voting set of three members and only one has voted, **When** one member who has not voted leaves the group or is removed, **Then** the voting set is the other two. When both of those two have voted, a facilitator is elected. Any vote already cast for the departed member stays. The departed member cannot open the group.
7. **Given** a voting set of two members and one has not voted, **When** the member who has not voted leaves or is removed, **Then** no facilitator is elected. The vote already cast stays.
8. **Given** a member has already voted, **When** they leave the group or are removed, **Then** that vote stays and they remain a voter for that session. They cannot open the group afterward.

---

### Edge Cases

- An invitation for an email that is not registered yet waits on that person's home after they register. It does not wait on an inbox, because no message is sent.
- Two invitations that differ only by letter case in the email are the same invitation.
- A display name already used in the group, ignoring letter case, cannot be used by a new member. The same person may use a different display name in another group.
- The group creator's absence means both signed out and not marked present. Neither state is required for attendance, voting, or election on a session that already has topics. It does not let an ordinary member create a session or choose topics. The elected facilitator may still plan the following session while the group creator is away.
- A session cannot be created without its 2 or 3 topics. The person who creates it is the group creator or the facilitator of the highest-numbered session that already has a facilitator. While any session in the group still has no facilitator, another session cannot be created. If neither allowed person has created the waiting session, the members who are present cannot open a new session or supply the topics.
- Fewer than 2 present members, or more than 8, does not open voting. The absent creator is not added to make up the number.
- A person who marks themselves present after the first vote is present only. They have no vote and cannot be chosen. A voter who marks themselves absent after voting remains a voter and a candidate, and the vote stays.
- A member who has already voted cannot change that vote.
- Leaving the group or being removed is not the same as marking oneself absent. The voting-set effect of leave or removal is FR-012. Whether the election then finishes is FR-017. Marking oneself absent after voting still leaves that person a voter and a candidate.
- A pending invitation counts toward the cap of 8. Declining or cancelling it frees a place. The member who created a pending invitation may cancel it. The group creator may cancel any pending invitation. No other member may cancel an invitation they did not create. The creator is one active account membership, not a second seat. A name-only row is outside the sum.
- Accepting into a full group fails without consuming the invitation.
- A display name left over from a name-only seat is not a member, not a voter, and not a candidate. While that row remains, the name cannot be accepted by a new member.
- The facilitator elected in one session does not become the facilitator of the group. The next session elects its own.
- Planning a session does not publish catalog text and does not let a member edit a topic or a worked design.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Any accepted member MUST be able to invite a person to that group by email address, including while the group creator is signed out. An email that has no account yet MUST be a valid invitation target.
- **FR-002**: Creating an invitation MUST NOT send an email or any other message.
- **FR-003**: A pending invitation MUST appear on the invited person's home when they are signed in with that email, including when they register after the invitation was created. Matching MUST ignore letter case.
- **FR-004**: The home listing MUST show the group name and the inviter's display name in that group, and MUST NOT show that group's sessions, votes, member list, or pick counts before acceptance. That home lists only invitations for the signed-in account's own email. It does not list an invitation that account sent, and it does not offer cancel.
- **FR-005**: The invited person MUST be able to accept or decline from that home.
- **FR-006**: Acceptance MUST require a display name of 1 to 40 characters that is not already an active display name in that group, ignoring letter case. An active display name includes a current member and a leftover name-only seat. The accepted person MUST then be a member and MUST be able to open the group.
- **FR-007**: A declined invitation MUST remove the person from consideration and MUST allow an accepted member to invite that email again.
- **FR-008**: The member who created a pending invitation MUST be able to cancel it. The group creator MUST be able to cancel an invitation that another member created. An ordinary member MUST NOT be able to cancel an invitation they did not create. Only the group creator MUST be able to remove an accepted member. On the members page, the invited email is shown to the member who created that invitation and to the group creator. An ordinary member who did not create it sees neither the row nor the email.
- **FR-009**: An invitation MUST be refused when the email is blank or rejected by the same check registration uses (`Account.EmailAccepted`), is the inviter's own email, already belongs to a current member, or already has a pending invitation for that group.
- **FR-010**: A group MUST NOT exceed 8, counted as active account memberships plus pending invitations. The creator is one active account membership. A name-only row is not in the sum. A further invitation MUST be refused. An acceptance that would exceed 8 MUST be refused and MUST leave the invitation pending.
- **FR-011**: A member other than the creator MUST be able to leave the group. The creator MUST NOT be able to remove themselves or leave the group.
- **FR-012**: A removed or departed member MUST lose access to the group and its later sessions. Sessions they already attended MUST still list them, and votes already stored MUST stay. They MUST NOT be marked present or vote again unless they accept a new invitation. If they are in the current voting set and have not voted, they MUST drop out of that set. If they have already voted, they MUST remain a voter for that session.
- **FR-013**: Every person added to attendance or to a new voting set MUST be an accepted member with an account. A display name with no account MUST NOT be a member, a voter, or a candidate. A member who leaves the voting set after votes were cast for them MAY still be the facilitator chosen by those votes, and then MUST NOT plan the following session.
- **FR-014**: A session MUST be created only by the group creator, or by the active membership that is the facilitator of the highest-numbered session in that group which already has a facilitator. The group creator may create the first session and any later session. The first session MUST be created only by the group creator. A new session MUST be refused while the group already has a session whose facilitator has not been elected. That includes the first session before its own election. The following session is the session created after an election. Either allowed actor may create the next session only when every existing session already has a facilitator. A second create by either actor MUST be refused, and MUST NOT change a pick count, until that session elects its own facilitator. Two creates submitted together store one session and refuse the other. Creating a session MUST include choosing its 2 or 3 topics. Anyone else, including a facilitator of an earlier session, MUST NOT create a session. A session that already has topics MUST be holdable while its creator is signed out.
- **FR-015**: Each accepted member MUST be signed in to mark attendance or vote, MUST mark only their own attendance, and MUST cast only their own vote. A member MUST NOT mark another member present and MUST NOT cast another member's vote. One account on more than one browser MUST remain one member and one voter.
- **FR-016**: A session MUST be holdable when the group creator is absent. With 2 to 8 accepted members present, those members MUST be able to open voting, elect one facilitator from among the people present at the first vote, and finish that election without the creator being signed in or marked present.
- **FR-017**: The existing election rules MUST remain: a set of two may include a self-vote, a larger set MUST refuse a self-vote, each voter has one unchangeable vote, a later arrival does not join the voting set, a voter who marks themselves absent after voting stays a voter, and a tie stores one of the leading candidates and shows that same person later. Leave or removal changes who is still in the voting set only as FR-012 says. The election MUST finish when every remaining voter has voted, and only when at least two voters remain. If fewer than two voters remain, no facilitator is elected.
- **FR-018**: The facilitator named in FR-014 plans the following session by creating that next session, with the topics FR-014 requires, while the group creator may be signed out. Planning does not replace the topics of the session being held.
- **FR-019**: Removed. It repeated FR-014 and pointed at FR-020.
- **FR-020**: The topics of a session MUST be chosen by the person who creates that session. That person is the group creator, or the facilitator who planned that session from the previous one. That creator MAY replace the list before the session elects its own facilitator. No other member may choose or replace the topics.
- **FR-021**: After a session has elected its own facilitator, its topics MUST stay as its creator left them. The newly elected facilitator plans the following session and MUST NOT gain the right to replace the current session's topics.
- **FR-022**: If the elected facilitator leaves the group before creating the following session, an ordinary member MUST NOT be able to create it. The group creator MAY create it and choose its topics.
- **FR-023**: A session that already has topics MUST be holdable, including a new election, when both the group creator and the previous facilitator are absent.
- **FR-024**: Setting topics MUST add one pick for each topic newly included, MUST NOT count a topic twice in the same session, and MUST NOT reduce a pick count. One topic, four or more, a duplicate, or a worked design MUST NOT change the session or the counts.
- **FR-025**: A signed-out request for a group session MUST ask the person to sign in and MUST show no session content. An account that is not an accepted member of that group, including an account whose only tie to the group is a pending invitation, MUST see none of that group's members, sessions, votes, or pick counts. A direct request for one of its sessions MUST be told the session is not found and MUST show no session content. A direct request for `/members`, `/sessions`, or `/catalog/topics/{topicKey}` MUST NOT show that group's members, sessions, votes, or pick counts.
- **FR-026**: One account MAY belong to more than one group, with a display name chosen per group. While one group is open, another group's members, invitations, sessions, votes, and pick counts MUST NOT be shown.
- **FR-027**: The group MUST NOT store a standing facilitator. Election in one session MUST NOT fill the facilitator of another session.

### Key Entities *(include if feature involves data)*

- **Invitation**: A pending place in one group for one email address, created by an accepted member of that group. It records the group, the email, and the inviter. It is accepted, declined, or cancelled. It is not a message.
- **Member**: An account that has a place in a group, with a display name used only in that group. The creator is the member who created the group. An accepted invitee is a member with the same standing for attendance, voting, and election.
- **Facilitator**: The one member elected by the voters of a single session. This is the session coordinator. The role belongs to that session, not to the group, and it is what allows that member to plan the next session.
- **Session plan**: The following session and its 2 or 3 topics, created under FR-014. Any accepted member may later hold a session that already has topics.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An invited person who already has an account can see the invitation on their home, accept it, and open the group in under 2 minutes, without receiving an email.
- **SC-002**: A person invited before they register sees that invitation on the first home they reach after registering with that email.
- **SC-003**: Two accepted members can mark attendance, cast their own votes, and see a facilitator elected in a single session while the group creator stays signed out for the whole session.
- **SC-004**: The elected facilitator can put a next session with 2 or 3 topics in place during that same signed-out period for the creator, and those topics are visible to the other accepted members of the group.
- **SC-005**: Two accepted members can hold that already planned session and elect a new facilitator while both the creator and the previous facilitator stay away.
- **SC-006**: In a check of invitations, session links, and group views, an account that has not accepted a place in the group sees none of that group's sessions, votes, or member names. A signed-out visitor sees no session content.
- **SC-007**: A group whose active account memberships plus pending invitations already equal 8 refuses every further invitation and every further acceptance. The creator counts as one active account membership. A name-only row does not count.

## Assumptions

- "Session coordinator" in the request means the facilitator elected for that one session. The group still has no standing facilitator.
- Any accepted member may invite. The group creator is the only person who can remove an accepted member or cancel an invitation created by someone else. The inviter can cancel their own pending invitation. Absence of the group creator does not block invitations or sessions.
- A member other than the creator may leave so a group is not forced to keep someone the creator can no longer remove in person.
- The cap of 8 is active account memberships plus pending invitations. The creator is one active account membership. A name-only row is not in the sum.
- An email that is not registered yet is a valid invitation target. The invitation appears on that person's home after they register with that email. No message is sent.
- The invited person chooses their display name when they accept. The creator does not choose it for them.
- Each member is signed in and records only their own attendance and their own vote. One signed-in member cannot mark another member present or cast another member's vote.
- Name-only seats are not members under this feature. People who already appear only as a typed name need an invitation and an acceptance before they can attend or vote.
- The group creator may still rename an active account membership. That is the existing rename. This feature does not add a new rename, and rename is not a way to add a person. A name-only row is not on the member list, so the page does not offer rename for it.
- Who may create a session, and that the create includes its 2 or 3 topics, is FR-014. The group may have only one session waiting for its election. Other members do not choose topics.
- If the facilitator who was allowed to create the following session leaves the group before doing so, the result is FR-022.
- Topic pick counts, the published catalog, worked designs, and the rule that another group sees none of this group's progress stay as they are.
- Registration, sign-in, and the rule that a wrong password does not reveal whether an email is registered stay as they are. An invitation does not reveal an account's other groups.
- No notification channel is added. The product does not host voice or video, and it does not add the live practice blocks, the rubric, or billing.
- Constitution 1.3.0 already requires an account for every member who can attend, vote, or be elected. A display name with no account is not a member. Do not amend the constitution again in this feature. The group creator still creates the first session and chooses its topics. An accepted member can hold that session, vote, and be elected while the group creator is away, and the elected facilitator can create the following session and choose its topics when every existing session already has a facilitator.
