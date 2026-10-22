# Feature Specification: Group Catalog

**Feature Branch**: `001-group-catalog`

**Created**: 2026-10-22

**Status**: Draft

**Input**: User description: "A facilitator can sign in, create one group, and open that group's catalog of senior-backend topics, questions, and weekly units synced from the curated roadmap. Members have display names in the group. Another group's items are not visible. No live session, no Telegram, and no billing. Person served: the facilitator. Outcome: the answer key for a weekly unit is available inside the group before any session exists."

## Clarifications

### Session 2026-10-22

- Q: Should the facilitator browse the roadmap's tracks and topics, or weekly units of two or three topics? → A: The roadmap is a one-time starting catalog, not an ongoing dependency. The first publication fills the product with that roadmap's tracks, topics, questions, and worked designs. Further questions, designs, and later practice material such as code katas are future work and do not require the roadmap.
- Q: Should weekly topic selection be in this feature? → A: Topics belong to a session, not to a week. A group may have more than one session in the same week. The group creator creates the first session and sets its topics. Later sessions get their topics from the facilitator elected at the start of that session.
- Q: What happens when a topic is picked again, and when is a track reviewed? → A: A topic may be picked in more than one session. Each group keeps a pick count per topic, and the topic is marked reviewed once that count is at least 1. A new session adds 1 for each topic it includes. Changing topics inside the same session does not count a topic twice and does not reduce a count. Never-picked topics are offered first. A track is marked reviewed for that group when every topic in it has been picked at least once.
- Q: Is the facilitator a property of the group? → A: No. The members in a session elect the facilitator from among themselves at the start of that session. The group stores no facilitator.
- Q: Can one member be in more than one group, and under another name? → A: Yes for a person with an account. The display name is chosen per group and may differ. A roster name with no account stays in the one group where it was typed. Names are unique inside a group, not across groups.
- Q: When the people in a session elect a facilitator, may a person vote for themselves? → A: Only in a session of exactly two people. In a larger session a person must vote for someone else. When every included person has voted and two or more candidates share the highest count, one of those candidates is chosen at random and stays the facilitator.
- Q: After a session has named the people in it, can that list change? → A: The session does not name people at creation. Every group member may attend. The people marked present are the attendance.
- Q: When the attendance list changes after someone has voted, do the voters and the candidates change with it? → A: Attendance can still change. The voters and the candidates stay the people who were present when the first vote was cast. A person who arrives later is marked present and does not vote or stand. A person who leaves remains a voter and a candidate, and their vote stays.
- Q: Before a facilitator is stored, may a voter replace the vote they already cast? → A: No. The first choice each voter casts stays, even while other people have not voted yet.
- Q: If the same account is signed in on more than one browser, how many people is that? → A: One. Those visits are the same account, the same membership, and one voter when that membership is in the voting set.
- Q: Is the cap of 8 the attendance list after voting, or the group? → A: The group. A group never has more than 8 members, counting the owner. Attendance cannot exceed the group.
- Q: Which date does the catalog show? → A: The catalog's last modification date, which is the day the one publication completed.
- Q: What does it mean that a later session must still elect someone when the previous facilitator is absent? → A: Every session starts by electing its own facilitator from the people in that session's voting set. The person elected last time does not have to attend. Their absence does not block this election.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open a topic's answer key (Priority: P1)

A person creates an account, signs in, creates a group, and reads a topic's answer key from the catalog filled by the first publication of the senior-backend roadmap. They can also open a worked design. No live session exists. A second account has a separate group and member list, and both read the same seeded catalog.

**Why this priority**: The product is not useful until the signed-in account can read an answer key. The roadmap already organizes that material as tracks, topics, and worked designs.

**Independent Test**: Publish the roadmap into the product once. Create two accounts and two groups. The first account opens one track, reads one topic's answer key and questions, and opens one worked design. The second account sees none of the first group's name or members, and can open that same topic.

**Acceptance Scenarios**:

1. **Given** a person has no account, **When** they register with a valid email and a password of 10 to 128 characters, **Then** they have an account and are signed in.
2. **Given** a signed-in account has no group, **When** they name a group with 1 to 60 characters and choose their display name in that group, **Then** that group is created, they are a member under that name, and they are taken to the catalog.
3. **Given** an account already belongs to a group, **When** they create another group and choose a display name for it, **Then** both groups exist. The new group's only member is that account, under the new display name. It has no sessions and no pick counts. The first group is unchanged.
4. **Given** the first publication has completed, **When** the signed-in account opens the catalog, **Then** they see the roadmap's tracks in published order, the topics inside the track they open, the worked designs, and the catalog's last modification date, which is the day that publication completed.
5. **Given** a track with one or more topics, **When** the signed-in account opens a topic, **Then** they see each answer-key section the publication supplied (level, what the member should be able to do, a one-minute explanation, when it matters, what it costs, a failure to recognize, what to learn, review questions, interview prompts, and an exercise) and they see each published question once, labeled as a review question or an interview prompt.
6. **Given** a worked design included in the first publication, **When** the signed-in account opens it, **Then** they can read the design as published.
7. **Given** no live session has been started, **When** the signed-in account opens the topic, **Then** the answer key and questions are still fully readable.
8. **Given** the signed-in account is reading a topic or a design, **When** they try to change its text, questions, or track membership, **Then** no edit is available and the published wording stays as published.
9. **Given** the first publication has never completed, **When** the signed-in account opens the catalog, **Then** they see that the answer key is not available yet, and they do not see an empty track list presented as a finished catalog.
10. **Given** account A has a group with members and the catalog has been published, and account B is signed in, **When** B opens the catalog and their member list, **Then** B sees none of A's group name or members, and B can open the same tracks, topics, questions, and designs that A can open.
11. **Given** an email is already registered, **When** another person tries to register with that email, differing only by letter case, **Then** no second account is created and they are told that the email is already in use.
12. **Given** a person submits a blank group name, a blank display name, an invalid email, a password shorter than 10 characters, or a password longer than 128 characters, **When** they try to register or create the group, **Then** the action is refused and nothing is created.
13. **Given** the first publication has not finished, **When** the signed-in account opens the catalog, **Then** they see the not-available state and no partial track, topic, or design.

---

### User Story 2 - Create sessions and elect a facilitator (Priority: P1)

The group creator creates the first session and sets its 2 or 3 topics. The group may create more sessions in the same week. Every group member may attend. The first vote freezes the voters and candidates as the people present at that moment, and they elect one of themselves as facilitator. Later changes to attendance leave that voting set as it is. On every session after the first, that facilitator sets the topics. A topic can be picked again in a later session. The group sees pick counts and reviewed marks, and never-picked topics are offered first. Another group sees none of this. The 45-minute practice blocks are not part of this feature.

**Why this priority**: The catalog is the library. A session is the short list the group will practice, and a group may meet more than once in a week. The facilitator has to come from the people in that session, not from a fixed owner.

**Independent Test**: With a published catalog, the group creator creates the first session with 3 topics and sees each count become 1. They correct that session's topics without a second count. They create a second session in the same week with no topics. The people present cast the first vote and become the voters and candidates. One facilitator is elected, and the signed-in member records the 2 topics that facilitator sets, one of which was already picked, raising that topic's count from 1 to 2. A tie stores one of the leaders, and a later visit shows that same person. A later session still elects someone when the previous facilitator is not in that session. A member who arrives after that first vote is marked present and has no vote. A member who was absent when voting opened has no vote. A second group sees none of the sessions or counts. Creating a session before the catalog is published creates nothing.

**Acceptance Scenarios**:

1. **Given** a published catalog and a group whose creator has chosen a display name, **When** the creator creates the first session with 3 distinct topics, **Then** the session exists, those topics are on it in the chosen order, each has a pick count of 1, it has no attendance yet, and no facilitator is elected yet.
2. **Given** the first session has not started, **When** the creator replaces its 3 topics with 2 others, **Then** only those 2 are on the session. The new topics have a pick count of 1. The removed topics stay at 1. No count goes down.
3. **Given** a group that already has a session, **When** a member account of that group creates another session, **Then** the new session has no topics, no attendance, and no facilitator, and the earlier session is unchanged.
4. **Given** a session that has not started and 2 to 8 group members are marked present, **When** the first vote is cast and then every one of those people has voted for one of them and one candidate has strictly more votes than each other candidate, **Then** those people are the voters and the candidates, that candidate is the facilitator, and the session has started. A group member who was absent at that first vote is not a voter.
5. **Given** every voter has voted and two or more candidates share the highest count, **When** the ballots are checked, **Then** one of those candidates is stored as the facilitator. Each of them had an equal chance. A later visit shows that same facilitator.
6. **Given** a voter has not voted, **When** the ballots are checked, **Then** the session has no facilitator.
7. **Given** a voting set of exactly two people, **When** one of them votes for themselves, **Then** the vote is accepted.
8. **Given** a voting set of three or more people, **When** a voter votes for themselves, **Then** the vote is refused and the session has no facilitator from that ballot.
9. **Given** a later session whose facilitator has been elected, **When** the signed-in member records the 2 distinct topics that facilitator sets, **Then** those topics are on that session and each included topic's pick count increases by 1. A topic already counted by the first session goes from 1 to 2 and stays reviewed.
10. **Given** a later session with no elected facilitator, **When** anyone tries to save topics, **Then** nothing is saved.
11. **Given** a later session with an elected facilitator, **When** an account that does not belong to that group tries to save topics, **Then** nothing is saved. The elected facilitator may be a roster name with no account. The signed-in member of the group records the topics that facilitator sets.
12. **Given** someone is choosing topics, **When** some topics have been picked and some have not, **Then** topics with a pick count of 0 are listed before the others, and published order is kept inside each of those groups.
13. **Given** a track whose every topic has been picked at least once, **When** a member of that group views the track, **Then** the track is marked reviewed. A second group still sees it as not reviewed.
14. **Given** a topic save of 1 topic, of 4 topics, of the same topic twice, or of a worked design, **When** it is submitted, **Then** the session, the counts, and the reviewed marks stay as they were.
15. **Given** the catalog has never been published, **When** the creator tries to create a session, **Then** no session is created.
16. **Given** account A has sessions and pick counts, **When** account B views another group, **Then** B sees none of A's sessions, votes, facilitator, or counts.
17. **Given** an account is already a member of one group under one display name, **When** that account creates a second group and chooses a different display name, **Then** both memberships exist and the names differ. A roster name with no account does not appear in the second group.
18. **Given** a person who is not a member of the group, **When** someone tries to record them as present, **Then** they are not added and the attendance list is unchanged.
19. **Given** no vote has been cast, **When** fewer than 2 or more than 8 group members are marked present, **Then** the attendance is not saved and no voting set is created.
20. **Given** no vote has been cast and attendance is 2 to 8 people, **When** that list is replaced with another 2 to 8 group members, **Then** the new list is who will vote.
21. **Given** at least one vote has been cast, **When** another group member is marked present, **Then** that member is on the attendance list, has no vote, and cannot be chosen. The voters, the candidates, and the votes already cast stay as they were.
22. **Given** at least one vote has been cast, **When** a voter is taken off the attendance list, **Then** that person is no longer marked present, remains a voter and a candidate, and their vote stays.
23. **Given** a voter has already cast a vote and the facilitator is still empty, **When** that voter is submitted again for a different candidate, **Then** the first choice stays and the second submission changes nothing.
24. **Given** a session whose facilitator is a member named Mina, **When** a later session's voting set does not include Mina and every voter in that later session has voted for one leader, **Then** that leader is the facilitator of the later session. Mina's absence does not block it.

---

### User Story 3 - Return to the same group (Priority: P2)

The account holder leaves and comes back later. The same account still has the same groups, the same members, and the same catalog. The catalog remains after the roadmap itself is no longer the source. A wrong password does not open any catalog.

**Why this priority**: The answer key has to still be there on the day of the session, including when no further publication is run. A failed sign-in must not reveal another group's members.

**Independent Test**: After the first publication, sign out, fail one sign-in with a wrong password, sign in correctly, and confirm the same group, the catalog's last modification date, topic, and members. Then make the roadmap unavailable and confirm the same topic still opens.

**Acceptance Scenarios**:

1. **Given** an account is signed in, **When** they sign out, **Then** the catalog, members, and answer key are no longer shown.
2. **Given** a signed-out person, **When** they try to open the catalog or an answer key directly, **Then** they are asked to sign in and no catalog or member list is shown.
3. **Given** a registered email, **When** someone signs in with the wrong password, **Then** they stay signed out and the message does not say whether the email is registered.
4. **Given** an account that created a group and a session, **When** they sign in with the right password, **Then** they land in that same group and see its members, the catalog's last modification date, the tracks, the topics, the worked designs, the same sessions, and the same pick counts and reviewed marks.
5. **Given** the same account is signed in on two browsers, **When** either browser opens the group, marks attendance, or submits a vote, **Then** both browsers show one membership for that account. The account adds one person to attendance and, once voting has opened, one voter. Signing out of one browser leaves the other browser signed in.
6. **Given** the first publication has completed, **When** the roadmap is no longer available and the signed-in account opens a topic they opened before, **Then** the same answer key and questions are still shown, and no second publication is required.

---

### User Story 4 - Record member display names (Priority: P3)

The account that owns the group keeps a list of the people in the group. Each added person is a display name only. The owner can read the catalog. Members do not sign in. The group never has more than 8 members, counting the owner.

**Why this priority**: The group needs roster names before a later session can seat people who do not sign in. The owner can already read the catalog. The owner is always a member, and the group never grows past 8.

**Independent Test**: In a group whose catalog is already published, add two members, reject a duplicate name, reject a ninth member, rename one, remove one, and confirm a topic still opens for the owner.

**Acceptance Scenarios**:

1. **Given** the account that owns the group is signed in and the catalog is published, **When** they add a member with a display name of 1 to 40 characters, **Then** that name appears in the group's member list and the catalog still opens.
2. **Given** the account that owns the group is signed in and the catalog is published, **When** they open a topic, **Then** the answer key is shown.
3. **Given** a member named "Mina", **When** the owner adds "mina" or " Mina ", **Then** the duplicate is refused and the list still contains one Mina.
4. **Given** an existing member, **When** the owner renames them to a name that does not collide, **Then** the list shows the new name.
5. **Given** an existing member, **When** the owner removes them, **Then** the name is gone and the catalog is unchanged.
6. **Given** account A has members, **When** account B views their own group, **Then** A's member names are not shown.
7. **Given** a group already has 8 members, counting the owner, **When** the owner tries to add another name, **Then** the list stays at 8.

---

### Edge Cases

- A first publication that has not finished shows the not-available state. A half-published track, topic, or design is never shown.
- After the first publication completes, this feature does not run a second one. Later changes to the roadmap do not change what a signed-in account sees.
- A roadmap section that is absent on a topic is omitted. The product does not invent text for it. The topic still opens if the publication included that topic.
- A track is shown with however many topics the publication included, including a track with fewer or more than three topics.
- A worked design opens even when it has no review questions and no interview prompts.
- A question with no wording is not shown. The rest of the topic still opens.
- The same question wording is shown once for a topic, even if the publication contained a duplicate.
- A display name of only spaces, or longer than 40 characters, is refused.
- A group name of only spaces, or longer than 60 characters, is refused.
- Creating another group does not copy members, sessions, or pick counts. Renaming a membership does not move it into another group. An account can open each group it belongs to, one at a time.
- Signing out, closing the visit, and signing in again does not duplicate a group or its members.
- The same account may be signed in on more than one browser. Those visits are one account and one membership in each of its groups. They add one person to a session's attendance and one voter to its voting set. Signing out of one browser leaves the other signed in.
- A removed member can be added again later under the same display name.
- Password reset, member sign-in, inviting an existing account into a group that account did not create, and adding new catalog pages are not offered.
- The group has no facilitator field. The only facilitator is the member elected on a session. That member may be a roster name. The signed-in member of the group records the votes and, after election, the topics that facilitator sets.
- A session topic list of 1 topic, of 4 or more, of the same topic twice, or of a worked design is refused. Counts and reviewed marks stay unchanged.
- A topic may be picked again on a later session. One session lists each chosen topic once.
- Replacing topics on the same session adds a pick only for a topic that was not already on it, and it never lowers a count. A different session adds one pick for each topic on its saved list.
- A pick count never goes down. A topic marked reviewed stays reviewed.
- A track is not marked reviewed while any of its topics has a pick count of 0. A track with no topics is not marked reviewed.
- Never-picked topics are offered first. This does not change the published order of tracks in the catalog.
- The first session cannot be created without topics, and no session can be created before the catalog publication exists. A later session cannot receive topics before its facilitator is elected. A signed-in member of the group records the topics that facilitator sets.
- A vote for someone outside the voting set is refused. The voter must be in that set.
- A group never has more than 8 members, counting the owner. Adding a ninth name is refused.
- Every group member may attend any session of that group. Before the first vote, the people marked present can be corrected, and that list must be 2 to 8 current members. The first vote freezes those people as the voters and the candidates.
- After the first vote, attendance can gain a current member or lose people, including dropping below 2. It cannot add a person outside the group, and it cannot exceed 8. A later arrival is marked present and does not join the voting set. A person who leaves the attendance list remains a voter and a candidate, and their vote stays.
- Every session elects its own facilitator from the people in that session. The previous session's facilitator does not have to attend, and that absence does not block the new election.
- The first choice a voter casts stays. A second submission for that voter changes nothing, whether or not a facilitator has been stored.
- A person outside the group cannot be marked present.
- Removing a member from the group roster leaves them listed on sessions where they were already marked present or were already a voter. They cannot be marked present on a later session.
- In a voting set of exactly two people, a person may vote for themselves. In a voting set of three or more, a vote for oneself is refused.
- When every included person has voted and two or more candidates share the highest count, one of those candidates is chosen with equal chance and stored. A later visit shows the same facilitator. A missing vote chooses nobody.
- Two sessions may be created on the same calendar day. Nothing is unique per week.
- An account's display name in one group does not have to match that account's display name in another group. The same display name may be used in two different groups.
- A group with no session still shows the full catalog. Every topic starts at a pick count of 0.
- A signed-in account can open the answer key for each selected topic without starting a session.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A person MUST be able to create one account with an email address and a password.
- **FR-002**: The email MUST be unique across accounts, ignoring letter case. A duplicate registration MUST be refused and MUST say that the email is already in use.
- **FR-003**: The password MUST be at least 10 characters and at most 128 characters. A shorter or longer password MUST be refused and MUST NOT create an account.
- **FR-004**: An account MUST be signed in before any group, member, catalog, or answer key is shown.
- **FR-005**: A signed-in account MUST be able to create a group by entering a name of 1 to 60 characters, not counting surrounding spaces, and a display name of 1 to 40 characters for themselves in that group. An account that already belongs to a group MUST be able to create another. The new group starts with no sessions and no pick counts.
- **FR-006**: An account MAY belong to more than one group. Each membership MUST have its own display name, and those names MAY differ. A roster entry with no account MUST exist in only the group where it was added. This feature MUST NOT place an existing account into a group that account did not create.
- **FR-007**: While an account has a group open, it MUST see only that group's name and member list. It MUST be able to open each group it belongs to. It MUST NOT see a group it does not belong to.
- **FR-008**: The catalog MUST become visible only after the first publication of the senior-backend roadmap has completed.
- **FR-009**: Before that publication completes, a signed-in account MUST see a not-available state for the answer key, distinct from a catalog that published zero tracks.
- **FR-010**: A signed-in account MUST see the catalog's last modification date. That date is the UTC calendar date the publication completed, stored as `CatalogPublication.CompletedOn`.
- **FR-011**: After the first publication completes, a signed-in account MUST be able to read the stored catalog without a further publication and without the roadmap remaining available.
- **FR-012**: The catalog MUST list every track included in the first publication, in published order, and the topics inside a track in published order.
- **FR-013**: The catalog MUST list every worked design included in the first publication.
- **FR-014**: A signed-in account MUST be able to open a track and, from there, open each of its topics.
- **FR-015**: A signed-in account MUST be able to open a worked design from the catalog.
- **FR-016**: An opened topic MUST show every answer-key section supplied for it: level, what the member should be able to do, a one-minute explanation, when it matters, what it costs, a failure to recognize, what to learn, review questions, interview prompts, and an exercise. Sections the publication does not supply MUST be omitted, not filled with invented text.
- **FR-017**: Each published question on a topic MUST be readable once and labeled as either a review question or an interview prompt.
- **FR-018**: A signed-in account MUST be able to read a topic's answer key and questions when no live session exists.
- **FR-019**: A signed-in account MUST NOT be able to add, edit, reorder, or delete tracks, topics, questions, designs, or answer-key text in this feature.
- **FR-020**: The account that created the group MUST be able to add, rename, and remove members. A member is a display name of 1 to 40 characters, not counting surrounding spaces. The creator's own membership MAY be renamed and MUST NOT be removed.
- **FR-021**: Display names MUST be unique within the group, ignoring letter case and surrounding spaces. A blank or colliding name MUST be refused.
- **FR-022**: The account that created the group MUST be able to read the catalog. That account is always a member. The group MUST NOT have more than 8 members, counting that account. Adding another member past 8 MUST be refused.
- **FR-023**: Member names MUST be visible only to a signed-in account that belongs to that group.
- **FR-024**: A person who is not signed in MUST NOT see any group's name, members, sessions, tracks, topics, questions, designs, or answer key.
- **FR-025**: A wrong password MUST leave the person signed out. The failure message MUST NOT reveal whether the email is registered.
- **FR-026**: Signing out and signing in again MUST return the account to the same memberships, sessions, pick counts, and reviewed marks. An account MAY be signed in on more than one browser at the same time. Those visits MUST be that one account. They MUST share one membership per group, one attendance entry for that membership, and one vote when that membership is a voter. Signing out of one browser MUST leave any other visit signed in.
- **FR-027**: A publication that has not finished MUST NOT show a partial track, topic, or design. If the first publication has not completed, the not-available state remains.
- **FR-028**: This feature MUST NOT run the 45-minute practice blocks, send a Telegram message, or request payment.
- **FR-029**: Every account with a group MUST see the same stored catalog after the first publication. They MUST NOT see another group's name, members, sessions, votes, or elected facilitator. A session id from another group MUST be treated as not found.
- **FR-030**: The group creator MUST create the group's first session and MUST set 2 or 3 distinct topics on it at creation. Participants are recorded later, at the start.
- **FR-031**: A topic list of fewer than 2 topics, more than 3 topics, the same topic twice, a worked design, or an unknown topic MUST be refused and MUST leave the session, pick counts, and reviewed marks unchanged.
- **FR-032**: Before the first publication has completed, creating a session MUST NOT be available. With no session, the catalog MUST remain readable.
- **FR-033**: A member account of the group MUST be able to create a later session with no topics and no attendance. More than one session MAY exist on the same date.
- **FR-034**: The first accepted vote MUST freeze the voters and the candidates as the people on the attendance list at that moment. Each of those voters MUST cast one vote for one candidate in that same set. In a voting set of exactly two people, a voter MAY vote for themselves. In a voting set of three or more, a voter MUST vote for a different candidate, and a vote for themselves MUST be refused. When every voter has voted and one candidate has strictly more votes than every other candidate, that candidate MUST be the facilitator. When every voter has voted and two or more candidates share the highest count, one of those candidates MUST be chosen with equal probability and stored as the facilitator, and a later visit MUST show that same person. Until every voter has voted, the session MUST have no facilitator. A later change to attendance MUST NOT add or remove a voter or a candidate, and MUST NOT drop a vote. The first choice a voter casts MUST stay. A second submission for that same voter MUST change nothing.
- **FR-045**: Every member of the group MAY attend any session of that group. Attendance MUST NOT exceed the group, and the group MUST NOT exceed 8 members, so attendance MUST NOT exceed 8. Before the first vote, attendance MUST be 2 to 8 current group members or the save is refused. After the first vote, attendance MAY change and MAY drop below 2. It MUST NOT add a person who is not a current member of the group. A person added after the first vote MUST be marked present and MUST NOT vote or stand. A voter removed from attendance MUST remain a voter and a candidate, and the vote MUST stay. The first vote MUST be refused when attendance is missing or outside 2 to 8, and that refusal MUST create no voting set.
- **FR-035**: On every session after the first, topics MUST be saved only after a facilitator has been elected, and that save is the elected facilitator's topic list. A signed-in member account of that group records the save. The elected facilitator MAY be a roster name with no account. An account that is not a member of the group MUST NOT be able to save. The group creator's topics on the first session MUST remain the topic list for that session once the session has started. Before it starts, only the group creator MAY replace them. In this feature, a session has started when its facilitator is stored. There is no separate started status.
- **FR-036**: Replacing topics on the same session MUST add 1 pick only for a topic that was not already on it. A topic that stays MUST NOT gain another pick. A removed topic MUST keep its count. Saving topics on a different session MUST add 1 for each topic on that saved list.
- **FR-037**: Each topic MUST have a pick count for each group, starting at 0. The topic MUST be marked reviewed for that group when the count is at least 1, and MUST stay reviewed after that.
- **FR-038**: A topic that is already reviewed MUST be selectable on a later session.
- **FR-039**: A pick count MUST NOT decrease.
- **FR-040**: While topics are being chosen, topics with a pick count of 0 MUST be listed before topics with a pick count of 1 or more. Published order MUST be kept inside each of those two groups. The published order of tracks in the catalog MUST stay unchanged.
- **FR-041**: A track MUST be marked reviewed for a group only when every topic in that track has a pick count of at least 1 for that group. A track with no topics MUST NOT be marked reviewed.
- **FR-042**: Pick counts, sessions, votes, and the elected facilitator MUST belong to the group. One group's records MUST NOT change what another group sees.
- **FR-043**: The group MUST NOT store a facilitator. The facilitator MUST be stored only on the session, and only as the member elected from that session's voting set at that session's start. Every session starts by electing its own facilitator. The facilitator of an earlier session does not have to attend the later session, and that absence MUST NOT block the later election.
- **FR-044**: The account holder MUST be able to open each session topic's answer key. The 45-minute blocks, pairing, the clock, and rubric scores are not part of this feature.

### Key Entities *(include if feature involves data)*

- **Account**: The person who signs in. Identified by email. May be a member of more than one group. Not a facilitator.
- **Group**: The study circle. Has a name, the account that created it, at most 8 memberships counting that account, and any number of sessions. It has no facilitator. It does not have a private copy of the catalog.
- **Membership**: One person's place in one group. Holds the display name used in that group. May point at an account. A membership with no account is a roster name and cannot appear in a second group.
- **Session**: One meeting of a group. It has an attendance list, a voting set frozen at the first vote, an optional elected facilitator, and 2 or 3 topics once those topics have been set. Many sessions may exist in the same week.
- **Attendance**: The group members marked present. It can be corrected before the first vote, when it must be 2 to 8 current members. After that vote it can change, including dropping below 2, and it cannot exceed the group.
- **Voting set**: The voters and the candidates. Both are the people present when the first vote is cast. The size is 2 to 8. Later attendance changes leave this set as it is.
- **Vote**: One voter's choice of one candidate in that voting set. In a set of two, the choice may be the voter. In a larger set, the choice is a different person. The first choice stays. A shared lead, once every voter has voted, stores one of the leaders.
- **Topic progress**: For one group and one topic, the pick count and the reviewed mark. The count starts at 0. The topic is reviewed when the count is at least 1. Visible only inside that group.
- **Catalog publication**: The single completed first delivery of the senior-backend roadmap into the product. Its last modification date is the UTC calendar date that publication completed. This feature does not replace it with a later delivery.
- **Track**: A named chapter from that publication, holding topics in published order. For a group, the track is shown as reviewed when every topic in it has been picked at least once by that group.
- **Topic**: A named page inside one track. Holds the answer-key sections and its questions.
- **Worked design**: A named design page from that publication. Readable on its own.
- **Question**: Wording attached to one topic, labeled as a review question or an interview prompt.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new account can register, create one group, and open a published topic's answer key in under 3 minutes.
- **SC-002**: For that topic, every answer-key section supplied by the publication is visible, and every published question for that topic is visible exactly once.
- **SC-003**: In a trial with two accounts and two groups, each account sees 0 of the other group's name, members, and sessions, and both can open the same seeded topic and the same worked design.
- **SC-009**: A group creator who already has the catalog can create the first session with 3 topics and open all three answer keys in under 2 minutes.
- **SC-010**: After the creator replaces that session's topics with 2 other topics, only those 2 remain on it, and no pick count is lower than before the change.
- **SC-011**: When some topics have been picked and some have not, every never-picked topic appears before any picked topic in the chooser.
- **SC-012**: A track is marked reviewed for a group exactly when every topic in it has been picked at least once by that group, and a second group still sees that track as not reviewed.
- **SC-013**: A second session in the same week, after its members elect a facilitator, can include a topic already picked once and raise that topic's pick count from 1 to 2.
- **SC-014**: The same account can belong to two groups under two different display names, and a roster name with no account appears in only one of them.
- **SC-015**: When every voter has voted and two or more candidates share the highest count, the session shows exactly one of those candidates as facilitator, and a later visit shows that same person.
- **SC-016**: A group member who was absent when the first vote was cast casts no vote on that session. A person who arrives after that vote is marked present and is not a voter or a candidate. A voter who later leaves the attendance list keeps the vote. A person outside the group cannot be added. Attendance outside 2 to 8 people is refused before the first vote. After the first vote, attendance may drop below 2 and must stay within the current members. A group never has more than 8 members.
- **SC-017**: A second submission for a voter who has already voted leaves that voter's first choice unchanged.
- **SC-004**: With no live session ever started, and with no second publication, the same account can still open that answer key on a later sign-in the same day.
- **SC-005**: 100% of sign-in attempts with a wrong password remain signed out, and none of those attempts display catalog text.
- **SC-006**: The account that created a group can open a published topic and its answer key. A group never has more than 8 members, counting that account, and adding a ninth member is refused.
- **SC-007**: On the first attempt, at least 90% of accounts reach the answer key of a named topic without asking for help, in a trial of at least 5 accounts who have not seen the product before.
- **SC-008**: After the roadmap used for the first publication is no longer available, a signed-in account can still open that same topic and one worked design.

## Assumptions

- The person served in this feature is the signed-in account. A roster name with no account does not sign in. Several browsers signed in to the same account are still that one account.
- An account may create and belong to more than one group. The display name is stored on the membership and may differ by group. This feature does not place an existing account into a group that account did not create. Transferring the creating account is out of scope.
- The group has no facilitator and never has more than 8 members, counting the owner. Every group member may attend a session. The first vote freezes the voters and candidates as the 2 to 8 people present at that moment. Attendance may change afterward, including dropping below 2, without changing that set, and it cannot add a non-member or exceed the group. A person may vote for themselves only when the voting set has exactly two people. A single leader wins. A shared lead, once every voter has voted, stores one of the leaders with equal chance, and that person stays the facilitator. A missing vote leaves the role empty. The first choice each voter casts stays. The elected person may be a roster name. Every session elects its own facilitator. The previous session's facilitator does not have to attend. The signed-in member of the group records attendance, the votes, and the topics that facilitator sets. The creator sets topics on the first session and may replace them only before that session's facilitator is stored. Started means the facilitator is stored. A session cannot be created before the catalog publication exists. The 45-minute blocks are a later feature.
- Sign-in is email and password. A password is 10 to 128 characters. There is no single sign-on, magic link, or password reset in this feature. A lost password cannot be recovered here.
- The senior-backend roadmap is only the starting body of content. This feature uses it for one publication. Tracks, topics, questions, and worked designs in that publication are what a signed-in account can read. The roadmap is not required again for those pages to remain. The catalog page shows that publication's last modification date.
- A track is one chapter of that roadmap, such as "Language and runtime" or "Architecture." A topic is one subject inside a track, such as "Memory." A worked design is one of the roadmap's design pages, such as the URL shortener.
- A session holds 2 or 3 distinct topics once they are set. A topic may be chosen again on a later session. Worked designs are readable and are not session topics. Nothing limits a group to one session per week.
- Replacing topics on the same session adds a pick only for a newly added topic and never lowers a count. Saving topics on another session adds one pick for each topic on that list.
- A topic is marked reviewed for a group when that group has picked it at least once. A track is marked reviewed for a group when every topic in the track is reviewed for that group. Never-picked topics are offered first when choosing. Those marks and counts are private to the group. The shared catalog pages stay the same for every group.
- Adding more questions, more designs, or other practice material is later work. Code katas are an example of that later material. This feature does not add them, and a signed-in account cannot author catalog pages here. A later feature that adds them must leave the seeded pages readable. Code katas also need a constitution amendment before that later feature is planned.
- Each member's personal topic pick, reminders, the 45-minute practice blocks, pairing, the clock, and rubric scores are out of scope. Creating a session and electing its facilitator does not run those blocks.
- A voting time limit and a session schedule are later features. This feature does not store a meeting time, run a voting countdown, or send reminders. The election ends only when every voter has cast a choice, or it stays without a facilitator while a vote is missing.
- A signed-in account reads the catalog on a desktop web visit. A native mobile app is out of scope. The pages are still readable in a narrow browser window.
- Telegram, billing, payment details, and a content marketplace are out of scope.
- The account holder has ordinary web access. The first publication has already completed before the under-3-minutes task in SC-001 is timed.
- Display names and group names are stored and shown exactly as entered, aside from removing surrounding spaces when checking blanks and duplicates.
- Answer-key text and member names are shown only inside a signed-in visit. This feature does not email them. Member names stay inside the owning group. The seeded answer key is visible to every signed-in account.
