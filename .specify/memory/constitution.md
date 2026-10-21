# Quorum Constitution

## Core Principles

### I. Quorum of Two

A live session MUST be startable and finishable by any two members of a
group. A session MUST NOT depend on one person's advance preparation, a
designated presenter, or a headcount above two.

An odd count MUST be seated so that nobody is only an observer. Three
people form a ring: each person asks one other. A supported live session
is 2 to 8 people. Specs MUST NOT extend this format to a larger room.

Rationale: these groups fail when the person who prepared is absent. The
reason to pay is a useful session on the night people actually attend.

### II. Everyone Practices

In every live block, each seated person MUST have an active turn: ask,
answer, or state one decision and one rejected alternative. A live mode
that is a presentation with an audience MUST NOT ship.

The person asking MUST be able to run the turn from the page on screen.
That page is the answer key. Writing questions beforehand MUST NOT be
required.

Attendance, turns taken, and logged exercises MUST be storable on their own.
The product MUST NOT require a numeric rank of members in order to store
that work. A leaderboard of interview performance MUST NOT ship.

Rationale: buyers already have free calls. They pay for practice that
includes everyone and does not punish a missed week.

### III. The Runner Is the Product

Pairing, question draw, the clock, and score capture are the product.
Content is data. Authoritative pages live outside the application and MUST
be synced into the database before a session reads them. Session code at
runtime MUST read the database, not a git repository and not copies of
pages inside the web app.

A second content source MUST be addable by an adapter without changing
pairing, draw, the clock, or score storage. The first catalog is the
senior-backend roadmap at
https://github.com/xolghadr/software-engineer-roadmap. The core model MUST
NOT assume that repository is the only catalog.

Question draw MUST NOT repeat a question inside the same pair in a
session. Questions a group writes, when that feature exists, MUST be tagged
with a source other than the curated catalog and MUST be readable only
inside that group.

Score storage MUST record the rubric items the catalog defines. v1 ships
one rubric. Adding a rubric item MUST NOT require a change to pairing or
the session clock.

Rationale: the asset a second customer buys is the loop, not a hard-coded
repository. An adapter is enough. A content marketplace is not.

### IV. Group-Scoped from the First Table

A group is the customer: one study circle, bootcamp pod, or team.
Sessions, members, units, scores, and question history MUST be stored and
queried by group. v1 MAY run a single group and MAY omit billing.

The schema MUST allow a second group without splitting tables or rewriting
session code. Billing integration, single sign-on, a content marketplace,
and native mobile apps MUST NOT be built before two real groups have each
completed four sessions. A manual invoice is allowed and MUST NOT be a
runtime dependency.

Rationale: a group column is cheap on day one and expensive after the first
paying customer. A payments platform before the loop is used keeps the
product a prototype.

### V. A Facilitator Runs It Alone

A facilitator who did not write Quorum MUST be able to open a session, seat
whoever is present, advance the blocks, show the current question, record
the rubric, and close the session from the web UI.

The web UI is the system of record for live state. Notification channels,
including Telegram, are adapters. A session MUST be completable with no
adapter connected. Voice and video are a link the facilitator supplies.
Quorum MUST NOT host media.

Rationale: a tool that needs its author on the call cannot be sold. A
second client, before the web loop has been used for a real session, adds
surface without a buyer.

## Product Boundaries

Quorum runs a short live practice session for a small engineering group.

The default session, which a spec MAY retune after a real run and MUST NOT
replace, is about 45 minutes and three jobs in order:

1. Shared retrieval on the week's topics, in pairs.
2. Personal retrieval: each person is questioned on a topic they picked, and
   that topic has a page so an answer key exists.
3. One group tradeoff. Each person adds one decision and one rejected
   option. With only two people this job is a one-on-one debate.

The close stores a self-score and the next unit. With two people present,
the session still runs all three jobs.

The v1 rubric, for the senior-backend catalog, marks a topic known only
when the member can explain it in their own words, name the problem it
solves, write a C# example, give a backend example, answer one interview
question, and distinguish it from a nearby concept. The interviewer records
that rubric during the turn.

Stack, until amended:

- ASP.NET Core modular monolith. Each feature folder is a vertical slice.
- The shared kernel is limited to group identity, member identity, time,
  and references to synced content. Feature rules stay in the feature.
- One database. Sync from a structured content repository is an explicit
  step. Runtime reads come from the database.
- One process on one small virtual machine is a sufficient production
  shape. A session MUST be runnable without a cluster, a second region, or
  an external account for payments, media, identity, or messaging.
- A facilitator account is required to create and close sessions. A member
  joins a live session with a display name and a session link. A Telegram
  identifier MAY be stored as contact data and MUST NOT be the only key
  for a member.

Privacy and content rights:

- Scores and written answers are visible to the member who produced them
  and to facilitators of that group. Another member sees feedback only
  inside the live turn they share.
- Logs MUST NOT contain answer text, rubric notes, or scores.
- Items a group authors MUST NOT be readable by another group.
- The curated catalog is offered to groups only under terms the project
  owner sets. Group-authored items are not part of that catalog.

Outside this constitution, both before and after Principle V is
demonstrated: coding katas, automatic breakout rooms, points, streaks,
seasons, boss fights, in-app video, billing integration, a content
marketplace, single sign-on, and native mobile apps. Adding any of them
requires an amendment.

## Delivery Bar

A spec MUST name the person served (facilitator or member) and the session
outcome that changes. Work that does not change a session outcome waits.

These are release blockers:

- Two people cannot start and finish a session.
- An odd count leaves someone without a turn.
- A question repeats inside the same pair.
- A query can return another group's sessions, scores, or authored items.
- The web path cannot advance blocks, draw a question, and store a rubric
  result.

Automated tests MUST cover those blockers before the behavior is called
done. Test-first applies to session state, pairing, question draw, and
group scoping. An adapter or a visual change needs one happy-path check,
not a test per control.

A new service, datastore, or repository MUST be justified in the spec by a
constraint the monolith and one database cannot meet. The default is to
decline it.

Ship order follows the principles. First: catalog sync, the session runner,
and the web UI that satisfies Principle V. Next: a recurring group can set
the next unit and send a reminder. After that: effort tracking. Game
mechanics stay outside this constitution.

## Governance

This constitution overrides conflicting text in specs, plans, and tasks.
The root `CONSTITUTION.md` file is the 2026-10-20 brainstorm. Where it
disagrees with this file, this file wins.

An amendment is a change to this file that states the version bump and
lists specs to reconcile. The amendment is accepted when it is merged. If
a spec already in progress would violate the new text, the amendment MUST
include a short migration note.

Versioning:

- MAJOR: a principle is removed or redefined so that an existing spec would
  violate it. Examples: dropping the quorum of two, hosting media, or
  making one repository the only legal content source.
- MINOR: a new principle, or a new binding rule in Product Boundaries or
  Delivery Bar.
- PATCH: clarification that does not change what a spec may do.

Compliance review on every spec and plan checks all of the following:
quorum of two, no observer when the count is odd, the web path completes
with no adapter, queries are group-scoped, the rubric is unchanged or this
file is amended, and no out-of-scope item is in the milestone. Any step
past one process and one database is justified in the spec.

Specs and plans hold implementation detail. This file holds rules that
stay true across features.

**Version**: 1.0.0 | **Ratified**: 2026-10-22 | **Last Amended**: 2026-10-22
