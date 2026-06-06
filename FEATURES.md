# Autocoder — Feature & Flow Summary

This document describes every feature and flow that currently exists in the codebase, split by layer. "Exists" means implemented and tested or verified running — not just planned.

---

## 1. Core Concepts

### Board
A project workspace. Owns an ordered pipeline of columns, a set of linked git repositories, and global instructions prepended to every Claude prompt on that board.

### Column
A stage in the pipeline. Two types:
- **Input** — a holding area. No agent runs. Users create tasks here (To Do) or tasks land here at completion (Done).
- **Agent** — Claude is invoked automatically when a task enters with `Waiting` status.

### Task
The unit of work that travels left→right through the pipeline. Carries a title, description, status, branch name (set at spec time), the full context chain of every step so far, and any pending question from the agent.

### Context Chain
An ordered log of every interaction a task has had: agent outputs (with action and structured data), user answers to agent questions, and system notes (e.g. retry warnings). Every new agent run receives the entire chain as history, so it always has full context of everything that happened before it.

---

## 2. Task Statuses

| Status | Meaning |
|---|---|
| `Waiting` | In an agent column, ready for the orchestrator to pick up |
| `Running` | Agent is actively executing for this task |
| `Asking` | Agent paused mid-column and is waiting for a user answer before continuing |
| `Error` | Agent timed out, crashed, or failed to produce valid structured output |
| `Done` | Task has reached the final column |

---

## 3. Transition Actions

Every agent column produces exactly one of three actions in its structured output:

| Action | What happens |
|---|---|
| `forward` | Task moves to the next column. If `AutoForward` is on, it moves immediately; otherwise it waits as `PendingApproval` until progressed |
| `backward` | Task moves to the column specified by `BackwardTargetColumnId` (or the previous agent column if not set). Used for rework loops |
| `ask` | Task stays in the same column with status `Asking`. The agent's question is surfaced to the user; when answered, the answer is appended to the context chain and the agent re-runs in the same column |

---

## 4. Backend Features (C# / .NET 7)

### OrchestratorService
The central coordination engine. Four public operations:

**`ProcessTaskAsync`**
1. Loads task with its full board, columns, repositories, and context chain
2. Validates task is `Waiting` or `Asking` in an agent column
3. Sets status → `Running`, saves
4. Builds the full prompt via `PromptBuilder`
5. Spawns agent with a configurable per-column timeout
6. On timeout: sets status → `Error` with message, stops
7. On missing structured output: sets status → `Error`, stops
8. On success: extracts `branchName` from output if not yet set → calls `IGitService.SetupWorktreeAsync`
9. Saves a `ContextEntry` (kind: `AgentOutput`) with full text, structured data, and action
10. Routes based on action: `forward` → next column, `backward` → configured target, `ask` → status `Asking` + stores question

**`SubmitAnswerAsync`**
1. Validates task is `Asking`
2. Saves a `ContextEntry` (kind: `UserAnswer`) with the user's text
3. Resets status → `Waiting`
4. Immediately calls `ProcessTaskAsync` — the agent re-runs the same column with updated context

**`RetryTaskAsync`**
1. Validates task is `Error`
2. Saves a `ContextEntry` (kind: `SystemNote`) instructing the agent to produce valid structured output
3. Clears `ErrorMessage`, resets status → `Waiting`
4. Immediately calls `ProcessTaskAsync`

**`ApproveTaskAsync`**
1. Validates task is `PendingApproval`
2. If current column is an Input type → `Done`; otherwise → `Waiting` (agent runs next)

### PromptBuilder
Assembles the Claude prompt in four sections:
1. **Preamble** — board name, global instructions, list of all linked repositories with local paths
2. **Task context** — title, description, current branch name
3. **History** — every `ContextEntry` in chronological order (agent outputs labelled with column name and action; user answers labelled as responses; system notes labelled as system)
4. **Column instructions** — this column's configured instruction text + the required structured output format with action/summary/question fields and any column-specific schema hints

### Data Model (EF Core / SQLite)
- `Board` — id, name, global instructions
- `Column` — id, board, name, type, position, instructions, output schema hint, backward target column, auto-forward flag, timeout seconds, max agent turns
- `WorkTask` — id, board, title, description, current column, status, branch name, worktree path, pending question, error message, timestamps
- `ContextEntry` — id, task, kind (AgentOutput/UserAnswer/SystemNote), column snapshot, content, structured data JSON, action, timestamp
- `BoardRepository` — id, board, display name, local path, default branch

### Interfaces
- `IAgentRunner` — `RunAsync(prompt, ct)` → `AgentResult` (full output text, parsed structured output, timed-out flag)
- `IGitService` — `SetupWorktreeAsync(task, ct)`, `TeardownWorktreeAsync(task, ct)`
- `IOrchestrator` — `ProcessTaskAsync`, `SubmitAnswerAsync`, `RetryTaskAsync`, `ApproveTaskAsync`

---

## 5. Flow Tests (45 passing)

Each scenario tests the orchestrator end-to-end against an in-memory database with a mock `IAgentRunner` (per-column response queues) and a mock `IGitService`.

| Scenario | What it proves |
|---|---|
| 1 — Happy path | Task visits each column once, branch set after spec, git called once, Done |
| 2 — Code review rework | Backward → In Progress → Code Review (second visit) → Done; context chain records both visits in order |
| 3 — Ask during testing | Task stays in Testing on `ask`, user answer saved as `UserAnswer` entry, second agent run receives both question and answer, backward routes to In Progress not Code Review |
| 4 — Multi-round ask | Two ask/answer pairs in one column, three agent runs total; third run's prompt contains both Q&A pairs; column visited exactly three times |
| 5 — Invalid output | Error state set, no context entry saved, task stays in column; retry adds system note, system note appears in retry prompt |
| 6 — Backward skips columns | `BackwardTargetColumnId` overrides positional logic; Testing failure → In Progress (not Code Review); after fix, normal forward path resumes from In Progress's next column |
| 7 — Parallel tasks | Two tasks run simultaneously, each gets its own branch name and worktree setup call, context chains are completely isolated, one task's failure does not affect the other |
| 8 — Timeout | Error state set, no context entry saved, branch name preserved, cannot process again without retry |
| 9 — Context chain integrity | After a 5-step complex flow (spec → in-progress×2 → code-review×2), Testing agent's prompt contains all 5 prior entries in chronological order with correct action labels |

---

## 6. Frontend Features (React + TypeScript + Tailwind)

### Pages & Routes
- `/` or `/board/:boardId` → Board page (Kanban + drawer + modals)
- `/settings` → Settings page

### Board Page

**Kanban board (desktop)**
- Six columns rendered left→right in fixed 240px swimlanes with horizontal scroll
- Column headers sticky, showing name (uppercase tracked), task count, and an animated blue dot when any task in that column is Running
- Tasks sorted within each column by priority: Asking first, then Error, Running, Waiting, Done last

**Task cards — five visual states:**

| State | Visual |
|---|---|
| Waiting | Neutral border, title + column + timestamp |
| Running | Blue left border, animated pulse dot, last line of live agent output in monospace |
| Asking | Amber left border, ⚡ icon, truncated question text (italic), `Answer →` chip — click opens drawer |
| Error | Red left border, ✕ icon, error message (2 lines), inline Retry button |
| Done | No border, muted text, right-aligned ✓ |

**Attention banner**
- Appears at the top of the board whenever any task is in `Asking` state
- Shows count + each task's name as a clickable link that opens its detail drawer
- Disappears automatically when no tasks need input

**Task detail drawer**
- Slides in from the right on desktop (480px wide), slides up from the bottom on mobile
- Escape key closes
- Header: task title, status chip, column name, branch name (indigo monospace)
- **Answer panel** (Asking tasks only): amber-bordered section at top, full question text, auto-focused textarea, `Submit answer →` button, ⌘ Enter shortcut
- **Live output panel** (Running tasks): dark terminal background, monospace text, auto-scrolls to bottom; `<<<STRUCTURED_OUTPUT>>>` line and everything after it renders in green
- **History**: collapsible steps, each showing column name + action badge (Forward=green, Backward=amber, Ask=amber+⚡) + relative timestamp; expanded shows full agent text in monospace block
- **Details**: branch (indigo monospace), board name, created/updated relative times, error message if present

**Create task modal**
- Triggered by `+ New` button or `N` keyboard shortcut anywhere on the page (not in an input)
- Single focused title input, Enter submits
- `+ Add description` expander reveals optional textarea
- Task is created directly in the first agent column with `Waiting` status
- Escape closes

### Settings Page

**Repositories tab**
- List of linked git repositories showing display name + local path (monospace)
- Inline edit form per repository: display name, local path, default branch
- Add and delete

**Board tab**
- Board name field
- Global instructions textarea (prepended to every agent prompt)
- Claude CLI path field
- Save button with confirmation flash

**Columns tab**
- All six columns listed in pipeline order
- Up/down arrow buttons to reorder
- Input columns show type badge only (no expand)
- Agent columns expandable with:
  - Instructions textarea
  - Auto-forward toggle (indigo pill switch)
  - Backward target column dropdown (other agent columns or "Previous agent column (default)")
  - Timeout (seconds) number input
  - Max agent turns number input

### Mobile Layout
- Bottom navigation bar with two tabs: **⚡ Focus** and **⊞ Board**
- Badge on Focus tab showing count of tasks needing input
- **Focus tab**: shows only `Asking` tasks as full-width cards; empty state: "All clear. The agents are working."
- **Board tab**: horizontal scrollable column chip strip (All + each column name); tapping a chip filters the vertical card list below to that column

---

## 7. Key Flows (End to End)

### Flow A — Normal task completion
1. User opens create modal (`+ New` or `N`), types title, presses Enter
2. Task appears in In Specification column as Waiting
3. Orchestrator picks it up → status Running; frontend shows blue dot + streaming output preview on card
4. Agent produces `forward` with `branchName` in structured output
5. Orchestrator extracts branch name → calls Git Service to create branch and worktree
6. Task advances to In Progress (auto-forward=true → immediately Waiting, no human gate)
7. In Progress agent writes code, produces `forward`
8. Task advances to Code Review (auto-forward=true)
9. Code Review agent reviews diff, produces `forward`
10. Task advances to Testing (auto-forward=false → PendingApproval — but UI no longer shows approve buttons; orchestrator handles this internally)
11. Testing agent runs all tests, produces `forward`
12. Task moves to Done; worktree removed; branch remains for PR

### Flow B — Agent asks a clarifying question
1. Testing agent is uncertain about a requirement; produces `ask` with a question string
2. Orchestrator sets status → `Asking`; saves question; context entry (kind: AgentOutput, action: Ask) recorded
3. Attention banner appears on board; task card shows amber border + truncated question + `Answer →`
4. User clicks card → drawer opens; answer panel shown at top with full question
5. User types answer, presses `Submit answer →` (or ⌘ Enter)
6. Frontend calls `handleAnswer(taskId, text)`
7. Orchestrator saves `ContextEntry` (kind: UserAnswer), resets status → Waiting, re-runs agent in same column
8. Agent's new prompt includes both the question entry and the user's answer entry in history
9. Agent produces `forward` or `backward`; task transitions accordingly
10. Multiple ask rounds possible: each adds another UserAnswer+AgentOutput pair to the chain

### Flow C — Rework loop
1. Code Review agent finds issues; produces `backward` with issue list in structured data
2. Orchestrator reads Code Review column's `BackwardTargetColumnId` = In Progress
3. Context entry (kind: AgentOutput, action: Backward) saved with issue details
4. Task moves to In Progress, status Waiting
5. In Progress agent's prompt now includes the Code Review feedback entry in history
6. Agent addresses the issues, produces `forward`
7. Task moves to Code Review again (next column after In Progress in the pipeline)
8. Code Review agent runs again with full history including both In Progress runs and the prior backward
9. If it produces `forward`, task proceeds to Testing; if `backward` again, cycle repeats

### Flow D — Agent error and retry
1. Agent produces a response without the `<<<STRUCTURED_OUTPUT>>>` sentinel (or times out)
2. Orchestrator sets status → `Error`; saves error message; no context entry added (run was invalid)
3. Task card shows red left border, error message, inline Retry button
4. User clicks Retry
5. Orchestrator adds a `SystemNote` context entry reminding agent to produce valid output
6. Status resets to Waiting, agent re-runs
7. Retry prompt includes the system note so the agent knows what went wrong

### Flow E — Parallel tasks (cross-task isolation)
1. Multiple tasks can be in agent columns simultaneously
2. Each task has its own worktree (separate directory per task, per repository)
3. Context chains are per-task; agents for different tasks never share history
4. A failure in one task (timeout, bad output) has no effect on other running tasks

---

## 8. What Is Not Yet Built

| Area | Status |
|---|---|
| HTTP API controllers | Not implemented — no routes, no request/response wiring |
| SignalR hub | Stubbed in frontend (`useSignalR` hook is a no-op comment) |
| Real Claude CLI runner | `IAgentRunner` interface exists; no production implementation |
| Real Git Service | `IGitService` interface exists; no production implementation |
| Backend startup / hosting | No `Program.cs`, no DI configuration |
| Structured output parser | Sentinel extraction logic not implemented in backend |
| Multiple boards | Data model supports it; UI hardcoded to one board |
| Board/column create/delete in UI | Settings shows edit only; no add column or delete board in UI |
| Task description visible in UI | Field exists in data model; not displayed anywhere |
| Cost / token tracking | Listed as future work |
| OS notifications | Listed as future work |
| PR auto-creation via `gh` | Listed as future work |
