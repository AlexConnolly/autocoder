# Autocoder — UI Specification

## Design Philosophy

**One guiding rule:** the interface should be calm when nothing needs the user, and impossible to ignore when something does.

- **Minimal chrome.** The pipeline structure, column names, and configuration are secondary. The work is primary.
- **State-driven hierarchy.** A task's visual weight is proportional to how urgently it needs attention. A waiting task is almost invisible. A task asking a question dominates the screen.
- **Progressive disclosure.** Every layer of the interface hides information by default and reveals it on demand. Never show what you don't need right now.
- **Mobile-native, not mobile-adapted.** The mobile view is designed first, not shrunk from desktop.
- **Dark by default.** The target user is a developer. A dark theme reduces fatigue and looks at home next to a terminal.

---

## Colour & Typography

### Palette (dark mode, default)

| Role | Value | Usage |
|---|---|---|
| Background | `#0f0f10` | Page background |
| Surface | `#18181b` | Cards, panels |
| Surface raised | `#1f1f23` | Modals, drawers |
| Border | `#2a2a30` | Subtle dividers |
| Text primary | `#f0f0f2` | Titles, body |
| Text secondary | `#7a7a8a` | Labels, metadata |
| Text muted | `#44444f` | Timestamps, inactive |
| Accent attention | `#f59e0b` | Asking state, needs action |
| Accent positive | `#22c55e` | Done, approved, forward |
| Accent error | `#ef4444` | Error state |
| Accent running | `#3b82f6` | Running indicator |
| Accent neutral | `#6366f1` | Branch names, links |

Light mode: same semantic roles, inverted — `#fafafa` background, `#18181b` text. Toggled via system preference; no manual toggle needed.

### Typography

| Element | Font | Size | Weight |
|---|---|---|---|
| Board name | System UI | 15px | 500 |
| Column header | System UI | 11px | 600, uppercase, tracked |
| Task title | System UI | 14px | 500 |
| Metadata, labels | System UI | 11px | 400 |
| Agent output | `ui-monospace, 'Cascadia Code', monospace` | 13px | 400 |
| Question text | System UI | 14px | 400, italic |

---

## Layout — Desktop

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  autocoder          Main Project        ⚡ 1 needs attention      + New  ⚙  │  ← Header (48px)
├────────────┬────────────┬────────────┬────────────┬────────────┬────────────┤
│ TO DO      │ IN SPEC    │ IN PROGRESS│ CODE REVIEW│ TESTING    │ DONE       │  ← Column headers
│ 0          │ 1          │ 2 ●        │ 1          │ 0          │ 4          │  ← counts + running dot
├────────────┼────────────┼────────────┼────────────┼────────────┼────────────┤
│            │            │ ╔══════════╗│            │            │            │
│            │ ┌──────────┐│ ║ ASKING ║│            │            │ ┌──────────┐
│            │ │CSV Export││ ║ Add CSV ║│            │            │ │Login fix │
│            │ │          ││ ║ Export  ║│            │            │ │          │
│            │ │ ● Running││ ║ ─────── ║│            │            │ │ ✓ Done   │
│            │ │ Spec...  ││ ║ Should  ║│            │            │ └──────────┘
│            │ │          ││ ║ deleted ║│            │            │            │
│            │ └──────────┘│ ║ rows be ║│            │            │ ┌──────────┐
│            │             │ ║ included║│            │            │ │Auth rewrite│
│            │             │ ║ in the  ║│            │            │ │          │
│            │             │ ║ export? ║│            │            │ │ ✓ Done   │
│            │             │ ║ ─────── ║│            │            │ └──────────┘
│            │             │ ║[_______]║│            │            │            │
│            │             │ ║ Submit  ║│            │            │            │
│            │             │ ╚══════════╝│            │            │            │
│            │             │            │            │            │            │
│            │             │ ┌──────────┐│            │            │            │
│            │             │ │Auth fix  ││            │            │            │
│            │             │ │ ● Running││            │            │            │
│            │             │ │ Writing..││            │            │            │
│            │             │ └──────────┘│            │            │            │
└────────────┴─────────────┴────────────┴────────────┴────────────┴────────────┘
```

**Column width:** 240px fixed. Horizontal scroll when columns overflow viewport. Each column scrolls independently.

**Column header:** sticky at top of its column. Shows name (uppercase, small) + task count. A blue dot `●` appears next to the count while any task in that column is running.

---

## Layout — Mobile

Mobile uses a **two-view model** accessible via a bottom navigation bar.

```
┌─────────────────────┐
│ autocoder  Main Proj│   ← Header (compact, 44px)
├─────────────────────┤
│                     │
│  ⚡ Needs Attention  │   ← Attention banner (only visible when tasks need action)
│  1 task waiting     │
│                     │
├─────────────────────┤
│                     │   ← Board view on mobile: columns shown as horizontal
│  IN PROGRESS    2 ● │     chip strip. Tap a chip to filter to that column.
│  [In Spec] [InProg] │
│  [Review] [Testing] │
│                     │
├─────────────────────┤
│ ╔═══════════════════╗ │
│ ║ ASKING            ║ │   ← Attention card: full width, elevated, colour-bordered
│ ║ Add CSV Export    ║ │
│ ║ ─────────────── ─ ║ │
│ ║ Should deleted    ║ │
│ ║ rows be included  ║ │
│ ║ in the export?    ║ │
│ ║ ──────────────────║ │
│ ║ [_______________] ║ │
│ ║        Submit  →  ║ │
│ ╚═══════════════════╝ │
│                     │
│ ┌─────────────────┐ │
│ │ Auth fix        │ │   ← Running card: compact
│ │ ● In Progress   │ │
│ └─────────────────┘ │
│                     │
├─────────────────────┤
│  ⚡ Focus  ⊞ Board  ⚙ │   ← Bottom nav (56px)
└─────────────────────┘
```

**Focus tab** (default on mobile): shows only tasks that need user action (Asking, PendingApproval, Error). If nothing needs attention, shows a calm empty state: "Nothing needs your attention right now."

**Board tab**: full board, columns displayed as a horizontal scrollable chip strip above a vertical card list. Tapping a chip filters to that column.

---

## Task Card States

Cards are the core of the interface. Each state has a distinct visual treatment. Cards are always **title-first** — the task name is never hidden.

### State 1 — Waiting (minimal)
```
┌──────────────────────────┐
│ CSV export button         │
│ In Specification  · 2m   │
└──────────────────────────┘
```
- No border accent
- Muted column label + timestamp
- Tapping opens the detail drawer

---

### State 2 — Running (live)
```
┌──────────────────────────┐
│ ● CSV export button       │  ← blue dot (animated pulse)
│ In Progress               │
│ ─────────────────────────│
│ Creating the endpoint...  │  ← last line of streamed output (truncated)
└──────────────────────────┘
```
- Left border: `2px solid #3b82f6` (blue)
- Output preview: single line, monospace, fades out at right edge
- Tapping expands to full streaming view

---

### State 3 — Asking (high priority)
```
╔══════════════════════════╗
║ ⚡ CSV export button      ║  ← amber accent, heavy border
║ Testing — needs your input║
║ ──────────────────────── ║
║ Should soft-deleted rows  ║
║ be included in the CSV    ║
║ export?                   ║
║ ──────────────────────── ║
║ [                       ] ║  ← text input, focused
║                  Submit → ║
╚══════════════════════════╝
```
- Full double border: `2px solid #f59e0b`
- Background slightly elevated: `#1f1f23`
- `⚡` icon prefix on title
- Question text in normal (non-monospace) body font
- Input + submit always visible — no tap required to reveal action
- Enter key submits

---

### State 4 — Pending Approval (action required)
```
╔══════════════════════════╗
║ ✓ CSV export button       ║  ← green accent
║ In Specification — review ║
║ ──────────────────────── ║
║ [  Approve  ] [  Reject  ]║  ← two buttons always visible
╚══════════════════════════╝
```
- Border: `2px solid #22c55e`
- Approve is primary (filled), Reject/Request Changes is ghost
- Tapping Approve immediately transitions; no confirmation dialog
- Tapping Reject opens a small inline text area for feedback, then Submit

---

### State 5 — Error
```
┌──────────────────────────┐
│ ✕ Auth refactor           │  ← red icon
│ In Specification  · error │
│ ─────────────────────────│
│ Agent did not produce a   │  ← error summary, one line
│ valid structured output.  │
│               [ Retry ]   │
└──────────────────────────┘
```
- Left border: `2px solid #ef4444`
- Error message truncated to two lines, tap to see full
- Retry button inline, no navigation required

---

### State 6 — Done (muted)
```
┌──────────────────────────┐
│ CSV export button     ✓   │  ← checkmark right-aligned
│ Done  · 3h ago           │
└──────────────────────────┘
```
- No border
- Text secondary colour throughout
- No interactive elements on the card itself
- Tap opens read-only history

---

## Task Detail Drawer

Slides in from the right on desktop (480px wide), slides up from the bottom on mobile (full screen). Does not replace the board — board is still visible and live-updating behind the overlay.

```
┌─────────────────────────────────────────────┐
│ ← Back    CSV export button         ● Running│
│            In Progress · feature/csv-export  │
├─────────────────────────────────────────────┤
│                                             │
│  LIVE OUTPUT                                │  ← only visible when running
│  ┌─────────────────────────────────────────┐│
│  │ $ Reading export.ts...                  ││  ← monospace terminal panel
│  │ Found existing endpoint skeleton        ││     auto-scrolls to bottom
│  │ Writing null check for empty result set ││
│  │ ▌                                       ││  ← blinking cursor
│  └─────────────────────────────────────────┘│
│                                             │
│  HISTORY                                    │
│  ▼ In Specification  [Forward]  · 1h ago    │  ← collapsible step
│  │ Technical spec for CSV export...  ↓      │
│  │ Branch: feature/csv-export-button        │
│  ▶ In Progress (run 1)  [Forward]  · 45m    │  ← collapsed
│                                             │
│  DETAILS                                    │
│  Branch    feature/csv-export-button        │
│  Board     Main Project                     │
│  Created   Today at 10:34                   │
│                                             │
└─────────────────────────────────────────────┘
```

**History section:** each step is a collapsible row. Collapsed: column name + action badge + timestamp. Expanded: full agent output in a monospace scrollable block, plus structured data (key-value pairs, not raw JSON).

**Action badge colours:**
- `[Forward]` → green
- `[Backward]` → amber  
- `[Ask]` → amber with ⚡
- `[Error]` → red

**Live output panel:** visible only while the task is running. Collapses to a "View output" toggle once the agent finishes. Monospace, dark terminal style (`#0a0a0b` background), text in `#d4d4d8`. The `<<<STRUCTURED_OUTPUT>>>` block is highlighted in green when it appears.

---

## Attention Banner

Displayed at the top of the board (below the header) whenever any task is in `Asking` or `PendingApproval` state. Dismisses automatically when all attention items are resolved.

```
┌─────────────────────────────────────────────────────────────────┐
│ ⚡  2 tasks need your input  ·  CSV Export (question)  Auth fix (approve) │
└─────────────────────────────────────────────────────────────────┘
```

- Amber background: `#451a03` (dark amber tint)
- Task names are links that open the relevant card's detail drawer
- On mobile: taps navigate directly to the Focus tab

---

## Create Task

Triggered by the `+` button in the header or via keyboard shortcut `N`.

```
┌──────────────────────────────────┐
│ New task                       ✕ │
├──────────────────────────────────┤
│                                  │
│  What needs to be done?          │
│  ┌──────────────────────────┐    │
│  │ Add CSV download button  │    │  ← single text input, auto-focused
│  └──────────────────────────┘    │
│                                  │
│  ┌────────────────────────────┐  │
│  │ More detail (optional)  ↓  │  │  ← collapsed textarea
│  └────────────────────────────┘  │
│                                  │
│                    [ Create  → ] │
└──────────────────────────────────┘
```

- Modal overlay, appears centered on desktop, bottom sheet on mobile
- Single input for title; Enter submits immediately
- "More detail" expands a description textarea
- No board/column selection — always goes to the first agent column
- On submit: modal closes, card appears in first agent column with `Waiting` status, agent picks it up within seconds

---

## Settings

Accessed via `⚙` in the header. Full-page view (no overlay).

**Three-tab layout:**

```
[ Repositories ]  [ Board ]  [ Columns ]
```

### Repositories tab
```
Repositories
────────────────────────────────────────

api               C:\repos\api       ✎  ✕
frontend          C:\repos\frontend  ✎  ✕

+ Add repository
```
Simple list, inline edit/delete. Adding opens a two-field form: display name + local path. Path validated as a git repo on save (green tick or red error shown inline).

### Board tab
```
Board name        [Main Project              ]
Global instructions
┌──────────────────────────────────────────┐
│ Always follow existing code conventions. │
│ Write TypeScript...                      │
└──────────────────────────────────────────┘

Claude CLI path   [claude                    ]

                                    [ Save ]
```

### Columns tab
Drag-to-reorder list. Each row expands to edit:

```
≡  To Do           input                    ▼
≡  In Specification  agent                  ▼
   ┌─────────────────────────────────────────┐
   │ Instructions                            │
   │ ┌─────────────────────────────────────┐ │
   │ │ Produce a full technical spec...    │ │
   │ └─────────────────────────────────────┘ │
   │ Auto-forward after agent finishes  [ ] │ │  ← toggle
   │ Backward target column  [In Progress ▼] │  ← dropdown of other columns
   │ Timeout (seconds)       [300          ] │
   │ Max agent turns         [10           ] │
   └─────────────────────────────────────────┘
≡  In Progress     agent                    ▼
≡  Code Review     agent                    ▼
≡  Testing         agent                    ▼
≡  Done            input                    ▼

+ Add column
```

---

## Empty States

| Situation | Message |
|---|---|
| Board has no tasks | "Add your first task with +" (centered, muted) |
| Column is empty | Nothing — empty columns show no text, just whitespace |
| Focus tab, nothing needs attention | "All clear. The agents are working." with a subtle animated dot |
| Done column, no tasks | Nothing — Done being empty is the normal starting state |

---

## Keyboard Shortcuts (desktop)

| Key | Action |
|---|---|
| `N` | New task |
| `Escape` | Close drawer / modal |
| `Enter` | Submit answer / approve (when input is focused) |
| `←` `→` | Navigate between tasks in the detail drawer |
| `?` | Show shortcuts help |

---

## Information Hierarchy Summary

The principle applied consistently across every view:

| Priority | Information | How surfaced |
|---|---|---|
| 1 — Urgent | Tasks asking a question or awaiting approval | Full card with input/button visible without any tap; amber or green border; attention banner |
| 2 — Active | Running agents | Blue left border; live output preview on card; blue column dot |
| 3 — Current | Task title and which stage it's in | Always visible on card, no interaction needed |
| 4 — Context | Agent output, context chain history | Behind one tap (detail drawer), collapsed by default |
| 5 — Structural | Column names, board config, repos | Column headers in small caps; settings page only |
| 6 — Completed | Done tasks | Muted, no border, greyed text, hidden context chain |
