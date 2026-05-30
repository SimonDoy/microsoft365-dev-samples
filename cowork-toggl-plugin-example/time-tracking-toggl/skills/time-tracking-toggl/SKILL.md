---
name: time-tracking-toggl
description: |
  Helps iThink 365 users view what tasks and time they have booked in Toggl Track, and
  create new time entries safely. Use when user asks "what have I logged
  today", "show my time for this week", "how much time did I spend on
  [project/task]", "create a 90-minute entry for [task]", "log time from
  9 to 11 for [project]", or "add my missing time entry". The Toggl API uses
  UTC, so local date/time inputs must be converted to UTC before API calls.
  Do NOT use for: project planning boards (use project-management), invoice
  creation (use invoicing), or importing a full calendar automatically
  (use calendar-integration).
metadata:
  author: Simon Doy
  version: "1.4"
---

## Overview

This skill provides a reliable timesheet assistant for Toggl Track, it requires that the user is logged into their Toggl account and provide their Toggl username and password, please default the username to use their email address. You can check the status of their Toggl connection with the 'check_login_status' command.
The skill can::

- Read booked time entries for a person across a day/week/month
- Group entries by project, task, client, and billable status
- Create new entries from natural language requests
- Validate overlaps, missing project/task context, and invalid time ranges
- Always convert local user times to UTC before hitting Toggl APIs

## When to Use

- "Show me what I tracked today"
- "What tasks did I log this week?"
- "How much time have I booked on Project X this month?"
- "Create a time entry for 2 hours on presales"
- "Log from 13:30 to 15:00 for customer workshop"
- "Add an entry yesterday at 4pm for 45 mins"

## When NOT to Use

- Sprint planning, board updates, task assignment changes
- Invoice generation and tax calculations
- Bulk migration of historical data from another system
- Calendar-to-timesheet auto backfill without explicit user review


## Toggl MCP Integration

This skill integrates with the **iThink 365 Time Tracking Toggl MCP Server**
configured in this project manifest:

- MCP endpoint:
  `https://[your toggl mcp server endpoint]/api/mcp`

The MCP server abstracts Toggl API details and authentication.

## Quick Start

```text
User: "Show me what I tracked today and add 90 minutes for proposal writing from 14:00"

1. Resolve timezone and date context (default to user's local timezone if known)
2. Fetch entries for today and present summary first
3. Parse new entry request (description/project/task/start/duration)
4. Convert local start/end to UTC
5. Validate no overlap and required fields
6. Create the entry via MCP
7. Confirm created entry in local time and UTC
```

## Core Instructions

### Phase 1: Resolve Context

- Identify requested period: today, yesterday, this week, last week, custom range
- Identify user timezone (IANA preferred, for example `Europe/London`)
- If timezone is missing and cannot be inferred, ask once before creating entries
- For read requests, proceed with best-known timezone and state the assumption

### Phase 2: Retrieve Entries

- Query Toggl entries for the resolved UTC range
- Include: description, project, task, start, stop, duration, billable, tags
- Return both:
  - Itemized view (chronological)
  - Summary view (total hours by project/task)

### Phase 3: Create New Entry

Collect or infer the minimum required fields:

- `description`
- `start` (local datetime) and one of:
  - explicit `stop` time, or
  - `duration` in minutes
- `project` (or project id)
- optional: `task`, `billable`, `tags`

Validation before create:

- start must be before stop
- duration must be positive
- no conflicting overlap unless user explicitly confirms
- if project/task is ambiguous, show options and ask user to choose
- if client is not obvious, then search for projects under iThink 365 otherwise ask user to specify client to do the project search for.

### Phase 4: Timezone and UTC Conversion (Mandatory)

All Toggl API write operations must use UTC timestamps.

Rules:

- Treat user-entered date/time as local time in the resolved timezone
- Convert local datetime to UTC using timezone-aware conversion
- Never apply manual fixed offsets (for example, never "always -1 hour")
- Respect DST transitions automatically through timezone libraries
- Store/send timestamps as ISO 8601 UTC (ending in `Z`)

Conversion examples:

- Local: `2026-05-22 09:00` in `Europe/London` (BST, UTC+1)
  -> UTC: `2026-05-22T08:00:00Z`
- Local: `2026-12-02 09:00` in `Europe/London` (GMT, UTC+0)
  -> UTC: `2026-12-02T09:00:00Z`

For entries crossing midnight locally:

- Convert both local start and local stop independently to UTC
- Do not assume same UTC date as local date

For ambiguous/non-existent DST times:

- If local time is ambiguous (clock goes back), ask user which occurrence
- If local time does not exist (clock jumps forward), ask for corrected time

### Phase 5: Confirm Back to User

After create/update actions, confirm with both local and UTC values:

- description, project/task
- local start/stop
- UTC start/stop sent to API
- calculated duration
- entry id (if available)

## Output Format

For "show my time" responses:

```text
Timesheet: <range in local timezone>

Total: <hours>h

By project:
- <Project A>: <hours>h
- <Project B>: <hours>h

Entries:
- <HH:mm-HH:mm> <description> (<project>/<task>) - <duration>
- ...
```

For "create entry" responses:

```text
Created time entry: <description>
- Project/Task: <project> / <task or none>
- Local: <YYYY-MM-DD HH:mm> to <YYYY-MM-DD HH:mm> (<timezone>)
- UTC sent to Toggl: <startZ> to <stopZ>
- Duration: <minutes> min
```

## Guardrails

- Never guess project/task when multiple matches exist; ask user to choose
- Never send local timestamps directly to Toggl APIs
- Always display the timezone assumption used for conversion
- If required fields are missing, ask only for missing fields
- Keep summaries factual and traceable to returned entries
- Do not expose tokens, secrets, or raw auth payloads
- Check that the user is logged into Toggl before attempting API calls.
- If the user is not logged in, prompt them to log in and provide their Toggl username and password, defaulting the username to their email address if possible.
- If MCP is unavailable, explain that Toggl access is temporarily unavailable and
  provide a retry suggestion

