# Data Model: Group Catalog

Rules below are the ones tests must enforce. Field names are the model, not a migration script.

## Account

The person who signs in. An account may have a membership in more than one group. The account is not a facilitator.

| Field | Rule |
| --- | --- |
| Id | Internal id |
| Email | Required, trimmed, stored lowercase, unique. Reject an invalid email. |
| PasswordHash | Required. Plain password is never stored and never logged. Length 10 to 128 is checked before hashing. ASP.NET Core `PasswordHasher` generates a unique salt per password and stores that salt inside the hash string. There is no salt column. |
| CreatedAt | UTC instant from `IClock` |

**Relationships**: One account has many memberships, one per group. No facilitator role is stored here.

## Group

| Field | Rule |
| --- | --- |
| Id | Internal id |
| CreatorAccountId | Required. The account that created the group. Not unique: one account may create more than one group. This is not a facilitator. |
| Name | Trimmed, 1 to 60 characters. A blank or longer name is rejected. Stored with the surrounding spaces removed and the inner text unchanged. |
| CreatedAt | UTC instant |

**Relationships**: One group has at most 8 memberships, counting the creator, many sessions, and many topic-progress rows. Adding a ninth membership is refused. A group does not own catalog rows and has no facilitator column.

## Membership

One person's place in one group. The display name belongs here, so the same account can use a different name in another group.

| Field | Rule |
| --- | --- |
| Id | Internal id |
| GroupId | Required. Every query filters on a group the signed-in account belongs to. |
| AccountId | Optional. When set, unique inside the group. The same account may have a membership in another group. |
| DisplayName | Trimmed, 1 to 40 characters. Unique within the group when compared case-insensitively after trim. Not required to match the same account's name in another group. |
| CreatedAt | UTC instant |

**Rules**: A membership with no account is a roster name and cannot be attached to a second group. This feature does not attach an existing account to a group that account did not create. A group has at most 8 memberships, counting the creator. Adding another when the group already has 8 is refused. Removing a membership takes that person off the roster and does not change session topic lists or pick counts. A session where they were already marked present, or where they were already a voter, still lists them. They cannot be marked present on a later session. The same display name may be added again after removal. The group creator has a membership in the group they create, with the display name they choose for that group. That membership may be renamed. It may not be removed. The creator can read the catalog.

## CatalogPublication

The single completed seed. Absence of this row is the not-available state.

| Field | Rule |
| --- | --- |
| Id | Internal id |
| SourceKey | `senior-backend-roadmap` for this feature |
| CompletedOn | UTC calendar date. This is the catalog's last modification date: the day the one publication completed. The catalog page shows it. |

Track, topic, and design totals are not stored. They are the number of child rows. The publish test expects 7 tracks, 72 topics, and 6 designs by counting those rows.

**Rules**: At most one row. The publish transaction inserts this row last, after all catalog rows. A failure rolls the whole transaction back, so a partial publication cannot be read. A second invocation finds this row and writes nothing.

## Track

| Field | Rule |
| --- | --- |
| Id | Internal id |
| PublicationId | Required |
| Key | Unique, stable, from the track file name (`04-data-and-apis`) |
| Title | Heading text |
| SortOrder | File order, starting at 1 |

**Reviewed**: Not stored. For a group, the track is reviewed when it has at least one topic and every topic has `PickCount >= 1` for that group. A track with no topics is not reviewed.

## Topic

| Field | Rule |
| --- | --- |
| Id | Internal id |
| TrackId | Required |
| Key | Unique, stable (`{track-key}/{slug}`) |
| Title | Heading text |
| SortOrder | Order inside the track, including good-to-have topics |
| Level | `Must-have` or `Good-to-have`, from `**Level:**` |
| YouCan | Text of `**You can:**`. Empty if absent. |
| Body | Prose not under a known section. Empty if absent. Shown without an invented heading. |

**Relationships**: Many sections, many questions. A topic is the only thing that may appear on a session.

## TopicSection

| Field | Rule |
| --- | --- |
| Id | Internal id |
| TopicId | Required |
| Key | One of `in-one-minute`, `when-it-matters`, `what-it-costs`, `failure`, `learn`, `review`, `interview`, `exercise` |
| SortOrder | The order listed above, skipping keys the topic does not have |
| Markdown | The section body as published. Not invented when missing. |

Review and interview sections are still stored so the page can show them. Their questions are also rows, so the count and the "shown once" rule have a single list to render.

## Question

| Field | Rule |
| --- | --- |
| Id | Internal id |
| TopicId | Required |
| Kind | `Review` or `Interview` |
| SortOrder | Order inside that kind |
| Text | Trimmed wording. An empty item is not inserted. |
| Fingerprint | Hash of kind plus normalized text, unique per topic |

**Rules**: The same wording and kind is inserted once per topic. Review questions come from numbered items under Review. Interview prompts come from numbered items, or from the whole paragraph when the section is not a list.

## WorkedDesign

| Field | Rule |
| --- | --- |
| Id | Internal id |
| PublicationId | Required |
| Key | Unique, from the file name (`url-shortener`) |
| Title | Heading text |
| SortOrder | Order in the roadmap's design list |
| Level | From `**Level:**` |
| Markdown | The page body after the title and level, as published |

A design has no questions in this model and cannot be referenced by a session.

## Session

One meeting. A group may have many, including more than one on the same date. There is no week key.

| Field | Rule |
| --- | --- |
| Id | Internal id |
| GroupId | Required |
| Number | 1 for the first session of the group, then 2, 3, and so on |
| CreatedByAccountId | Required. For session 1 this is the group creator. For a later session this is a member account of the group. |
| CreatedOn | UTC calendar date |
| FacilitatorMembershipId | Empty until election succeeds. Must be in this session's voting set. |

**Relationships**: `SessionAttendance` rows are the people marked present. `SessionVoter` rows are the frozen voters and candidates. Zero topics until they are set, then two or three `SessionTopic` rows. Many `SessionVote` rows.

## SessionAttendance

One group member marked present for this session.

| Field | Rule |
| --- | --- |
| SessionId | Required |
| MembershipId | Required. The membership must belong to the session's group and be on the roster when this row is saved. |
| | Unique together |

**Rules**: Before the first vote, replacing the list succeeds only when the new count is 2 to 8 current members. Any other count leaves the previous list unchanged. After the first vote, the list may shrink, including below 2, and that change does not add or remove `SessionVoter` rows or votes. The list must contain only current members of the group. It cannot exceed 8, because the group itself never has more than 8 members. A person outside the group has no row. Every current group member is eligible to be marked present at any session of that group.

## SessionVoter

One person who may vote and who may be elected. The candidate set is this same set.

| Field | Rule |
| --- | --- |
| SessionId | Required |
| MembershipId | Required. Copied from attendance. |
| | Unique together |

**Rules**: Created when the first vote is accepted, as a copy of the attendance list at that moment. That copy must contain 2 to 8 people. If attendance is missing or outside 2 to 8, the first vote is refused and no row is created. Later attendance changes do not change these rows. A voter removed from attendance keeps this row. A person added to attendance after this copy has no row here.

## SessionVote

| Field | Rule |
| --- | --- |
| SessionId | Required |
| VoterMembershipId | Required. Must be a `SessionVoter` for this session. One vote per voter. A second submission for the same voter is refused and the stored choice stays. |
| CandidateMembershipId | Required. Must be a `SessionVoter` for this session. |

**Election**: A signed-in member account of the group records attendance, then one vote per voter. The first accepted vote copies attendance into `SessionVoter`. A roster name may vote and may be elected. In a voting set of exactly two people, the candidate may be the voter. In a larger set, a vote for the voter is refused. When every voter has voted and one candidate has strictly more votes than every other candidate, that candidate is stored as the facilitator and the votes lock. When every voter has voted and two or more candidates share the highest count, production chooses one of those leaders with equal probability, stores that member as the facilitator, and the votes lock. Tests replace that choice with one of the leaders so the result is repeatable. A later read returns that same member. A missing vote leaves the facilitator empty. A second submission for a voter who already has a vote is refused, and the first choice stays. A vote for someone outside the voting set is refused. Changing attendance after the copy does not change these votes. Each session elects from its own voting set. The previous session's facilitator does not have to be in the new set.

## SessionTopic

| Field | Rule |
| --- | --- |
| SessionId | Required |
| TopicId | Required. Must belong to the completed publication. A design id is rejected. |
| Position | 1, 2, or 3. Unique inside the session. |

**Rules**: Zero topics, or two or three. `TopicId` unique inside the session. The voting set comes from attendance at the first vote, not from creation. Session 1 receives its topics from the group creator at creation. Only that creator may replace them, and only before election succeeds. After election, session 1's topics stay. A later session receives topics only after its facilitator is elected. The signed-in member account of the group records that list. It is the elected facilitator's list, including when the facilitator is a roster name. Replacing topics on the same session adds 1 pick only for a topic that was not already on it. The first save on a later session adds 1 for each topic on the list. An illegal list does not change topics or progress. An account that is not a member of the group cannot write the session.

## TopicProgress

Per group, per topic. Created on the first pick. Missing row means count 0 and not reviewed.

| Field | Rule |
| --- | --- |
| Id | Internal id |
| GroupId | Required |
| TopicId | Required. Unique together with `GroupId`. |
| PickCount | Integer, minimum 0. Never decreases. A reviewed topic is one with `PickCount >= 1`. |

## State transitions

### Publication

1. No `CatalogPublication` row: catalog pages show the not-available state. Creating a session writes nothing.
2. `publish-catalog` succeeds: one publication row, full catalog visible, sessions become available. The creating account can read that catalog without any other member. Track, topic, and design totals are the counts of those rows. `CompletedOn` is the last modification date shown on the catalog page.
3. `publish-catalog` fails or a completed row already exists: stay in the previous state. Existing sessions and pick counts are untouched.

### Session

1. A group with no session still shows the catalog. Every topic has pick count 0 until a session includes it.
2. The group creator creates session 1 with 2 or 3 distinct topics and no attendance. Each of those topics gains 1 pick. The facilitator is empty. The creator may replace those topics before election: a topic newly added gains 1, a topic that stays does not, a topic removed keeps its count.
3. A member account of the group creates a later session with no attendance and no topics. More than one session may share a calendar date. Earlier sessions stay as they are.
4. Before the first vote, attendance saves only a list of 2 to 8 current group members. The first accepted vote copies that list into the voting set. Later attendance edits may drop below 2 and must stay inside the current members, never past 8. They do not change the voting set or the votes. Votes accumulate until every voter has one. A single leader is stored as facilitator and the votes lock. A shared lead stores one of the leaders. Production chooses that leader with equal probability. Tests replace that choice. The votes lock. A missing vote leaves the facilitator empty. A second submission for the same voter is refused. A self-vote is accepted only when the voting set has exactly two people.
5. After election, session 1's topics no longer change. On a later session, the signed-in member of the group may save 2 or 3 topics. That first save adds 1 pick for each topic on the list. A later replacement on that same session adds 1 only for a topic that was not already on it.
6. A save of 1 topic, 4 or more, a duplicate, a design, or an unknown topic writes nothing.
7. A save before election, or a save by an account that is not a member of the group, writes nothing.
8. The previous session's facilitator does not have to attend. Their absence does not block the new session's election.

### Account and group

1. Register signs the account in and does not create a group.
2. Create group stores the account as creator and as a member, with the display name chosen for that group. No facilitator is stored. The group never grows past 8 memberships, counting that creator. The same account may create another group with a different display name. Members, sessions, and pick counts are not copied.
3. Sign-out hides every group, catalog, session, and progress page. Sign-in returns the same rows.

## Query boundaries

- Catalog reads require a signed-in account who belongs to at least one group, and a completed publication. They do not filter topics by group. Pick counts and reviewed marks shown beside a topic are for the group that is open.
- Membership, session, vote, and topic-progress reads and writes use a `GroupId` the signed-in account belongs to. A posted id from any other group is treated as not found, including a group this account does not belong to.
- Logs may include account id, group id, session id, topic key, and command name. They must not include section markdown, question text, design markdown, email, or display name.

## Not built here

This feature stores the session, who is in it, the votes, the elected facilitator, and the 2 or 3 topics. It does not run the 45-minute blocks, pairing, the clock, or rubric scores. It does not send reminders. A later feature must not add a facilitator column to `Group` or `Account`.
