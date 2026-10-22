# Contract: Account pages

The signed-in person is an account. An account may belong to more than one group, with a display name chosen per group. This feature has no group facilitator role. The facilitator exists only on a session, after the members in that session elect one. Server-rendered pages. A signed-out request to any page below the account pages redirects to sign-in and shows no catalog, member, session, or progress text. Dates are UTC calendar dates.

The open group is one group the account belongs to. Switching groups shows that group's members, sessions, and pick counts. It does not copy them.

## Account

| Action | Input | Success | Failure |
| --- | --- | --- | --- |
| GET `/account/register` | None | Empty form | |
| POST `/account/register` | Email, password | Account created, signed in, redirected to create-group | Invalid email or password shorter than 10 or longer than 128 stays on the form. Duplicate email, ignoring case, says the email is already in use and does not sign anyone in. |
| GET `/account/sign-in` | None | Empty form | |
| POST `/account/sign-in` | Email, password | Signed in. No group: create-group. One group: that group's catalog. More than one group: the group list. | Wrong password or unknown email: same message, still signed out, no catalog text. |
| POST `/account/sign-out` | Cookie | That browser's cookie is cleared, and that browser is redirected to sign-in. Another browser still signed in to the same account stays signed in. | |

The same account may be signed in on more than one browser. Both visits read and write the same membership, attendance entry, and vote. A second browser does not add a second person.

The sign-in cookie is HttpOnly, SameSite=Lax, and Secure when the site is served over HTTPS. It slides for 14 days of activity. Sign-out deletes only that browser's cookie. A password is 10 to 128 characters. The stored value is a `PasswordHasher` hash with a unique salt inside the hash string. The plaintext password is never stored or logged.

## Group

| Action | Input | Success | Failure |
| --- | --- | --- | --- |
| GET `/groups` | Signed in | The groups this account belongs to | No group: redirect to create-group. |
| GET `/groups/new` | Signed in | Name form and display-name form, including when the account already has a group | |
| POST `/groups` | Group name, display name for this account in the new group | Group created. The account is its creator and a member under that display name. Redirect to that group's catalog. Members, sessions, and pick counts start empty. | Blank or over 60 characters for the group name, or a blank or over-40-character display name: form redisplays, no group. |
| POST `/groups/{groupId}/open` | A group this account belongs to | That group becomes the open group | Any other group id: not found. |

## Catalog

Requires a signed-in account with an open group.

| Action | Success | Empty |
| --- | --- | --- |
| GET `/catalog` | Tracks in publication order, worked designs in publication order, and the catalog's last modification date (`CatalogPublication.CompletedOn`, the day publication completed) | No completed publication: not-available message. No track list that could be mistaken for a finished empty catalog. The account that created the group can open this page. |
| GET `/catalog/tracks/{trackKey}` | Topic titles in publication order. Reviewed badge only when every topic in the track has been picked at least once by the open group. | Unknown key: not found. |
| GET `/catalog/topics/{topicKey}` | Level, "you can", unlabeled body if any, then only the sections that were published, in the standard order. Each question once, labeled Review or Interview. The open group's pick count and reviewed mark. | Unknown key: not found. |
| GET `/catalog/designs/{designKey}` | Title and published body. No control that puts the design on a session. | Unknown key: not found. |

No page accepts edits to title, section text, questions, or track membership. The page does not show a stored track count, topic count, or design count. It shows the rows that were published.

## Sessions

| Action | Input | Success | Failure |
| --- | --- | --- | --- |
| GET `/sessions` | Signed in, catalog published | This group's sessions by number: date, topic titles or "not set", and the elected facilitator or "not elected". Links to each topic's answer key. | No publication: sessions unavailable. No session yet: empty list plus a way to create the first session. The catalog link still works. |
| POST `/sessions` | First session: 2 or 3 distinct topic ids in order. Later session: no topics. | Session 1 is created by the group creator with those topics and no attendance. Each topic's pick count increases by 1. No facilitator yet. A later session is created by a member account of the group with no topics, no attendance, and no facilitator. Several sessions may share a date. | No publication: nothing created. Session 1 without 2 or 3 topics, or a later session that includes topics: nothing created. One topic, four or more, a duplicate, a design id, or an unknown id: nothing created. |
| POST `/sessions/{sessionId}/attendance` | The member ids present, each a current member of this group. | Before the first vote, a list of 2 to 8 replaces attendance. After the first vote, the list may drop below 2 and replaces attendance only. It cannot exceed the group, and the group never has more than 8 members. Voters, candidates, and votes stay. A person added after the first vote is present and is not a voter or a candidate. A voter removed from the list stays a voter and a candidate. | Before the first vote: fewer than 2, more than 8, a duplicate, a member from another group, a removed member, or a person who is not a member leaves attendance and the voting set unchanged. After the first vote, a non-member is still refused. A session id from another group is not found. |
| POST `/sessions/{sessionId}/topics` | Two or three distinct topic ids, in order | Session 1, before election, and the signed-in account is the group creator: replaces that session's topics. A topic newly added gains 1 pick. A topic that stays does not. A removed topic keeps its count. Later session, after election: the signed-in member of the group saves the elected facilitator's list. The first save on that session adds 1 for every included topic. A replacement on that same session adds 1 only for a topic that was not already on it. | Session 1 after election: no change. Later session before election: no change. An account that is not a member of the group: no change. One topic, four or more, a duplicate, a design id, or an unknown id: no change to topics, counts, or reviewed marks. |
| POST `/sessions/{sessionId}/votes` | One candidate member id for a voter. On the first accepted vote, the candidate must be on the current attendance list. After that, voter and candidate must both be in the frozen voting set. In a set of two, the candidate may be the voter. | The first accepted vote copies attendance into the voting set, then stores the vote. A later submission for a voter who has no vote yet stores that choice. When every voter has voted and one candidate leads, that candidate is the facilitator and votes lock. When every voter has voted and two or more candidates share the highest count, production stores one of those leaders with equal probability and votes lock. Tests replace that choice with one of the leaders. A later request returns that same facilitator. The winner may be a roster name. A later session elects from its own voting set when the previous facilitator is absent. | Attendance missing or outside 2 to 8 on the first vote: no voting set and no vote. A missing vote leaves the facilitator empty. A vote for someone outside the voting set is refused. In a set of three or more, a vote for oneself is refused. A second submission for a voter who already voted is refused and the first choice stays. After a facilitator is stored, further votes are refused. A session id from another group is not found. |

The chooser lists the open group's never-picked topics first, then picked topics. Published track and topic order is kept inside each of those groups.

Pick counts and reviewed badges on these pages are the open group's only.

## Members

| Action | Input | Success | Failure |
| --- | --- | --- | --- |
| GET `/members` | | The open group's names only | |
| POST `/members` | Display name | Roster name appears, with no account. Catalog and sessions unchanged. | Blank, over 40 characters, or a case-insensitive duplicate: list unchanged. A group that already has 8 members, counting the creator, refuses another name. |
| POST `/members/{memberId}/rename` | New display name | Name changes | Collision or blank: list unchanged. A member id from a group this account does not belong to: not found. |
| POST `/members/{memberId}/remove` | | Name leaves the roster. Sessions they already attended still list them. Counts unchanged. They cannot be marked present again. | The creator's membership: list unchanged. A member id from a group this account does not belong to: not found. |

Adding the same name again after removal succeeds. There is no action that attaches an existing account to a group that account did not create.

## Isolation

Account B, signed in, receives none of a group B does not belong to: not its name, member names, sessions, votes, facilitator, pick counts, or reviewed marks. B can open the same topic and design keys. A's topic, while B has B's own group open, shows B's own count, which starts at 0. An account that belongs to two groups sees each group's members, sessions, and counts only while that group is open.
