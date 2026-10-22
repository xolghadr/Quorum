# Quickstart: Group Catalog

Validation guide for this feature. Implementation steps belong in `tasks.md`. Contracts are in [contracts/pages.md](./contracts/pages.md) and [contracts/publish-catalog.md](./contracts/publish-catalog.md). Fields and transitions are in [data-model.md](./data-model.md).

## Prerequisites

- .NET 10 SDK
- PostgreSQL 17 on localhost
- A local checkout of the senior-backend roadmap (for the manual publish check only)
- Connection string for the app database, supplied by configuration, not committed

## Run the automated checks

From the repository root, after the test projects exist:

```text
dotnet test
```

Expected groups of checks:

| Project | What a passing run proves |
| --- | --- |
| `tests/Quorum.Catalog.Tests` | Fixture markdown becomes tracks, topics, sections, and deduped questions. A design is not a topic. A failed publish leaves no publication row. A second publish writes nothing. The publication row has no stored track, topic, or design total. |
| `tests/Quorum.Sessions.Tests` | Creating a session before a publication writes nothing. Session 1 counts its topics once and starts with no attendance. Replacing topics before election adds a pick only for a new topic. Attendance outside 2 to 8 writes nothing before the first vote. After the first vote, attendance may drop below 2 and cannot add a non-member or exceed the group. The group never has more than 8 members. The first vote freezes the voters and candidates. A later arrival is present and cannot vote. A voter removed from attendance keeps the vote. A later session adds a pick for each topic after election, including a repeat. A later session elects someone when the previous facilitator is absent. Production would choose a tied leader with equal chance. The test replaces that choice and keeps that person. A second submission for the same voter leaves the first choice. A self-vote is accepted for two people and refused for three or more. A session id from another group is not found. Illegal sets write nothing. A track is reviewed only when every topic has been picked. |
| `tests/Quorum.Groups.Tests` | One account can create two groups with two display names. A roster name stays in one group. Display-name uniqueness is per group. A password outside 10 to 128 is refused. The stored password is a salted hash, not plaintext, and two hashes of the same password differ. A ninth member is refused. A query for a group the account does not belong to does not return that group's members, sessions, or counts. |
| `tests/Quorum.App.Tests` | One browser path: register, create a group with a display name, see the not-available state, publish a fixture, open a topic and the catalog's last modification date, create the first session with three topics, sign out, fail a wrong password, sign in, and see the same session. |

`dotnet test` must pass before the feature is called done. The manual walk below is in addition to that, not a substitute.

## Manual walk

1. Start PostgreSQL and the app.
2. Register with a new email and a password of 10 to 128 characters. Confirm a shorter or longer password is refused. Create a group. Confirm the catalog says the answer key is not available yet. Confirm a session cannot be created yet.
3. Run:

```text
dotnet run --project src/Quorum.App -- publish-catalog --source <roadmap-directory>
```

Confirm exit code 0. Run it again and confirm exit code 2 and an unchanged catalog.

4. Refresh. Confirm seven tracks, the catalog's last modification date, and six designs. Open `Transactions and isolation` under Data and APIs. Confirm the answer-key sections that exist on that page, and that each review question appears once.
5. Create the first session with three topics and no attendance. Confirm never-picked topics are offered first. Confirm each chosen topic shows a pick count of 1 and is marked reviewed, and that no facilitator is elected yet. Open each answer key from the session.
6. Replace that session's topics with two others before anyone votes. Confirm only those two are on the session and no count went down.
7. Mark two group members present. Add the votes so that one has strictly more than the other. Confirm that member is the facilitator and the absent members are not voters. Create a second session on the same date with no topics. After its attendance and election, save a list that includes one topic already counted. Confirm that topic's count increased by 1.
8. Pick every topic in Language and runtime (three topics) across sessions. Confirm that track is marked reviewed. Confirm Architecture is not reviewed.
9. Add two roster names, including a case-insensitive duplicate, which must be refused. Remove one name that is not on a session. Confirm the sessions did not change. Confirm the group cannot grow past 8 members, counting the owner.
10. Sign out. Confirm a direct request for a topic URL shows sign-in and no answer key. Sign in with a wrong password and confirm the same. Sign in correctly and confirm the same group, sessions, and counts.
11. With the same account, create a second group and a different display name. Confirm the second group has no members from the first, no sessions, and a pick count of 0 on a topic the first group already picked, while the topic text is the same. Register a second account. Confirm that account sees none of the first account's groups.

## Not part of this walk

The 45-minute practice blocks, reminders, Telegram, billing, password reset, member sign-in, inviting an account into a group it did not create, catalog editing, and code katas.
