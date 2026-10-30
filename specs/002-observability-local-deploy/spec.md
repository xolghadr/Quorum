# Feature Specification: Transparent Local Operations

**Feature Branch**: `002-observability-local-deploy`

**Created**: 2026-10-30

**Status**: Draft

**Input**: User description: "I want to add a world class transparent observability to this project, make requirements deployable locally on docker, and have a guide to run and deploy it."

## Clarifications

### Session 2026-10-30

- Q: Who can open Grafana, Loki, and the other tools while a group is using Quorum? → A: Only someone with those tools' built-in credentials. The tools are on their own domain and port. A Quorum member sign-in does not open them.
- Q: Does the guide's start bring up Grafana, Loki, and the other tools, or does Quorum only send to copies that are already running? → A: The deployment is self-contained. A debug session connects to instances that are already running. Production uses the Docker network service name. Development uses localhost ports of the local containers.
- Q: Which tools, besides Grafana and Loki, must be included in both the self-contained deployment and the local containers a debug session uses? → A: Prometheus and Tempo, plus an OpenTelemetry collector. The collector is included because Grafana, Loki, Prometheus, and Tempo do not receive and forward telemetry. Quorum sends only to the collector. The collector forwards logs to Loki, metrics to Prometheus, and traces to Tempo. Grafana is where the operator looks.
- Q: When the OpenTelemetry collector is down, where does the operator read the record Quorum kept? → A: In Quorum's existing database, read on the machine by the command the guide names. Not a page on the group's address, and not a second database. Process text alone does not satisfy the 14-day restart rule.
- Q: Do Loki and Tempo get passwords? → A: No. Loki and Tempo have no password. Grafana, Prometheus, and the collector keep their own credentials.

## User Scenarios & Testing *(mandatory)*

Person served: the operator who did not write Quorum, and who may also sit in the session as a facilitator. Session outcome: before the group begins, that person can see that the product is ready, and during the session they can point to a failed step and retry it without the author and without reading answer text, rubric notes, or scores.

Transparent, for this product, means that person can answer four questions from the running product and the included guide alone:

1. Is this copy ready for a session?
2. Which attempt failed or was refused?
3. How long did that attempt take?
4. Which recorded parts belong to the same attempt?

The tool set is Grafana, Loki, Prometheus, Tempo, and the OpenTelemetry collector. Each one is on its own domain and port, which may be 127.0.0.1 or another domain, and is separate from the address the group uses for Quorum. The local Compose file publishes those tools on 127.0.0.1. A certificate vendor is out of scope. Grafana and Prometheus each have their own built-in sign-in. Loki and Tempo have no password. The collector has its own credentials, which Quorum uses when sending. A Quorum member sign-in does not open any of them and is not accepted by the collector. Quorum sends logs, metrics, and traces only to the collector. The collector forwards logs to Loki, metrics to Prometheus, and traces to Tempo. Grafana reads those three and is where the operator looks. A self-contained deployment starts the whole tool set with Quorum. Inside it, Quorum reaches the collector by Docker network service name, and the collector reaches Loki, Prometheus, and Tempo by service name. A development debug session does not start the tool set. It sends only to the collector's localhost port on containers that are already running.

A group is still 2 to 8 people. This feature does not change pairing, question draw, the session clock, the rubric, or who may see a score.

### User Story 1 - See that Quorum is ready (Priority: P1)

The operator follows the included guide and starts the self-contained deployment once. The application, the place it keeps groups and sessions, and the tool set all come up together. Inside that deployment, Quorum sends only to the OpenTelemetry collector by Docker network service name. A status page says whether this copy is ready. The operator does not sign in to read that status, and the status does not show groups, members, or practice content.

**Why this priority**: A live session cannot honestly start if the person in the room cannot tell the product is up. That person is not the author.

**Independent Test**: On a machine that has only the guide and the local start tool the guide names, start Quorum, open the status without an account, and see ready and local use only. Stop stored data and see not ready within 30 seconds, naming stored data. Start stored data again and see ready. Start a second copy while the first is running and see a clear refusal, with the first copy's data still present.

**Acceptance Scenarios**:

1. **Given** the product is not started, **When** the operator follows the guide's self-contained start, **Then** the application, its stored data, Grafana, Loki, Prometheus, Tempo, and the OpenTelemetry collector are running together, Quorum sends only to the collector by Docker network service name, and the status says ready and local use only.
2. **Given** the product has just started and nobody has signed in, **When** the operator opens the status, **Then** they see ready or not ready without an account, and the status shows no group name, member name, answer text, rubric note, score, password, or session secret.
3. **Given** the status says ready on a copy that still uses local-only settings, **When** stored data is stopped, **Then** within 30 seconds the status says not ready, names stored data as the part that is down, still says local use only, and does not say ready or ready for a group.
4. **Given** the status says not ready because stored data is down on a copy that still uses local-only settings, **When** stored data is started again, **Then** the status says ready again and still says local use only. One activity record exists for the change to not ready, and one exists for the change back to ready.
5. **Given** a self-contained deployment is already running, **When** the operator starts a second self-contained deployment, **Then** the second start is refused with a message the guide explains, and existing groups, sessions, and activity records stay as they were. A debug session attached to tool containers that are already running is not that second start.
6. **Given** the product is running and has stored a group, **When** the operator stops it the way the guide describes, **Then** the product stops, and that group is still there on the next start.
7. **Given** the status stays ready, **When** another ready check runs, **Then** no new activity record is added for that check.
8. **Given** the operator starts the product the way the guide describes, **When** the start finishes, **Then** one activity record says the application started.
9. **Given** the operator stops the product the way the guide describes, **When** the stop finishes, **Then** one activity record says the application stopped, and the records from before that stop remain.

---

### User Story 2 - Explain one attempt without private practice content (Priority: P1)

Each attempt that changes stored state leaves one activity record. Quorum sends it only to the OpenTelemetry collector. The collector delivers the record to Loki, the duration and the success or refusal count to Prometheus, and the single reference, including stored-data wait, to Tempo. The operator finds it in Grafana by that reference, by failed outcome, by group, by session, and by time, after signing in with Grafana's built-in credentials. A refused attempt shows that same reference to the person who tried it, so they can read it aloud. The record has the outcome and the duration. It never holds answer text, rubric notes, scores, passwords, or the body of a catalog page. A Quorum member sign-in does not open Grafana, Loki, Prometheus, Tempo, or the collector.

**Why this priority**: When a step fails in the room, the facilitator has to see what failed and retry it. The record must not become a second copy of the group's practice.

**Independent Test**: Register, refuse one sign-in with a wrong password, refuse one sign-in for an unknown email, create a group, add, rename, and remove a member, publish the catalog, create a session, set topics, save attendance, and store a facilitator by votes. Each attempt appears once with outcome and duration. The wrong-password page shows the same reference as its record. A search of every record finds none of the password, the unknown email, a known answer sentence, a rubric note, or a score. The Quorum pages do not open Grafana, Loki, Prometheus, Tempo, or the collector, and a Quorum sign-in does not accept there. A wrong Grafana credential shows no records.

**Acceptance Scenarios**:

1. **Given** a person registers with a valid email and password, **When** the account is created, **Then** one record says register succeeded, with a time, a duration, a short reference, and the account identifier, and the password does not appear in the record.
2. **Given** an account exists, **When** someone signs in with the wrong password, **Then** one record says sign-in refused, the password is absent, the account identifier is present, and the page shows the same reference as the record.
3. **Given** no account matches the typed email, **When** someone tries to sign in, **Then** the record says the account is unknown and contains neither the typed email nor the password. The page shows the same reference and does not say whether a different email would have worked.
4. **Given** a signed-in account, **When** they sign out, **Then** one record says sign-out succeeded, and the product pages no longer show that account's groups.
5. **Given** a signed-in account, **When** they create a group and then add, rename, and remove one member, **Then** each attempt has its own succeeded record with that group's identifier. None of those records contain a display name, an email, or a password.
6. **Given** a catalog publication succeeds, **When** the operator opens that record, **Then** it shows how many tracks, topics, and designs were published, the outcome, and the duration, and it does not contain the text of a catalog page.
7. **Given** a published catalog and a group, **When** a member creates a session, sets its topics, saves attendance, and votes are stored until a facilitator is chosen, **Then** each of those attempts has one record. The submission that causes the facilitator to be stored also writes an election record, and that vote record and election record share one short reference. No record contains a score, an answer, or a rubric note.
8. **Given** a vote the product refuses, such as a person in a session of three or more voting for themselves, **When** it is refused, **Then** one record says refused, and there is no succeeded record for that same attempt.
9. **Given** a reference shown on a refused page, **When** the operator looks up that reference, **Then** they see that attempt's name, outcome, time, and duration, and nothing from another attempt.
10. **Given** records for more than one group and both succeeded and refused outcomes, **When** the operator filters by one group, by refused outcome, and by a time range, **Then** only records that match all of those filters are shown.
11. **Given** three succeeded attempts and one refused attempt in the last hour, **When** the operator looks at that hour, **Then** the view shows 3 succeeded and 1 refused.
12. **Given** eight members each complete one distinct state-changing attempt in the same minute, **When** the operator lists that minute, **Then** eight records are present and each attempt can be told apart by its reference.
13. **Given** a signed-in member is using the Quorum pages, **When** they look for Grafana, Loki, Prometheus, Tempo, or the OpenTelemetry collector, **Then** those pages do not offer them. Opening Grafana or Prometheus without that tool's own password shows no activity records. Loki and Tempo have no password. A Quorum email and password does not sign in to Grafana or Prometheus and does not authorize a send to the collector.
14. **Given** an activity record is 15 days old and the group it refers to still exists, **When** the operator opens the inspection view, **Then** that record is no longer shown, and the group and its sessions are still in the product.
15. **Given** a record includes a duration, **When** the attempt waited on stored data, **Then** the record also says how much of that duration was waiting on stored data. When it did not wait, that portion is zero.
16. **Given** an ordinary attempt that already finishes in under 1 second for the person waiting, **When** the same kind of attempt runs with recording on in a session of eight people, **Then** the person still receives the outcome in under 1 second, and the activity record exists.
17. **Given** the product's existing register, sign-in, group, catalog publication, and facilitator election behavior, **When** those attempts run with this feature on, **Then** they succeed or are refused as they already are. No rank, streak, or score board is offered, and no phone, email, or chat alert is sent.

---

### User Story 3 - Keep the session independent of the inspection view (Priority: P2)

The inspection view is the operator's window onto readiness and activity. A session does not need it. If the view or the collector is stopped, people can still sign in and change a group. Quorum keeps each activity record in the same stored data it already uses for groups and sessions. The guide names one command, run on the machine, that prints the matching record from that stored data. That command is not a page on the address the group uses, and this feature does not add a second database.

**Why this priority**: The room still has to finish if the operator's window is closed. The view exists so a non-author can see readiness, duration, and one reference per attempt. The application and its stored data remain what a group depends on.

**Independent Test**: Stop the collector. Sign in and create a group. Both succeed. On the machine, run the guide's command and see both records, including their references. Confirm the group's address has no page of those records. Start the collector again and see both records in Grafana. Restart Quorum and confirm the command still prints both records.

**Acceptance Scenarios**:

1. **Given** the inspection view is stopped and the application is running, **When** a person signs in and creates a group, **Then** both attempts complete as they do when the view is running, and the product does not tell them that the session is blocked because the view is down.
2. **Given** those two attempts happened while the collector was stopped, **When** the collector accepts sends again, **Then** both records are visible in Grafana with outcome and duration.
3. **Given** the collector is stopped, **When** the operator runs the command the guide names on the machine, **Then** that command prints those same outcomes and references from Quorum's existing stored data before Grafana shows them again. The group's address does not grow a page of those records.
4. **Given** the view is stopped and stored data is still reachable, **When** the operator opens the status, **Then** the status still says ready or local use only from the application, and it does not become not ready only because the view is stopped.

---

### User Story 4 - Run locally and deploy for one group from the guide (Priority: P2)

The guide has a self-contained local path and a deploy path, both for a person who has not worked on Quorum. Each path starts the application, its stored data, and the tool set together. Quorum sends only to the OpenTelemetry collector by Docker network service name. The local path shows local use only and keeps groups after a restart. The deploy path puts that same self-contained deployment on one machine the group can open, requires the local-only settings to be replaced, and then shows ready for a group. Stopping never erases stored groups. Erasing is a separate documented step. The development debug path is a separate story.

**Why this priority**: The group cannot meet on the product until someone other than the author can run it locally and then put it on one machine. Local readiness is already valuable before that deploy.

**Independent Test**: A person who has not worked on Quorum follows only the guide. Locally they start, see local use only, create a group, restart, and see the group again. On one group-facing machine they replace the local-only settings, see ready for a group, restart, and see the group again. They stop without losing the group, then use the documented erase and see that group gone on the next start. Each numbered step matches the result printed in the guide.

**Acceptance Scenarios**:

1. **Given** a local copy has a group, a session, and an activity record from today, **When** the operator stops and starts again the way the guide describes, **Then** that group, session, and record are still there, and the status says local use only.
2. **Given** a fresh local start that still uses the guide's local-only settings, **When** the operator reads the status, **Then** it says local use only and does not say ready for a group.
3. **Given** the operator follows the deploy section on one machine and replaces every local-only setting the guide names, **When** stored data is reachable, **Then** the status says ready for a group and does not say local use only.
4. **Given** a copy that the operator intended for a group still uses a local-only setting, **When** they open the status, **Then** it says local use only and does not say ready for a group.
5. **Given** a group-facing copy whose status says ready for a group and that already has a group stored, **When** the application is restarted, **Then** the group is still there and the status returns to ready for a group.
6. **Given** a running copy with a stored group, **When** the operator stops it the way the guide describes, **Then** the group remains. **When** they then follow the documented erase and start again, **Then** that group and its sessions are gone, and a new ready status is possible.
7. **Given** only the included guide, **When** a person who has not worked on Quorum runs the local path and the deploy path, **Then** every numbered step has the result the guide states, with no step that exists only in someone's memory.
8. **Given** the failure notes in the guide, **When** the local start tool is missing, stored data is not ready, the guide's address is already in use, the inspection view is unavailable, or local-only settings remain on a group-facing copy, **Then** the guide says what the operator will see and the next action to take.
9. **Given** the deploy section, **When** the operator follows it, **Then** the group can open the product on that one machine, and the deployment contains the application, its stored data, Grafana, Loki, Prometheus, Tempo, and the OpenTelemetry collector. Quorum sends only to the collector by Docker network service name. The path does not ask for a second machine, a paid account, a tool hosted outside the deployment, or more than one copy of the application.
10. **Given** the self-contained deployment is running, **When** a person registers, **Then** Quorum sends that record only to the collector by the collector's service name. The collector delivers the activity record to Loki, the duration and outcome count to Prometheus, and the single reference to Tempo. The operator can see it in Grafana. Quorum does not use a localhost port for that hop and does not send straight to Grafana, Loki, Prometheus, or Tempo.

---

### User Story 5 - Debug against containers that are already running (Priority: P2)

A developer starts a debug session while Grafana, Loki, Prometheus, Tempo, and the OpenTelemetry collector are already running as local containers. Quorum sends only to the collector through the localhost port the guide names. The debug session does not start a second copy of the tool set, does not address the collector by Docker service name, and does not send straight to Grafana, Loki, Prometheus, or Tempo. The group can still sign in and change a group when those containers are down.

**Why this priority**: The deployed copy has to stand alone. The person debugging has to attach to the tool containers already on the machine.

**Independent Test**: Start the tool set so the collector's port is open on localhost. Start a debug session, register, and see that one record in Grafana. Confirm the session did not start the tool set and did not send straight to Loki, Prometheus, or Tempo. Stop those containers, register again, and confirm the attempt still finishes and the guide's fallback has the record. Then start the self-contained deployment and confirm that copy sends only to the collector by service name.

**Acceptance Scenarios**:

1. **Given** Grafana, Loki, Prometheus, Tempo, and the OpenTelemetry collector are already running as local containers, **When** a debug session starts and a person registers, **Then** Quorum sends that record only to the collector's localhost port, and the operator can see it in Grafana.
2. **Given** a debug session is running, **When** the developer sees which tools started with that session, **Then** the session did not start the tool set. It did not address the collector by Docker service name, and it did not send straight to Grafana, Loki, Prometheus, or Tempo.
3. **Given** those local containers are not running, **When** a debug session starts and a person registers, **Then** the attempt still succeeds or is refused as it otherwise would, the guide's fallback has the record, and the session does not start the tools or switch to a Docker service name.
4. **Given** a debug session is using localhost ports and the self-contained deployment is not the process under debug, **When** the operator opens Grafana, **Then** they use that tool's own port and built-in credentials.

---

### Edge Cases

- A catalog publication that fails produces one refused record, does not include page text, and does not report a success. Counts are omitted when publication did not finish.
- An attempt that the person has not yet been shown an outcome for has no succeeded record. A later success or refusal writes one record for that finished attempt.
- The short reference shown to a person contains no answer text, rubric note, score, password, or email.
- Activity records stay in the order the product accepted the attempts.
- Application start writes one start record. Application stop writes one stop record. A forced stop that cannot write is described in the guide, and the earlier records remain.
- Routine ready checks while the status is unchanged write no activity record. Each change between ready and not ready writes one record.
- Eight simultaneous attempts are all kept. None are dropped to save space during a single session.
- Records older than 14 days leave Grafana and are no longer printed by the guide's command. Groups, members, sessions, votes, and the catalog do not expire because of that rule. The activity records live in the same stored data as those groups, and this feature does not add a database.
- The status names stored data when that is the part that is down. It does not name people, groups, or catalog pages.
- The operator can relate an account, group, session, or member identifier to a person by opening the product they already run. The activity record does not copy the display name or email onto itself.
- Stopping the inspection view does not refuse sign-in, group changes, or session actions the product already allows.
- A Quorum email and password does not open Grafana or Prometheus and does not authorize a send to the collector. A missing or wrong password for Grafana or Prometheus shows no activity records. Loki and Tempo have no password. A missing or wrong collector credential means Quorum's send is refused, and the attempt still finishes in Quorum. The address the group uses for Quorum does not serve those tools.
- Quorum does not send straight to Grafana, Loki, Prometheus, or Tempo. If the collector is down, those four do not receive a new record until the collector is back. The activity record is still in Quorum's existing stored data, and the guide's command on the machine prints it. Process text is not the copy that has to survive a restart.
- A debug session whose localhost ports are closed does not switch to a Docker service name. A self-contained deployment does not send to a localhost port or to a tool outside that deployment.
- Starting a debug session while the tool containers are already running does not count as a second self-contained deployment and does not erase stored groups.
- Erase is the only documented step that destroys stored groups and sessions. A normal stop, a restart, and a failed second start do not.
- This feature does not add a phone, email, or chat alert. The operator sees not ready by opening the status.
- This feature does not add a member-facing board of scores, streaks, or ranks, and it does not show scores to the operator in the activity record.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The product MUST include a guide that a person who has not worked on Quorum can follow without the author. The guide MUST have a development debug path, a self-contained local path, and a single-machine deploy path, with numbered steps and the result of each numbered step. For each tool the guide MUST name the Docker network service name and the localhost port.
- **FR-002**: The self-contained local path and the deploy path MUST bring up the application, the stored data, Grafana, Loki, Prometheus, Tempo, and the OpenTelemetry collector together. The operator MUST NOT install those parts by hand and MUST NOT point either path at a tool outside that deployment. Inside that deployment, Quorum MUST send only to the collector by the collector's Docker network service name. The collector MUST reach Loki, Prometheus, and Tempo by their service names.
- **FR-003**: The guide MUST describe one stop. That stop MUST leave stored groups, sessions, the catalog, and activity records in place.
- **FR-004**: The guide MUST describe one erase that removes local stored groups, sessions, and activity records. Erase MUST be separate from stop. A restart, a failed second start, and a stop MUST NOT erase that data.
- **FR-005**: A second self-contained deployment while one is already running MUST be refused with a message the guide explains, and MUST leave existing data in place. A debug session that connects to tool containers already running MUST NOT be treated as that second deployment.
- **FR-006**: A status MUST be readable without signing in. It MUST say ready only when the application can use its stored data.
- **FR-007**: When stored data is unreachable, the status MUST say not ready within 30 seconds, MUST name stored data as the part that is down, and MUST NOT say ready or ready for a group.
- **FR-008**: The status MUST say whether this copy is for local use only or ready for a group. While any documented local-only setting is still in use, the status MUST say local use only and MUST NOT say ready for a group.
- **FR-009**: After the operator replaces every local-only setting the deploy section names, and stored data is reachable, the status MUST say ready for a group and MUST NOT say local use only.
- **FR-010**: The status MUST NOT show a group name, member name, display name, email, answer text, rubric note, score, password, session secret, or catalog page text.
- **FR-011**: The product MUST write one activity record for each finished attempt in this list: register, sign in, sign out, create a group, add a member, rename a member, remove a member, publish the catalog, create a session, set session topics, save attendance, and cast a vote. When a vote causes the facilitator to be stored, that same submission MUST also write one election record, and the vote record and the election record MUST share one short reference. The product MUST also write one record when the application starts, one record when the application stops cleanly, and one record each time the status changes between ready and not ready.
- **FR-012**: Each activity record MUST include the attempt name, the outcome succeeded or refused, the time accepted, the duration, how much of that duration was waiting on stored data, and one short reference. A record for a group, session, account, or member MUST include that identifier when one exists. It MUST NOT include a display name or an email.
- **FR-013**: A refused attempt MUST be recorded as refused and MUST NOT also be recorded as succeeded. An attempt the person has not been shown an outcome for MUST NOT have a succeeded record yet.
- **FR-014**: A sign-in refusal MUST NOT record the password. When no account matches, the record MUST say the account is unknown and MUST NOT store the typed email. When an account matches, the record MUST include that account identifier and MUST NOT include the password.
- **FR-015**: Activity records, the status, the short reference, and the guide's fallback MUST NOT contain answer text, rubric notes, scores, passwords, session secrets, or the body of a catalog page. A successful catalog publication record MAY include the counts of tracks, topics, and designs.
- **FR-016**: Every record written by one submission MUST share that submission's short reference. Looking up the reference MUST return those records and no record from another submission.
- **FR-017**: When a person is shown a refusal, the page MUST show the same short reference as the activity record. The page MUST NOT show another group's information or a dump of the product's internals. The reference itself MUST NOT contain answer text, a password, or an email.
- **FR-018**: In Grafana, the operator MUST be able to find records by reference, by refused outcome, by group identifier, by session identifier, and by a time range, and MUST be able to see the count of succeeded and refused attempts in the last hour.
- **FR-019**: The tool set MUST be Grafana, Loki, Prometheus, Tempo, and the OpenTelemetry collector. Each MUST be on its own domain and port. That address may be 127.0.0.1 or another domain, and it MUST NOT be the address the group uses for Quorum. The local Compose file publishes the tools on 127.0.0.1. A development debug session still uses localhost. A certificate vendor is out of scope. Grafana and Prometheus MUST each require their own built-in password. Loki and Tempo MUST NOT gain a password. The collector MUST require its own credentials on every send from Quorum. A Quorum member sign-in MUST NOT open any of them and MUST NOT authorize a send to the collector. Quorum pages MUST NOT offer them. A missing or wrong password for Grafana or Prometheus MUST show no activity records. A missing or wrong collector credential MUST refuse the send, and the attempt in Quorum MUST still finish.
- **FR-020**: When the inspection view or the collector is stopped, sign-in, group changes, and session actions the product already allows MUST still complete, the status MUST still reflect stored data, and new activity records MUST still be kept in the same stored data Quorum already uses for groups and sessions. The guide MUST name one command, run on the machine, that prints a matching record from that stored data. That command MUST NOT be a page on the address the group uses. This feature MUST NOT add a second database. A record kept while the collector was down MUST be sent once the collector accepts sends again, and MUST then be visible in Grafana.
- **FR-021**: Activity records MUST remain available for 14 days in Grafana and through the guide's command. Records older than 14 days MUST leave both. A restart of Quorum MUST NOT be what removes a record younger than 14 days. Groups, members, sessions, votes, and the catalog MUST NOT be deleted by this retention rule.
- **FR-022**: A repeated status check that does not change ready or not ready MUST NOT add an activity record.
- **FR-023**: Eight distinct state-changing attempts from eight members in the same minute MUST all be present and distinguishable. The product MUST NOT drop attempts from a session of eight to save space.
- **FR-024**: After a stop and a start, groups, sessions, the catalog, and activity records younger than 14 days MUST still be there.
- **FR-025**: The guide MUST state what the operator sees, and the next action, when the local start tool is missing, stored data is not ready, the guide's address is already in use, the inspection view is unavailable, or a local-only setting remains on a group-facing copy.
- **FR-026**: The deploy path MUST run the group on one machine the group can open, as one self-contained deployment. It MUST NOT require a second machine, a paid external account, a cluster, or a Grafana, Loki, Prometheus, Tempo, or OpenTelemetry collector hosted outside that deployment. A session MUST remain possible when the inspection view is not running.
- **FR-027**: Recording MUST leave an ordinary attempt, one that already finished in under 1 second for the person waiting, still finished in under 1 second in a session of eight people.
- **FR-028**: This feature MUST NOT change pairing, question draw, the session clock, the rubric, group size, or who may see a score. It MUST NOT add phone, email, or chat alerts, and it MUST NOT add a member-facing rank, streak, or score board.
- **FR-029**: A development debug session MUST send only to an OpenTelemetry collector that is already running, using the collector's localhost port. That session MUST NOT start the tool set, MUST NOT address the collector by Docker service name, MUST NOT send straight to Grafana, Loki, Prometheus, or Tempo, and MUST NOT switch to a service name when that localhost port is closed.
- **FR-030**: The guide's development path MUST say how to start the local tool set before the debug session, the collector's localhost port, the service name and localhost port of each tool, and what the developer sees when the collector is not running.
- **FR-031**: Quorum MUST send logs, metrics, and traces only to the OpenTelemetry collector. The collector MUST forward logs to Loki, metrics to Prometheus, and traces to Tempo. Quorum MUST NOT send straight to Grafana, Loki, Prometheus, or Tempo. Grafana MUST show the activity record, the duration, the stored-data wait, the single reference, and the hourly success and refusal counts by reading Loki, Prometheus, and Tempo. The collector is required because Grafana, Loki, Prometheus, and Tempo do not receive and forward telemetry.

### Key Entities

- **Health status**: The operator's unsigned answer to whether this copy can serve a session. It says ready or not ready, names stored data when that part is down, and says either local use only or ready for a group. It carries no group, member, or practice content.
- **Activity record**: One finished attempt, or one change of health, or one clean application start or stop. It is kept in the same stored data Quorum already uses for groups and sessions. It holds the attempt name, outcome, time, duration, stored-data wait, a short reference, and identifiers for the account, group, session, or member when those exist. It does not hold display names, emails, passwords, answer text, rubric notes, scores, or catalog page text. The collector exports a copy. The guide's command prints the stored record on the machine when that export is unavailable.
- **Short reference**: The code that ties a person's refusal on screen to that one submission. It is unique to the submission and safe to read aloud. The vote record and the election record from that submission share it.
- **Local-only setting**: A documented setting that is safe only for the operator's own practice copy, including the default database password, the collector credential, the Grafana password, the Prometheus password, and the local-only address. Loki and Tempo have no password. While any local-only setting remains, the copy is not ready for a group.
- **Tool set**: Grafana, Loki, Prometheus, Tempo, and the OpenTelemetry collector. Quorum sends logs, metrics, and traces only to the collector. The collector forwards logs to Loki, metrics to Prometheus, and traces to Tempo. Grafana reads those three. Each tool is on its own domain and port, which may be 127.0.0.1 or another domain, and is separate from the group's address. The local Compose file publishes the tools on 127.0.0.1. A certificate vendor is out of scope. Quorum sign-in opens none of them. In a self-contained deployment, Quorum reaches the collector by Docker network service name. In a development debug session, Quorum reaches an already running collector by localhost port. The inspection view is Grafana, which is where the operator sees activity records and the count of succeeded and refused attempts.
- **Run guide**: The included instructions with a local path, a one-machine deploy path, the result of each numbered step, and the failure notes in FR-025.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person who has not worked on Quorum can follow the local path and see a ready status in under 15 minutes, not counting a download that itself takes longer than 5 minutes. After that download, the remaining steps still finish within 15 minutes.
- **SC-002**: That same person can follow the deploy path alone and see ready for a group on one machine in under 30 minutes, with the same download allowance.
- **SC-003**: After any finished attempt in FR-011, the operator can find that attempt's outcome and duration in under 1 minute, using the inspection view or the guide's fallback, without asking the author.
- **SC-004**: In the full set of activity records from one session of eight people, the count of answer text, rubric notes, scores, and passwords is zero.
- **SC-005**: After stored data is stopped, the status shows not ready and names stored data within 30 seconds, and it does not say ready.
- **SC-006**: With the inspection view stopped, two people can still sign in and one of them can create a group. Both records are available afterward.
- **SC-007**: On a clean machine, 100% of the numbered steps in the self-contained local path, the deploy path, and the development debug path match the result the guide states, with no undocumented step.
- **SC-008**: In a session of eight people, an ordinary attempt that finished in under 1 second before this feature still finishes in under 1 second with recording on.
- **SC-009**: Eight distinct attempts from eight members in the same minute are all present and can be told apart.
- **SC-010**: After a documented stop and start, the same group and session are still present, and an activity record from that day is still visible. After the documented erase and a new start, that group is gone.
- **SC-011**: A finished attempt in the self-contained deployment appears in the tools that started with that deployment. A finished attempt in a debug session appears in the tool containers that were already running. Neither attempt requires a tool hosted outside those two setups.

## Assumptions

- The person served is the operator who starts Quorum for a group. They did not write it. They may also be the facilitator elected inside a session. Members do not receive credentials for Grafana, Loki, Prometheus, Tempo, or the OpenTelemetry collector.
- The session outcome that changes is readiness before the group begins, and the ability to identify and retry a failed step during the session without the author. Pairing, question draw, the clock, and score storage stay as they are.
- The application and its stored data cannot, by themselves, give a non-author one place to see readiness, duration, stored-data wait, and a single reference per attempt. The inspection view is that place. A group does not depend on it. In front of a group, Quorum is still the application and its stored data on one machine. No paid external account is required. This is the constraint that justifies the extra operator window.
- The user asked for the local requirements to start with Docker. The guide names that container tool. A self-contained deployment starts the application, its stored data, and the tool set together. Quorum sends only to the OpenTelemetry collector by Docker network service name, and the collector forwards to Loki, Prometheus, and Tempo. A development debug session sends only to an already running collector through that collector's localhost port, and does not start a second copy of the tool set. This specification judges one self-contained start, a ready status, data that survives a restart, and a debug session that can attach to the collector already on the machine.
- Local-only settings are the sample database password, the collector credential, the Grafana password, the Prometheus password, and the local-only address, as printed in the guide. Loki and Tempo have no password. They are the fresh local start. A group-facing copy replaces every local-only setting before the status may say ready for a group.
- The group reaches a deployed copy at the address the operator chooses on that one machine. Each tool is on its own domain and port, which may be 127.0.0.1 or another domain, and is not the group's Quorum address. The local Compose file publishes the tools on 127.0.0.1. A certificate vendor is out of scope.
- Activity retention is 14 days. Product data the group created is kept until the operator uses the documented erase. The activity record is stored with that product data, in the database Quorum already has. A second database was considered and declined: Loki already keeps the exported copy, and process text does not satisfy a restart. The guide's command reads the existing stored record on the machine. Hosts that already keep their own copies of process text may keep those copies under their own rule. Grafana and the guide's command follow the 14-day rule.
- Successful reads of the catalog are not activity records. A failed publication is. State-changing attempts in FR-011 are.
- Unknown sign-in emails are not stored, so the record does not become a list of attempted addresses. The operator still sees that an unknown-account refusal happened, and how long it took.
- Alerts to a phone, mailbox, or chat are out of scope. The operator looks at the status.
- Member-facing analytics, leaderboards, streaks, and score boards are out of scope, as are changes to the live session format.
- The first catalog and the existing account, group, and session behavior are already in the product. This feature records them. It does not add a new session job.
