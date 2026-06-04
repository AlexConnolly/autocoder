# Autocoder

A Kanban board that codes for you.

---

Autocoder is a locally-hosted AI agent orchestration platform built around a Kanban board. You write a task in plain English, drop it in the backlog, and Autocoder's pipeline of Claude-powered agents carries it through Spec, Implement, Review, Test, and Done — automatically.

Each pipeline stage is a board column. When a card advances, an agent spins up, reads every prior stage's output for full context, and either moves the card forward, flags it for rework, or pauses to ask you a clarifying question. You stay in the loop without doing the work.

Everything runs locally. No data leaves your machine except the prompts sent to Claude. Tasks run in parallel, each in an isolated git worktree so branches never collide. The UI is real-time and mobile-friendly.

```
Backlog → Spec → Implement → Review → Test → Done
              ↑_______________↓  (rework loop)
```

---

## Prerequisites

| Requirement | Minimum version | Notes |
|---|---|---|
| .NET SDK | 7.0 | [Download](https://dotnet.microsoft.com/download/dotnet/7.0) |
| Node.js | 18.x | LTS recommended |
| npm | 9.x | Bundled with Node 18+ |
| Claude CLI | latest | Must be on `PATH` as `claude` |
| Git | 2.30+ | Required for worktree support |

**Claude CLI authentication is required.** Before starting Autocoder, run:

```bash
claude auth login
```

Agents will silently fail to launch if `claude` is not authenticated. This is the most common setup stumbling block.

---

## Setup

### Backend

```bash
cd src/Autocoder.Api
dotnet run
```

The API listens on `http://localhost:7100`. On first run it creates `autocoder.db` (SQLite) in the same directory. Always run `dotnet run` from `src/Autocoder.Api/` so the database file lands in the right place.

### Frontend

Open a second terminal:

```bash
cd frontend
npm install
npm run dev
```

Vite starts on `http://localhost:5173` and proxies API requests to `:7100` automatically — no manual configuration needed.

Then open `http://localhost:5173`.

> Two terminals are required: one for the backend, one for the frontend. There is no single-command launcher yet.

> If either port is already in use the app will fail silently. Free port `7100` and `5173` before starting.

---

## Running Tests

```bash
cd tests/Autocoder.FlowTests
dotnet test
```

The test suite has 45+ end-to-end flow tests covering the full pipeline, including rework loops, agent questions, timeouts, and parallel task isolation.

---

## Configuration

Key values are in `src/Autocoder.Api/appsettings.json`:

| Key | Default | Description |
|---|---|---|
| `Claude.CliPath` | `"claude"` | Path to the `claude` binary if not on `PATH` |
| `Git.WorktreeBaseDir` | `"C:\\autocoder-worktrees"` | Where per-task git worktrees are stored (Windows default; update for macOS/Linux) |
| `Orchestrator.MaxConcurrency` | `3` | Maximum number of tasks running in parallel |

---

## Project Structure

```
src/Autocoder.Api/          ASP.NET Core API, SignalR hub
src/Autocoder.Core/         Models, enums, interfaces
src/Autocoder.Orchestrator/ Claude agent runner, orchestration logic
frontend/                   React + TypeScript + Tailwind CSS UI
tests/Autocoder.FlowTests/  End-to-end xUnit tests
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core (.NET 7), C# |
| Real-time | SignalR |
| Frontend | React 18, TypeScript, Tailwind CSS, Vite 6 |
| Persistence | SQLite via EF Core |
| AI Execution | Claude CLI (spawned as child processes) |
| VCS | Git + Git Worktrees |
