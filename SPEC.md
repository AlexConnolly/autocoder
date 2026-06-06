# Autocoder — Product Specification

## Overview

Autocoder is a locally-hosted Kanban-style AI agent orchestration system. Users create tasks on a board; agents automatically pick them up, process them through configurable pipeline stages, and produce code — all by driving Claude via the CLI. Human approval gates can be inserted at any stage. Everything runs on the developer's own machine.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core (.NET 9), C# |
| Real-time | SignalR |
| Frontend | React 19, TypeScript, Tailwind CSS, Vite |
| Persistence | SQLite via EF Core |
| AI Execution | Claude CLI (spawned as child processes) |
| VCS | Git + Git Worktrees |

---

## Core Concepts

### Board
A board represents a project or work stream. It owns:
- An ordered list of **Columns**
- A set of linked **Repositories**
- Global **board-level instructions** prepended to every agent prompt

### Column
Columns are the stages of the pipeline. Tasks flow left → right (and may flow right → left for rework loops). There are two column types:

| Type | Behaviour |
|---|---|
| `agent` | An orchestrator agent picks up tasks automatically and runs a Claude session |
| `input` | A holding column where users create tasks; no agent runs here |

Columns cannot be skipped. Every agent column's structured output contains an `action` field that drives the transition.

### Transition Actions

Every `agent` column produces exactly one of three transition actions:

| Action | Meaning |
|---|---|
| `forward` | Work complete — move task to the next column |
| `backward` | Work failed or needs rework — move task to a configured target column |
| `ask` | Agent needs clarification — pause, surface a question to the user, resume when answered |

**`backward` target**: Each column has a configurable `backwardTargetColumnId`. The agent simply signals `backward`; the orchestrator knows where to send it. Example: a Testing column's backward target is "In Progress", not "Code Review" — the agent doesn't decide which column, only that it failed.

**`ask` flow**: The agent is not done and does not transition. The orchestrator sets status → `asking`, surfaces the agent's question in the UI, waits for user text input, appends the answer to the context chain, then re-runs the same column's agent with updated context. The agent then produces `forward` or `backward`.

### Task (Card)
A task is the unit of work that travels through the board. It carries:
- **Title** — short user-entered description
- **Context chain** — ordered list of `StepOutput` records produced by each column
- **Current column**
- **Status** — `waiting`, `running`, `gated`, `done`, `error`
- **Branch name** — set during the spec stage, used for all git operations
- **Assigned repositories** — inherited from the board, overridable per task

### StepOutput
Every time an agent column completes, it produces a `StepOutput`:
```json
{
  "columnId": "uuid",
  "columnName": "In Specification",
  "agentOutput": "...(full markdown/text the agent produced)...",
  "structuredData": { /* column-defined JSON schema */ },
  "timestamp": "ISO-8601"
}
```
The full context chain is serialised and injected into the next agent's prompt.

---

## Example Pipeline

```
[To Do] → [In Specification] → [Approval Gate] → [In Progress] → [Code Review] → [Done]
```

1. **To Do** — user creates a task here; no agent, just a holding column
2. **In Specification** *(agent)* — Claude produces a technical spec + defines the git branch name (structured output)
3. **Approval Gate** *(gate)* — user reviews spec and approves/rejects (or auto-approve fires)
4. **In Progress** *(agent)* — Claude writes code on the worktree branch, following the spec from context
5. **Code Review** *(agent)* — Claude reviews the diff; either approves (→ Done) or rejects (→ In Progress with feedback appended to context)
6. **Done** — task complete; branch available for PR

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Browser (React + Tailwind)                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │ Kanban Board │  │ Task Detail  │  │ Settings               │ │
│  │ (live via    │  │ (context     │  │ (repos, columns,       │ │
│  │  SignalR)    │  │  chain view) │  │  instructions)         │ │
│  └──────────────┘  └──────────────┘  └────────────────────────┘ │
└───────────────────────────┬─────────────────────────────────────┘
                            │ HTTP + SignalR (WebSocket)
┌───────────────────────────▼─────────────────────────────────────┐
│  ASP.NET Core API                                                │
│  ┌──────────────────┐  ┌──────────────────┐                     │
│  │ BoardController  │  │ TaskController   │                     │
│  │ ColumnController │  │ SettingsCtrl     │                     │
│  └────────┬─────────┘  └────────┬─────────┘                     │
│           └──────────┬──────────┘                               │
│              ┌───────▼────────┐                                 │
│              │  Orchestrator  │  (hosted background service)    │
│              │  Service       │                                 │
│              └───────┬────────┘                                 │
│          ┌───────────┼───────────┐                              │
│   ┌──────▼──────┐ ┌──▼───────┐ ┌▼──────────────┐              │
│   │ Agent Runner│ │  Gate    │ │  Git Service  │              │
│   │ (Claude CLI │ │  Manager │ │  (worktrees,  │              │
│   │  process)   │ │          │ │   branches)   │              │
│   └─────────────┘ └──────────┘ └───────────────┘              │
│                                                                  │
│              ┌───────────────────┐                              │
│              │  SQLite (EF Core) │                              │
│              └───────────────────┘                              │
└─────────────────────────────────────────────────────────────────┘
                            │ child process (stdin/stdout)
                    ┌───────▼────────┐
                    │  claude CLI    │
                    └────────────────┘
```

### Orchestrator Service
A .NET `BackgroundService` that runs a polling loop (or is triggered by SignalR events). Its responsibilities:

1. **Scan** all boards for tasks in `agent` columns with status `waiting`
2. **Claim** a task (set status → `running`)
3. **Build** the prompt for that column (see Prompt Construction below)
4. **Spawn** a `claude` CLI child process, stream output back via SignalR
5. **Parse** the final structured output from the agent response
6. **Transition** the task: forward if successful, backward if the agent signals rework
7. For `gate` columns: set status → `gated`, notify frontend; wait for human action

### Agent Runner
Each running agent is an isolated child process:
```
claude --output-format json --max-turns N [--allowedTools ...]
```
- Stdout is streamed line by line → SignalR hub → frontend (live output panel)
- On exit, the final JSON block is extracted and parsed as `StructuredOutput`
- A per-agent timeout is configurable

### Git Service
- The branch name is established in the spec step's structured output (e.g. `feature/csv-download-button`)
- After the spec step, a **git worktree** is created for that branch at `{WorktreeBaseDir}\{taskId}`
- Each task gets exactly **one worktree** that persists for the task's entire lifetime — rework loops reuse it; the agent simply commits additional changes to the same branch
- **Multiple tasks run in parallel**, each in its own worktree on its own branch — worktrees make this safe with no cross-task interference
- On task completion (Done), the worktree is removed; the branch remains for PR creation
- Each linked repository gets its own worktree directory; a task touching multiple repos has multiple worktrees

---

## Prompt Construction

For each agent column, the system assembles a prompt in this order:

```
1. SYSTEM PREAMBLE
   - Board name, purpose
   - List of accessible repositories (name + local path)
   - Global board instructions

2. TASK CONTEXT
   - Task title
   - Full context chain: for each previous StepOutput, include columnName + agentOutput

3. COLUMN INSTRUCTIONS
   - The instructions configured for THIS column
   - The structured output schema this column must produce

4. CURRENT STATE (if rework)
   - Any feedback appended by a downstream agent that sent the task backward
```

This means every agent has full history and knows exactly where in the pipeline it sits.

---

## Structured Output Schema

Every `agent` column's structured output **always** includes a standard envelope, plus column-specific fields:

```json
{
  "action": "forward | backward | ask",
  "question": "string (required only when action=ask)",
  "summary": "string (human-readable description of what was done/decided)",
  // ... column-specific fields defined per-column
}
```

The orchestrator extracts this block using a sentinel pattern:

```
<<<STRUCTURED_OUTPUT>>>
{ ... }
<<<END_STRUCTURED_OUTPUT>>>
```

The `action` field drives all routing. Column-specific fields are stored in the `StepOutput` for context passing.

### Example: In Specification column
```json
{
  "action": "forward",
  "summary": "Spec complete. Branch name defined.",
  "branchName": "feature/csv-download-button",
  "acceptanceCriteria": ["CSV export downloads all visible rows", "..."],
  "technicalApproach": "Add an endpoint at GET /api/export/csv ...",
  "filesToChange": ["src/api/export.ts", "src/components/Table.tsx"]
}
```

### Example: Code Review column — approving
```json
{
  "action": "forward",
  "summary": "Code meets standards. No issues found.",
  "issues": []
}
```

### Example: Code Review column — requesting rework
```json
{
  "action": "backward",
  "summary": "Found two issues that need fixing before merge.",
  "issues": [
    "Missing null check on line 42 of export.ts",
    "CSV headers don't match the agreed spec"
  ]
}
```

### Example: Testing column — asking a question
```json
{
  "action": "ask",
  "question": "Tests pass but I noticed the export omits soft-deleted rows. Is that intentional, or should deleted rows be included in the export?",
  "summary": "Pending clarification before deciding pass/fail."
}
```

After the user answers, the answer is appended to the context chain and the Testing agent re-runs. It will then produce `forward` or `backward`.

---

## Data Model

```
Board
  - Id (UUID)
  - Name
  - GlobalInstructions (text)
  - Repositories[] → BoardRepository

BoardRepository
  - Id
  - BoardId
  - Name (display)
  - LocalPath (absolute path on disk)
  - DefaultBranch

Column
  - Id
  - BoardId
  - Name
  - Type (agent | input)
  - Position (int, ordered)
  - Instructions (text — injected into agent prompt)
  - OutputSchemaHint (text — describes column-specific fields the agent should include)
  - BackwardTargetColumnId (nullable UUID — where "backward" sends the task; null = previous column)
  - AutoForward (bool — if true, skip human review and transition immediately on "forward")
  - TimeoutSeconds (int)
  - MaxAgentTurns (int)

Task
  - Id
  - BoardId
  - Title
  - Description
  - CurrentColumnId
  - Status (waiting | running | asking | done | error)
  - BranchName (nullable, set after spec step)
  - WorktreePath (nullable)
  - CreatedAt / UpdatedAt

StepOutput
  - Id
  - TaskId
  - ColumnId
  - ColumnName (snapshot)
  - AgentOutput (full text)
  - Action (forward | backward | ask)
  - StructuredData (JSON string — full parsed output including action + column-specific fields)
  - CreatedAt

ContextEntry   ← covers both StepOutputs and user answers in one ordered chain
  - Id
  - TaskId
  - Kind (agent_output | user_answer)
  - ColumnId (nullable — null for user_answer)
  - ColumnName (snapshot, nullable)
  - Content (text)
  - StructuredData (JSON string, nullable)
  - CreatedAt
```

The `ContextEntry` table is the single source of truth for the full conversation history passed to each agent. Both agent outputs and user answers to `ask` prompts are stored here in order.

---

## Frontend Pages

### Kanban Board (`/board/:id`)
- Columns rendered left → right as swimlanes
- Each task card shows: title, status badge, active streaming output (if running)
- Drag is disabled — movement is agent-driven only
- Tasks with status `asking` show the agent's question inline with a text input and Submit button
- Tasks in `error` state show a retry button

### Task Detail (`/board/:id/task/:taskId`)
- Full context chain: each step expanded with agent output + structured data
- Live streaming panel (SSE/SignalR) for the currently running agent
- Git branch name + worktree status
- Manual transition controls (admin override)

### Settings (`/settings`)

**Repositories tab**
- Add / remove / edit repository entries (name + local path)
- Validate that the path is a git repo on save

**Boards tab**
- Create / rename / delete boards
- Set global instructions per board
- Toggle auto-approve

**Columns tab** (per board)
- Add / reorder / remove columns
- Set column type (agent / input)
- Write per-column Claude instructions
- Describe the column-specific output fields (free text hint sent to the model)
- Set backward target column (dropdown of other columns; default = previous)
- Toggle auto-forward (skip human review after agent produces `forward`)
- Set timeout and max-turns

---

## Configuration & Settings (persisted)

Stored in `appsettings.local.json` (gitignored):
```json
{
  "Claude": {
    "CliPath": "claude",
    "DefaultMaxTurns": 10,
    "DefaultTimeoutSeconds": 300
  },
  "Git": {
    "WorktreeBaseDir": "%TEMP%\\autocoder\\worktrees"
  }
}
```

Board/column/repo config is stored in SQLite (user-editable via Settings UI).

---

## Key Flows

### Flow 1: Task Creation → Specification
1. User types title in To Do column → POST `/api/tasks`
2. Orchestrator detects task in first `agent` column
3. Builds prompt, spawns `claude` CLI
4. Streams output to frontend via SignalR
5. On completion, parses structured output → extracts `branchName`
6. Git Service creates branch + worktree
7. Task transitions to next column

### Flow 2: Agent asks a question
1. Testing agent runs; is uncertain about a requirement
2. Produces `{ "action": "ask", "question": "Should deleted rows be included in the CSV export?" }`
3. Orchestrator sets task status → `asking`; stores the question in a `ContextEntry` (kind=`agent_output`)
4. Frontend surfaces the question inline on the task card with a text input
5. User types answer → POST `/api/tasks/:id/answer` with `{ "text": "No, exclude soft-deleted rows" }`
6. Orchestrator appends a `ContextEntry` (kind=`user_answer`) to the chain
7. Orchestrator re-runs the Testing agent in the same column with updated context
8. Agent now produces `forward` or `backward`

### Flow 3: Backward transition with a configured target
1. Testing agent runs, produces `{ "action": "backward", "summary": "3 tests failing", "issues": [...] }`
2. Orchestrator reads Testing column's `BackwardTargetColumnId` → "In Progress" (not "Code Review")
3. Task moves to In Progress; `StepOutput` with the failure summary is appended to context chain
4. In Progress agent picks it up; context chain includes the test failure details
5. Agent fixes the issues and produces `{ "action": "forward" }`
6. Task proceeds to Code Review (not back to Testing — Testing comes after Code Review in the pipeline)

### Flow 4: Rework loop (Code Review → In Progress → Code Review)
1. Code Review agent produces `{ "action": "backward", "issues": [...] }`
2. Code Review column's `BackwardTargetColumnId` = "In Progress"
3. Task moves to In Progress with feedback in context
4. In Progress agent addresses feedback, produces `{ "action": "forward" }`
5. Task moves to Code Review again
6. Cycle repeats until Code Review produces `{ "action": "forward" }`

---

## Open Questions / Future Work

- **Concurrency limit**: How many tasks can run simultaneously per board? (Configurable per board; default 2)
- **Notifications**: Local toast / OS notification when a task enters `asking` state
- **PR creation**: Auto-open a PR via `gh` CLI after a task reaches Done
- **Board templates**: Pre-built column pipelines (spec → code → review, or hotfix → deploy)
- **Agent tool permissions**: Per-column allowlist of Claude tools (Bash, Edit, etc.)
- **Cost tracking**: Log token usage per task/step for visibility
- **Ask depth limit**: Should there be a maximum number of back-and-forth `ask` rounds before the task is flagged as `error`?
