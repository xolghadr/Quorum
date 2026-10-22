# Contract: publish-catalog

Operator command. The facilitator has no page for it. The app does not read the roadmap while serving pages.

## Command

```text
dotnet run --project src/Quorum.App -- publish-catalog --source <directory>
```

`<directory>` is a local checkout of the senior-backend roadmap. It must contain `tracks/*.md` and `designs/*.md`.

## Success

Exit code 0. One transaction commits:

- 7 tracks in file order
- 72 topics, including good-to-have topics that carry `**Level:**`
- the review and interview questions parsed from those topics
- 6 worked designs
- one `CatalogPublication` row with source key `senior-backend-roadmap` and today's UTC date

After this, a signed-in account with a group sees the catalog. The command does not create accounts, groups, memberships, or sessions. The publication row stores the source key and the date. It does not store track, topic, or design totals. Those totals are the number of rows written above.

## Already published

Exit code 2. No insert, update, or delete. The existing catalog, pick counts, and sessions stay as they are. The message says a publication already exists.

## Failure

Exit code 1. The transaction rolls back. No catalog row is visible. A signed-in account still sees the not-available state. Typical causes: the directory is missing, a track file cannot be read, or a topic has `**Level:**` but no title.

## Parser outcomes the command must honor

- A heading is a topic only when its own block contains `**Level:**`. `## Good to have` is not a topic.
- Missing standard sections are omitted.
- Empty question text is omitted.
- The same question text and kind is stored once per topic.
- Design files are not topics.
- The command does not follow links to other repositories.

## Logging

The command may log the source directory, counts, and the publication id. It must not log section bodies, question text, or design bodies.
