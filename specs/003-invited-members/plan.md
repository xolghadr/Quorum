# Implementation Plan: Invited Members

**Branch**: `003-invited-members` | **Date**: 2026-10-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-invited-members/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

An accepted member invites a person by email. No message is sent. The invitation waits on that person's home, including when they register later with the same email. Accepting chooses a display name and creates a membership tied to that account. That member opens the group, marks only their own attendance, and casts only their own vote. The group creator still creates the first session and chooses its 2 or 3 topics. The facilitator of the highest-numbered session that already has a facilitator creates the following session and chooses its topics. A facilitator of an earlier session does not keep that right. Other members can hold a session that already has topics while the creator and the previous facilitator are away. A name-only seat is not a member. The group creator may also create a later session. Only one session may be waiting for its election. Once that session exists, the creator and the current facilitator are both refused until it elects a facilitator. Two creates submitted together store one session.

The app stays one ASP.NET Core process and one PostgreSQL database. Invitation, acceptance, attendance, voting, and session creation stay in the existing Groups and Sessions slices. Pages stay server-rendered. The constitution is amended to 1.3.0 so a member must have an account and each person acts only for themselves.

## Technical Context

**Language/Version**: C# on .NET 10 (ASP.NET Core)

**Primary Dependencies**: ASP.NET Core Razor Pages, Entity Framework Core, Npgsql, and the existing cookie sign-in. No mail library, no identity-provider SDK, no SPA toolchain.

**Storage**: PostgreSQL 17. One database. One new `Invitations` table. `Memberships.AccountId` stays nullable so existing name-only rows can remain. Every membership created by this feature sets `AccountId`. A row with `AccountId` null cannot attend, vote, or be elected. Do not add a `NOT NULL` migration on `AccountId`. Handlers use `QuorumDbContext` directly. No repository layer.

**Testing**: xUnit and `WebApplicationFactory`. Domain rules are pure tests. Invitation, attendance, and group isolation are integration tests against PostgreSQL.

**Target Platform**: Linux server. One app process and one database on one small virtual machine. Local development uses the same pair.

**Project Type**: Server-rendered web application inside a modular monolith.

**Performance Goals**: Accepting an invitation and opening the group stays inside SC-001, under 2 minutes, with no message sent. The home and a session page for 8 members stay the existing server-rendered pages. This feature adds no separate timing harness.

**Constraints**: No outbound email or other notification. No second database. Logs must not contain email addresses, display names, answer text, or votes. A group never exceeds 8, counting active account memberships and pending invitations. Name-only rows already stored stay so past sessions can still show their names, and they cannot attend or vote.

**Scale/Scope**: Tens of accounts. A group has at most 8 people and a handful of pending invitations. Two groups remain the isolation case.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Result | Evidence |
| --- | --- | --- |
| I. Quorum of two, no standing facilitator | Pass | Two accepted members can hold a session that already has topics while the creator is signed out. The group still has no facilitator column. Election stays per session. The room stays 2 to 8. The person who chose the topics does not have to attend. Constitution 1.3.0 records that the topic list is chosen by the session's creator ahead of the meeting. |
| II. Everyone practices | Pass | This feature does not add a live block or a ranking. Pick counts stay per topic for a group. |
| III. Content is data, read from the database | Pass | No new catalog source. Session topics still reference published topic ids. |
| IV. Group-scoped from the first table | Pass | Invitations, memberships, sessions, and votes stay keyed by group. A pending invitation's email is visible only to the invited account, the member who created it, and the group creator. |
| IV. No billing, SSO, marketplace, or mobile | Pass | Not in this plan. |
| V. A person present uses the web UI, no adapter required | Pass | Invite, accept, attendance, vote, and planning are Razor pages. No mail adapter. Constitution 1.3.0 requires each member to mark their own attendance and cast their own vote. |
| V. No media hosting | Pass | No media. |
| Privacy | Pass | Email and display name are not logged. Another group sees no invitation, member, session, or vote. A pending invitee sees the group name and the inviter's display name, not the group's sessions. |
| Out of scope items | Pass | No katas, points, video, billing, SSO, or the 45-minute runner. |
| One process, one database, no repository layer | Pass | New table in the existing database. Handlers use `QuorumDbContext`. |
| Delivery bar for this spec | Pass | The person served is a member who is not the group creator. The outcome is that two such members can attend, vote, elect a facilitator, and that facilitator can plan the next session while the creator is away. |

Post-design re-check: the same gates pass. The invitation is a row, not a service. Attendance is one membership marking itself, not a posted list of other people.

## Project Structure

### Documentation (this feature)

```text
specs/003-invited-members/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── pages.md
└── tasks.md             # created later by /speckit-tasks
```

### Source Code (repository root)

```text
src/Quorum.App/
├── Features/
│   ├── Groups/
│   │   ├── Invitation.cs
│   │   ├── InvitationCommands.cs
│   │   ├── GroupList.cshtml          # home: pending invitations and groups
│   │   ├── Members.cshtml            # invite by email, leave, creator removes; existing creator rename of an active account membership stays
│   │   └── RosterCommands.cs         # name-only add stops; creator rename of an active account membership stays
│   └── Sessions/
│       ├── Election.cs               # own attendance, own vote, drop a leaver
│       ├── SessionCommands.cs        # group creator, or facilitator of the highest-numbered session that already has a facilitator; topics required; one waiting session per group
│       └── SessionDetail.cshtml
└── Infrastructure/Persistence/       # EF migration for Invitations

tests/Quorum.App.Tests/
```

**Structure Decision**: Stay inside the existing Groups and Sessions slices. Invitations belong to Groups because they create memberships. Session rules stay in Sessions and call membership only to learn who the signed-in account is.

## Complexity Tracking

None. Constitution 1.3.0 removes the conflict. No second service or database.
