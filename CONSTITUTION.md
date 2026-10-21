# Study Group Platform — Constitution

> Self-contained record of goals, principles, locked decisions, architecture, and phased plan.  
> Use this document to start spec-driven development in a new session. Nothing from the original brainstorming handoff or subsequent decisions is omitted.

## 1. Purpose

A small group (4–6 people) studies the same senior backend (.NET) roadmap.  
Goals: refresh knowledge, review technical topics, and prepare for interviews.

Formats that required one person to prepare in advance repeatedly failed (absence or unfinished homework).  
**Hard rule:** no session may depend on one person’s preparation. A session must work with whoever shows up (quorum of 2).

## 2. Design principles

- Retrieval practice over presentation. Everyone participates throughout; nobody is pure audience.
- Interviewer needs no advance preparation; the content page is the answer key.
- Effort is rewarded more than test scores (attendance, daily log lines, exercises, interviewer turns).
- Content is the source of truth; the platform orchestrates it.
- Keep the shared kernel of the modular monolith small so later phases can be added by the group without rewriting Phase 1.

## 3. Content sources

### Primary (authoritative)
`https://github.com/xolghadr/software-engineer-roadmap`  
Senior backend, language-neutral with .NET 10 examples. Tracks 1–7, six worked designs, interview index, appendix.  
Topic pages have a fixed, parseable structure (Level, You can, In one minute, When it matters / What it costs, A failure you should recognize, Learn, Review (~3 questions), In an interview, Exercise).

### Structured content repository (platform’s content database)
A dedicated Git repository, normalized for the app, is the content source the platform reads.  
It is derived from the primary roadmap (and later other sources).  
A sync step loads it into the database. Member-added questions (later) land in the same tables with a different source flag.

### Future sources (pluggable adapters, not MVP)
- awesome-katas style coding katas
- `xolghadr/dsa-guide`
- `xolghadr/ef-core-review`
- Secondary Persian roadmap (used only for its “قانون عبور” scoring rubric)

### Scoring rubric (قانون عبور)
A topic counts as known when the person can:
1. explain it in their own words,
2. say what problem it solves,
3. write a C# example,
4. give a backend example,
5. answer at least one interview question about it,
6. explain how it differs from nearby concepts.

## 4. Approved weekly unit and 45-minute session

### Weekly unit (async)
- Shared part (~70%): 2–3 related must-have topics.
- Personal part (~30%): each person picks one topic that has a page (so an answer key exists).
- People interviewing soon answer review questions under a time limit; refreshers do the exercise + a short note.

### Live session (45 min) — online with coordinated pairs (no breakouts)
| Time | Block | Activity |
|---|---|---|
| 0–2 | Setup | Random pairs (trio ring if odd count) |
| 2–16 | Round 1 | Shared unit. Interviewer asks 3 questions; candidate answers; then swap. Everyone in parallel pairs. |
| 16–30 | Round 2 | Personal topic. New pairs. Each person is interviewed on their own pick. |
| 30–43 | Round 3 | Group tradeoff. One scenario from the unit. Round-robin: each person adds one decision + one rejected option. |
| 43–45 | Close | Self-score, pick next week’s unit. |

### Rules
- Odd numbers: trio ring (A asks B, B asks C, C asks A). No observers.
- Absence: quorum of 2. With only two people, Round 3 becomes a one-on-one debate.
- Scoring: interviewer uses the قانون عبور rubric. Also serves as feedback.
- Points (later phase): awarded for effort (attendance, log lines, exercises, interviewer turns).
- Question draw: no repeat inside the same pair. Use Review questions + In an interview + failure modes + tradeoff material. Member-added questions allowed later.

Communication stays outside the platform (shared call + mute, side channels, Telegram voice, etc.). The runner only produces pairings, questions, and timers.

## 5. Architecture and technology decisions

- **Form:** ASP.NET Core modular monolith, Jimmy Bogard vertical-slice style, plus a Telegram bot.
- **Layout:** feature folders (`/Features/Content`, `/Features/Sessions`, `/Features/Telegram`, `/Features/Members`, …). Shared kernel kept deliberately small.
- **Content flow:** structured Git repo → explicit sync → database tables. Runtime reads only the DB.
- **Operational data in DB:** topics, questions, weekly units, sessions, pairs, scores, question-usage history, members (Telegram id + name).
- **Session runner:** state machine that creates a session, pairs people, draws questions, advances blocks (timer or facilitator command), records scores. Exposes state to web UI and Telegram.
- **Telegram bot:** notifications, check-ins, current pair/question push, lightweight score collection. No video integration.
- **Hosting:** self-hosted on a VPS. No extra budget at this stage.
- **Who builds:** Phase 1 (MVP) built by the user + Claude; later phases handed to the group as practice projects.

## 6. Implementation phases

### Phase 1 — MVP (Content catalog + Session runner)
- Structured content repo schema and sync.
- Catalog: topics, questions (all types), weekly units.
- Session runner: pairing, question draw (no repeats in pair), block timers/state machine, rubric scoring, question-usage tracking.
- Minimal members.
- Telegram bot for session coordination.
- Web UI sufficient to run and observe a session.

### Phase 2 — Group and meetup
- Members, weekly schedule, attendance, reminders, next-unit and personal-topic picks.
- Video is a link (Meet/Jitsi), not built-in.

### Phase 3 — Progress and game
- Async logs, points, streaks, team goals.
- Later: seasons and boss fights (timed run of one of the six worked designs, judged by the other team).

Content must enter through pluggable source adapters so future repos can be added without changing the core model.

## 7. Explicitly out of scope for Phase 1
- Coding katas / dojo mode
- Automatic breakout rooms
- Full progress/game mechanics
- Group scheduling beyond the minimum needed to start a session

## 8. How to use this constitution
Start a new spec-driven development session with this file as the sole context.  
First deliverable of Phase 1: the structured content-repo schema (YAML contract for topics, questions, and units).  
Then write the Phase 1 spec (catalog + runner + bot), review it, and proceed to the implementation plan.

All decisions above are locked unless explicitly reopened.  
Primary content source remains `https://github.com/xolghadr/software-engineer-roadmap`.
