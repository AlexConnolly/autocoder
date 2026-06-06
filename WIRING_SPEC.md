# Autocoder — Wiring Specification

Defines the concrete contracts between every component. Read this before writing any integration code.

---

## Component Map

```
Frontend (React)
  │  HTTP REST          SignalR WS
  ▼                     ▼
Autocoder.Api (ASP.NET Core)
  ├── Controllers  ──► EF Core (SQLite)
  ├── OrchestratorHub (SignalR)
  ├── BackgroundOrchestratorService
  │     └── IOrchestrator (OrchestratorService)
  │           ├── IAgentRunner (ClaudeCliRunner)  ──► claude CLI process
  │           │     └── IHubContext ──────────────►  SignalR live output
  │           ├── IGitService (GitService)         ──► git process
  │           └── PromptBuilder
  └── EF Core (AutocoderDbContext)
```

---

## 1. Claude CLI Invocation

```
claude --print --output-format text --max-turns {N}
```

- Prompt is written to **stdin** (no shell-escaping issues, handles any length)
- **stdout** is read line by line; each line broadcast via SignalR `LiveOutput`
- **stderr** is captured but not streamed (used for error logging only)
- Process **working directory** = `task.WorktreePath` (first repo's worktree) if set; otherwise CWD
- Timeout enforced via `CancellationToken` linked to `column.TimeoutSeconds`
- On cancellation: process is killed; `AgentResult { TimedOut = true }` returned

---

## 2. Structured Output Parsing

Sentinel pattern extracted from the full stdout text:

```
<<<STRUCTURED_OUTPUT>>>
{ "action": "forward|backward|ask", "summary": "...", ... }
<<<END_STRUCTURED_OUTPUT>>>
```

- Everything between sentinels is parsed as JSON
- `action` maps to `TransitionAction` enum (case-insensitive)
- `question` extracted when action = Ask
- `branchName` extracted by `OrchestratorService` to trigger git worktree setup
- If no sentinel found or JSON invalid → `StructuredOutput = null` → orchestrator sets Error

---

## 3. Git Worktree Operations

Each call runs `git` as a child process. Repo must already exist locally.

**SetupWorktreeAsync** (called once, after spec step defines branchName):
```bash
git -C {repo.LocalPath} branch {branchName} {repo.DefaultBranch}   # create branch (no-op if exists)
git -C {repo.LocalPath} worktree add "{worktreeDir}" {branchName}    # create worktree
```
`worktreeDir` = `{WorktreeBaseDir}/{taskId}/{repoName}`  
Done for every linked repository. `task.WorktreePath` set to first repo's worktree.

**TeardownWorktreeAsync** (called when task reaches Done):
```bash
git -C {repo.LocalPath} worktree remove "{worktreeDir}" --force
```

---

## 4. SignalR Hub

**Hub URL:** `/hubs/orchestrator`  
**Group naming:** `board-{boardId}` (UUID string)

### Client → Server (hub methods)
| Method | Parameters | Effect |
|---|---|---|
| `JoinBoard` | `boardId: string` | Adds connection to group `board-{boardId}` |

### Server → Client (events)
| Event | Parameters | When |
|---|---|---|
| `TaskUpdated` | `task: WorkTask` | After any status/column change |
| `LiveOutput` | `taskId: string, line: string` | Each stdout line from claude CLI |
| `ContextEntryAdded` | `taskId: string, entry: ContextEntry` | After a context entry is saved |

`TaskUpdated` is broadcast by `BackgroundOrchestratorService` after each `ProcessTaskAsync` call completes.  
`LiveOutput` is broadcast by `ClaudeCliRunner` directly, on each line read.

---

## 5. API Routes

### Boards
| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/api/boards/{boardId}` | — | Board + columns + repos |

### Tasks
| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/api/boards/{boardId}/tasks` | — | WorkTask[] |
| POST | `/api/boards/{boardId}/tasks` | `{ title, description? }` | WorkTask |
| POST | `/api/tasks/{id}/answer` | `{ text: string }` | 204 |
| POST | `/api/tasks/{id}/retry` | — | 204 |

### Settings — Repositories
| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/api/boards/{boardId}/repositories` | — | BoardRepository[] |
| POST | `/api/boards/{boardId}/repositories` | `{ name, localPath, defaultBranch }` | BoardRepository |
| PUT | `/api/repositories/{id}` | `{ name, localPath, defaultBranch }` | BoardRepository |
| DELETE | `/api/repositories/{id}` | — | 204 |

### Settings — Columns
| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/api/boards/{boardId}/columns` | — | Column[] |
| PUT | `/api/columns/{id}` | `{ instructions?, autoForward, backwardTargetColumnId?, timeoutSeconds, maxAgentTurns }` | Column |
| PUT | `/api/boards/{boardId}/columns/reorder` | `{ ids: string[] }` | 204 |

### Settings — Board
| Method | Route | Body | Response |
|---|---|---|---|
| PUT | `/api/boards/{boardId}` | `{ name, globalInstructions? }` | Board |

---

## 6. Background Orchestrator Service

- Polls every **1 second**
- Queries DB for tasks where `Status = Waiting` AND current column `Type = Agent`
- Picks up at most `MaxConcurrency` tasks (default: 3, configurable)
- Uses `SemaphoreSlim` to enforce concurrency — does not block the poll loop
- Each task runs in its own DI scope (solves scoped-in-singleton problem)
- After `ProcessTaskAsync` completes, reads the updated task from DB and broadcasts `TaskUpdated`
- Errors are logged; the slot is released regardless

---

## 7. Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=autocoder.db"
  },
  "Claude": {
    "CliPath": "claude",
    "DefaultMaxTurns": 10
  },
  "Git": {
    "WorktreeBaseDir": "C:\\autocoder-worktrees"
  },
  "Orchestrator": {
    "MaxConcurrency": 3,
    "PollIntervalMs": 1000
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

---

## 8. AgentPrompt — new runtime fields

Three fields added to `AgentPrompt` for use by the runner (not part of the prompt content):

| Field | Type | Used by |
|---|---|---|
| `TaskId` | Guid | ClaudeCliRunner → SignalR `LiveOutput` event |
| `BoardId` | Guid | ClaudeCliRunner → SignalR group name |
| `WorktreePath` | string? | ClaudeCliRunner → process working directory |

`PromptBuilder` populates these from the `WorkTask`.

---

## 9. Frontend Integration

**SignalR client:** `@microsoft/signalr` npm package  
**Connection URL:** `http://localhost:5000/hubs/orchestrator` (proxied through Vite at `/hubs/orchestrator`)

`useSignalR(boardId, callbacks)` — establishes connection, joins board group, wires events.  
`useBoard(boardId)` — calls `useSignalR` internally; loads board + tasks from REST on mount; updates state on SignalR events.

**Env var:** `VITE_USE_MOCK=true` in `.env.development` → skip API calls, use local mock data. Allows frontend development without a running backend.
