# Autocoder — Flow Test Scenarios

These are behavioural scenarios describing expected system behaviour. Each scenario defines the board setup, the inputs, the expected agent outputs at each step, the task's path through the board, and how failures should manifest.

---

## Board Setup (shared across all scenarios)

**Board:** "Main Project"  
**Repositories:** `{ name: "api", path: "C:\repos\api" }`, `{ name: "frontend", path: "C:\repos\frontend" }`  
**Global Instructions:** "Always follow existing code conventions. Write TypeScript. Use the project's existing test framework."

**Columns (in order):**

| # | Name | Type | Backward Target | Auto-Forward |
|---|---|---|---|---|
| 1 | To Do | input | — | — |
| 2 | In Specification | agent | — (n/a) | false |
| 3 | In Progress | agent | — (n/a) | true |
| 4 | Code Review | agent | In Progress | true |
| 5 | Testing | agent | In Progress | false |
| 6 | Done | input | — | — |

---

## Scenario 1 — Happy Path: Full Forward Run

**Description:** A task flows through every column without any rework or questions.

### Setup
- Task created: `"Add CSV export button to the data table"`
- Task starts in: To Do

### Step 1 — Orchestrator picks up task → In Specification

**Prompt includes:**
- Task title
- Board global instructions
- Column instructions: "Produce a full technical specification. Define the branch name."

**Expected agent structured output:**
```json
{
  "action": "forward",
  "summary": "Spec written. Branch defined.",
  "branchName": "feature/csv-export-button",
  "acceptanceCriteria": [
    "A 'Download CSV' button appears above the data table",
    "Clicking it downloads all visible (non-deleted) rows as a .csv file",
    "CSV headers match the column display names"
  ],
  "technicalApproach": "Add GET /api/export/csv endpoint in api repo. Add ExportButton component in frontend repo.",
  "filesToChange": ["api/src/routes/export.ts", "frontend/src/components/Table.tsx"]
}
```

**Expected system behaviour:**
- `StepOutput` saved to context chain
- Branch `feature/csv-export-button` created in both repos
- Worktrees checked out at `%TEMP%\autocoder\{taskId}\api` and `%TEMP%\autocoder\{taskId}\frontend`
- Task moves to In Progress (auto-forward = false, so user sees task card with "Approve" option)

> **Gate check:** User reviews spec in task detail view, clicks Approve.  
> Task status → waiting in In Progress.

---

### Step 2 — In Progress

**Prompt includes:**
- Full context chain (spec step output)
- Column instructions: "Implement the specification. Work in the provided worktrees. Commit your changes."

**Expected agent structured output:**
```json
{
  "action": "forward",
  "summary": "Implementation complete. Endpoint and component written and committed.",
  "commitHash": "a1b2c3d",
  "filesChanged": ["api/src/routes/export.ts", "frontend/src/components/Table.tsx"]
}
```

**Expected system behaviour:**
- Task moves to Code Review (auto-forward = true, no human gate)

---

### Step 3 — Code Review

**Prompt includes:**
- Full context chain (spec + in-progress outputs)
- Column instructions: "Review the diff against the spec's acceptance criteria. Check for correctness, style, and edge cases."

**Expected agent structured output:**
```json
{
  "action": "forward",
  "summary": "Code meets spec. No issues found.",
  "issues": []
}
```

**Expected system behaviour:**
- Task moves to Testing (auto-forward = true)

---

### Step 4 — Testing

**Prompt includes:**
- Full context chain
- Column instructions: "Run the test suite in both repos. Verify all acceptance criteria are met."

**Expected agent structured output:**
```json
{
  "action": "forward",
  "summary": "All tests pass. Acceptance criteria verified.",
  "testResults": "47 passed, 0 failed"
}
```

**Expected system behaviour:**
- Testing has auto-forward = false; user sees Approve button
- User clicks Approve
- Task moves to Done
- Worktrees removed; branches remain for PR

### Pass Criteria
- Task visits each column exactly once
- Context chain has 4 entries (one per agent column)
- Branch exists in both repos with commits
- No `asking` or `error` status at any point

---

## Scenario 2 — Code Review Failure → Rework Loop → Done

**Description:** Code review identifies issues. Task goes back to In Progress, is fixed, and then passes review.

### Setup
- Same task: `"Add CSV export button to the data table"`
- Spec and In Progress complete normally (same as Scenario 1, Steps 1–2)

### Step 3a — Code Review (first attempt)

**Expected agent structured output:**
```json
{
  "action": "backward",
  "summary": "Two issues found that must be fixed before merge.",
  "issues": [
    "Missing null check: if the table has no rows, /api/export/csv throws a 500",
    "CSV headers use internal field names (e.g. 'created_at') instead of display names (e.g. 'Created Date')"
  ]
}
```

**Expected system behaviour:**
- Context chain entry appended (action=backward, issues logged)
- Code Review column's `BackwardTargetColumnId` = In Progress
- Task moves back to In Progress (status = waiting)
- Task card shows visible indicator: "Returned from Code Review"

---

### Step 3b — In Progress (second attempt)

**Prompt includes:**
- Full context chain including the Code Review backward entry
- Context chain clearly shows: previous In Progress run + Code Review feedback

**Expected agent structured output:**
```json
{
  "action": "forward",
  "summary": "Fixed null check and corrected CSV header mapping.",
  "commitHash": "d4e5f6a",
  "filesChanged": ["api/src/routes/export.ts"]
}
```

**Expected system behaviour:**
- Task moves to Code Review again (second visit)

---

### Step 3c — Code Review (second attempt)

**Prompt includes:**
- Full context chain: spec, in-progress #1, review #1 (backward), in-progress #2

**Expected agent structured output:**
```json
{
  "action": "forward",
  "summary": "Previous issues resolved. Code approved.",
  "issues": []
}
```

**Expected system behaviour:**
- Task moves to Testing
- Continues to Done (assuming Testing passes)

### Pass Criteria
- Context chain has 6 entries: spec, in-progress×2, code-review×2 (one backward, one forward), testing
- In Progress was visited twice
- Code Review was visited twice
- Task reaches Done without error
- The second In Progress run's agent demonstrably addressed the specific issues from the review (verifiable in the agent output text)

---

## Scenario 3 — Agent Asks a Question During Testing

**Description:** The testing agent is uncertain about a requirement and asks the user before deciding pass/fail.

### Setup
- Task has passed Spec, In Progress, Code Review
- Now in Testing

### Step 4a — Testing (first run)

The agent runs tests; all pass, but it notices an ambiguity in the spec.

**Expected agent structured output:**
```json
{
  "action": "ask",
  "question": "All 47 tests pass. However, I noticed the export currently includes soft-deleted rows (rows where deleted_at is set). The spec says 'all visible rows' but does not define whether soft-deleted rows count as visible. Should soft-deleted rows be included or excluded from the CSV export?",
  "summary": "Awaiting clarification before deciding pass/fail."
}
```

**Expected system behaviour:**
- Task status → `asking`
- Task card displays the question inline with a text input field
- No column transition occurs
- A `ContextEntry` of kind=`agent_output` is saved with action=`ask`

---

### Step 4b — User answers

User types: `"Exclude soft-deleted rows. The acceptance criteria says 'visible rows' which matches what the UI shows — deleted rows are hidden."`

- POST `/api/tasks/{id}/answer` with that text
- A `ContextEntry` of kind=`user_answer` is appended to chain
- Task status → `running`
- Testing agent re-runs in the same column

---

### Step 4c — Testing (second run, same column)

**Prompt includes:**
- Full context chain, now ending with: ask output → user answer

**Expected agent structured output:**
```json
{
  "action": "backward",
  "summary": "Soft-deleted rows are currently included in the export. This contradicts the user's clarification. Sending back for a fix.",
  "issues": [
    "Export includes soft-deleted rows; must filter WHERE deleted_at IS NULL"
  ]
}
```

**Expected system behaviour:**
- Testing column's `BackwardTargetColumnId` = In Progress
- Task moves back to In Progress with the failure detail in context
- Context chain now has: spec, in-progress, code-review, testing×2 (ask + backward), user-answer

### Pass Criteria
- Task does not transition during `ask` — it stays in Testing
- The user answer appears in the context chain before the second Testing run
- The second Testing run's agent prompt contains both the question and the user's answer
- Task correctly routes to In Progress (not Code Review or Spec) on backward

---

## Scenario 4 — Multi-round Ask (Ask depth)

**Description:** Agent asks two separate questions before deciding. Tests that multiple ask rounds in one column work correctly.

### Setup
- Task in Testing column

### Step 4a — Testing ask round 1
```json
{ "action": "ask", "question": "Should soft-deleted rows be included?" }
```
User answers: `"No, exclude them."`

### Step 4b — Testing ask round 2 (same column, second run)
Agent has the first answer but has another ambiguity:
```json
{ "action": "ask", "question": "Should the CSV use UTC timestamps or the user's local timezone?" }
```
User answers: `"UTC."`

### Step 4c — Testing final run (same column, third run)
```json
{
  "action": "forward",
  "summary": "Confirmed: soft-deleted rows excluded, timestamps in UTC. All tests pass.",
  "testResults": "47 passed, 0 failed"
}
```

**Expected system behaviour:**
- Task moves to Done (after user approval, auto-forward=false)
- Context chain for Testing has 5 entries: ask×2, user-answer×2, forward

### Pass Criteria
- Each ask/answer pair appears in order in the context chain
- Third Testing run's prompt includes both Q&A pairs
- Task stays in Testing column throughout all three runs
- No transition happens until `forward` is produced

---

## Scenario 5 — Spec Step Produces Invalid Structured Output

**Description:** The agent fails to produce a valid structured output block. Tests error handling.

### Setup
- Task created: `"Refactor auth middleware"`
- Orchestrator runs In Specification agent

**Agent response:** A well-written spec in prose, but the `<<<STRUCTURED_OUTPUT>>>` block is missing (or malformed JSON).

**Expected system behaviour:**
- Orchestrator fails to parse structured output
- Task status → `error`
- Error message stored: "Agent did not produce a valid structured output block"
- Task card shows error badge + "Retry" button
- Task does NOT transition to any column

### On Retry
- Orchestrator re-runs the same column's agent
- Context chain gains a system note: "Previous run failed to produce structured output. You must end your response with a valid <<<STRUCTURED_OUTPUT>>> block."
- Agent re-runs

### Pass Criteria
- Task stays in In Specification on error (no phantom transition)
- Retry re-runs the same column (not the previous or next)
- After a successful retry, task continues normally

---

## Scenario 6 — Backward Transition Skips Multiple Columns

**Description:** A Testing failure should route to In Progress, not Code Review — even though Code Review is the immediate prior column. Verifies that `BackwardTargetColumnId` overrides positional logic.

### Setup
Pipeline: Spec → In Progress → Code Review → Testing → Done  
Testing column's `BackwardTargetColumnId` = In Progress (not Code Review)

### State
- Task has completed Spec, In Progress, Code Review
- Now in Testing

**Testing agent output:**
```json
{
  "action": "backward",
  "summary": "Integration tests fail. API returns 500 on empty dataset.",
  "issues": ["Unhandled exception in export route when result set is empty"]
}
```

**Expected system behaviour:**
- Task routes to **In Progress** (not Code Review)
- Code Review is skipped on the way back

### In Progress (rework)
Agent fixes the bug, produces `forward`.

**Expected system behaviour:**
- Task goes to Code Review (next column after In Progress)
- Code Review reviews again (brief re-check is fine)
- Code Review produces `forward` → Testing again

### Pass Criteria
- Testing backward → In Progress (not Code Review)
- After fix: In Progress → Code Review → Testing (full forward path resumes from In Progress's next column)
- Context chain shows the correct column sequence

---

## Scenario 7 — Parallel Tasks, No Cross-Contamination

**Description:** Two tasks run simultaneously on different branches. Verifies worktree isolation.

### Setup
- Task A: `"Add CSV export"` — branch `feature/csv-export`, worktree at `%TEMP%\autocoder\{taskA-id}\api`
- Task B: `"Fix login redirect bug"` — branch `fix/login-redirect`, worktree at `%TEMP%\autocoder\{taskB-id}\api`
- Both enter In Progress at the same time (concurrency limit ≥ 2)

**Expected system behaviour:**
- Two separate `claude` CLI processes spawn
- Task A agent only sees files in `{taskA-id}\api` worktree
- Task B agent only sees files in `{taskB-id}\api` worktree
- Changes committed by Task A do not appear in Task B's worktree
- Both tasks produce `forward` and move to Code Review independently

### Pass Criteria
- `git log` in Task A's worktree shows only Task A's commits
- `git log` in Task B's worktree shows only Task B's commits
- No merge conflicts or shared state between the two working trees
- Both tasks complete without affecting each other

---

## Scenario 8 — Agent Timeout

**Description:** The agent process takes longer than the column's configured timeout.

### Setup
- In Progress column has `TimeoutSeconds = 120`
- Agent spawned but does not complete within 120 seconds

**Expected system behaviour:**
- Orchestrator kills the `claude` CLI child process after timeout
- Task status → `error`
- Error message: "Agent timed out after 120 seconds"
- Partial streamed output (if any) is preserved in the task detail view
- No structured output is saved (the run did not complete)
- Task remains in In Progress column

### On Retry
- Fresh agent run for In Progress
- Context chain does NOT include the timed-out run's partial output (it was not a valid completed step)

### Pass Criteria
- Task does not advance on timeout
- Error is visible in UI within a few seconds of the timeout firing
- Retry is available and starts a clean run

---

## Scenario 9 — Context Chain Integrity Check

**Description:** Verifies that every agent always receives the complete and correct context chain.

### Setup
- Task has gone through: Spec → In Progress → Code Review (backward) → In Progress → Code Review (forward) → Testing

**When Testing agent runs, its prompt must contain (in order):**
1. Spec output (full agent text + structured data)
2. In Progress #1 output
3. Code Review #1 output (action=backward, issues listed)
4. In Progress #2 output (fix summary)
5. Code Review #2 output (action=forward)

### Pass Criteria
- All 5 prior step entries are present in the Testing agent's prompt
- Entries are in chronological order
- The Code Review backward entry clearly shows its `issues` list so Testing knows what was originally wrong
- No entries are duplicated or missing
