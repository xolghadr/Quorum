# Research: Group Catalog

## Decision: .NET 10 and Razor Pages

**Decision**: Build one ASP.NET Core app on .NET 10. The account UI is Razor Pages posted back to the same process.

**Rationale**: The constitution locks an ASP.NET Core modular monolith. The roadmap itself treats .NET 10 as the current long-term release. Server-rendered pages match a reading product: long answer keys, a few forms, no client build. One process stays inside the hosting rule.

**Alternatives considered**: A separate SPA would add a second toolchain and a JSON API the spec does not ask for. Blazor would add a component model this feature does not need. Minimal APIs without pages would leave the account holder without the web path the constitution requires.

## Decision: PostgreSQL and one DbContext

**Decision**: PostgreSQL 17 through EF Core and Npgsql. Feature handlers call `QuorumDbContext`. There is no repository interface.

**Rationale**: The constitution allows one database and rejects an extra repository layer. Session topic saves and member edits are writes from more than one account. PostgreSQL transactions make the publish command atomic. The same engine is used in tests so group-isolation bugs are not hidden by a different database.

**Alternatives considered**: SQLite is one file and an easier laptop setup. Its writer lock is a poor match for two accounts saving sessions, and a second engine in tests would weaken the isolation checks. A document store would be a second kind of datastore.

## Decision: Cookie sign-in inside the process

**Decision**: Email and password on an `Account` row. A password must be 10 to 128 characters. That length is checked before hashing. An accepted password is hashed with ASP.NET Core `PasswordHasher`, which generates a unique salt per password and stores the salt inside the hash string. There is no salt column. The plaintext password is never stored and never logged. The sign-in cookie is HttpOnly, SameSite=Lax, and Secure when the request is HTTPS. It slides for 14 days of activity and ends at sign-out on that browser. The same account may hold a cookie in more than one browser, and those visits are one account, one membership per group, and one voter. Wrong password and unknown email return the same message. A duplicate email, ignoring case, is refused at registration with "email already in use." The account is not a facilitator. The group stores the creating account id and no facilitator role. A group has at most 8 memberships, counting the creator.

**Rationale**: The constitution forbids an external identity account. The spec requires email, a password of 10 to 128 characters, and a generic failure. `PasswordHasher` already embeds a unique salt, so a separate salt column would only duplicate that secret. Fourteen days avoids a surprise between two sessions in one week while still requiring the sign-in the return-visit story tests. Registration does not send mail, because password reset and mail are out of scope. The group cap of 8 is the room size, so attendance cannot grow past the group.

**Alternatives considered**: ASP.NET Identity's full stack brings roles, tokens, and external logins this feature must not have. A browser session that dies when the browser closes fails an account holder who continues the next morning. An open-ended cookie is harder to reason about on a shared computer; 14 days plus an explicit sign-out is the bound.

## Decision: One publication, all or nothing

**Decision**: `publish-catalog` reads a local checkout of the senior-backend roadmap and inserts tracks, topics, questions, and designs in one database transaction. If the command fails, the database is unchanged and a signed-in account still sees the not-available state. If a completed publication already exists, the command exits without writing. `CompletedOn` is the catalog's last modification date, and the catalog page shows it. No session is created before that publication exists.

**Rationale**: The spec says a half-published catalog is never shown, and this feature does not run a second publication. A transaction is the straightforward way to get both. The publication row stores the source key and the date only. Tests count child rows for 7 tracks, 72 topics, and 6 designs, so a stale total cannot disagree with the rows. The account holder never runs this command.

**Alternatives considered**: Streaming commits per track would show a partial catalog, which the spec forbids. Re-running the import to pick up roadmap edits would violate the one-seed rule. Copying markdown into the web project would make runtime reads depend on files.

## Decision: Parser rules for this roadmap

**Decision**: A topic is any heading whose following lines contain `**Level:**`, including `###` headings under `## Good to have`. `## Good to have` itself is not a topic. Known `###` blocks (`In one minute`, `When it matters`, `What it costs`, `A failure you should recognize`, `Learn`, `Review`, `In an interview`, `Exercise`) are sections. Numbered items under Review are review questions. Numbered items under In an interview are interview prompts; a single paragraph there is one interview prompt. Prose that is not under a known heading is stored as unlabeled body text and shown without an invented title. The six files in `designs/` are worked designs. Their body is stored as published markdown. They are not topics and cannot be placed on a session.

**Rationale**: The live roadmap has 72 `**Level:**` entries across the seven track files and 6 designs. Several good-to-have topics are `###` headings and do not use every standard section. The spec says to omit missing sections and not invent text. Counting those 72 as the track's topics makes "track reviewed" mean every one of them has been picked, including good-to-have topics. Track 5 alone has 33.

**Alternatives considered**: Treating only `##` headings as topics would drop Hollywood principle, mentoring, and the other good-to-have pages. Treating only must-have topics as required for a reviewed track would disagree with the spec, which says every topic in the track. Parsing designs into the topic section template would hide their real headings (`The problem`, `Numbers to say out loud`, and the rest).

## Decision: Stable content keys

**Decision**: Each track, topic, and design gets a stable string key from its file and heading, such as `04-data-and-apis/transactions-and-isolation`. The database also uses an internal id. Topic progress references the internal topic id. Keys survive a re-read of the same publication and are what tests assert.

**Rationale**: Pick counts must keep pointing at the same topic. A key derived from the roadmap is readable in tests and logs without storing question text in the log.

**Alternatives considered**: Hashing the whole page would change the key when a typo is fixed, which matters if a later feature allows another publication. An integer-only id is opaque in the publish tests.

## Decision: Reviewed marks are derived

**Decision**: Store `PickCount` per group and topic. A topic is reviewed when `PickCount >= 1`. A track is reviewed when every topic in it has `PickCount >= 1` for that group. Do not store a separate reviewed bit.

**Rationale**: The spec defines both marks from the count. A stored bit can drift from the count. Seventy-two rows per group is small enough to compute while rendering the track list.

**Alternatives considered**: A `TrackProgress` row would need a write on every pick and a repair job when it disagrees. Computing it in the page and again in the session handler risks two formulas. Storing track, topic, and design totals on the publication row has the same drift: the publish test counts the child rows instead.

## Decision: Sessions, not a week

**Decision**: A group has many sessions, numbered from 1, including more than one on the same date. There is no week row. The group creator creates session 1 with 2 or 3 topics and no attendance. A member account creates each later session with no topics and no attendance. Every group member may attend. Before the first vote, attendance must be 2 to 8 or the save writes nothing. The first accepted vote copies that attendance into the voting set, and only those people vote and stand. Later attendance changes do not change the voting set or the votes. A later arrival is present and has no vote. A voter who leaves attendance keeps the vote. In a voting set of two, a vote may be for themselves. In a larger set, a self-vote is refused. A single leader becomes the facilitator. When everyone has voted and two or more candidates share the highest count, production chooses one of those leaders with equal probability and stores that member. Tests replace that choice with one of the leaders so a tie is repeatable, and a later read returns the stored member. A missing vote elects nobody. The facilitator may be a roster name. Every session elects its own facilitator. The previous session's facilitator does not have to attend. After the first vote, attendance may drop below 2 and must stay within the current members. The group never has more than 8 members, so attendance never exceeds 8. Creating a session before the publication exists writes nothing. The first choice each voter casts stays, and a second submission for that voter is refused. Votes lock after a facilitator is stored. Session 1's topics can be replaced by the creator only before that election. Later sessions get topics only after election. The signed-in member of the group records those topics. Replacing topics on the same session adds 1 pick only for a topic newly added. Saving a different session's list adds 1 for each topic on it. An illegal list writes nothing. This feature does not invite a second account into a group, and it does not run the 45-minute blocks.

**Rationale**: A group may meet two or three times in a week, so a single current week cannot hold the topic lists. The constitution forbids a standing facilitator and says the members in the session elect one at the start. Roster names do not sign in, so the account that belongs to the group is the person who records the ballots and the facilitator's topics. One save action cannot mean both "fix this session" and "count this topic again."

**Alternatives considered**: Keeping one current week and adding extra meetings as notes would hide the second topic list. Requiring the elected facilitator to have an account would make a roster name unable to facilitate, and this feature has no invite that would give every member an account. Incrementing on every save of the same session double-counts a mis-click. Decrementing when a topic leaves the session contradicts the rule that a count never goes down.

## Decision: Chooser order

**Decision**: The chooser lists topics with `PickCount = 0` first, then the rest. Inside each group, order is track publication order, then topic order inside the track. The catalog's track list stays in publication order and is not rearranged by progress.

**Rationale**: The spec asks for new topics first without changing how the catalog is read.

**Alternatives considered**: Sorting the track list by "least reviewed" would hide the roadmap's order, which the spec keeps.

## Decision: Calendar dates in UTC

**Decision**: Publication dates and session dates are the UTC calendar date from `IClock`. Tests supply the clock. The catalog page shows `CompletedOn` as the catalog's last modification date. Session pages show the session's creation date, which is not a meeting time.

**Rationale**: The spec requires a calendar date and does not name a time zone. One VM with UTC is a single rule. The clock port keeps "today" testable.

**Alternatives considered**: The browser's local date would make the same save show two dates for two account holders. A per-group time zone is a setting this feature does not have.

## Decision: What this feature will not build

**Decision**: No 45-minute runner, no pairing, no clock, no rubric scores, no reminder sender, no authoring screen, no second content kind for katas, no password reset, no member login, and no invite of an existing account into a group that account did not create. Sessions, votes, the elected facilitator, and session topics are in this feature.

**Rationale**: The live practice blocks are a later feature. The schema leaves room for a later source key and for group-scoped rows. It does not reserve kata tables.

**Alternatives considered**: Adding a `ContentKind` enum value of `Kata` now would put an out-of-scope item in the milestone. The constitution requires an amendment before katas are planned. Building the election only inside the later runner would leave this feature with a week table the constitution no longer allows.
