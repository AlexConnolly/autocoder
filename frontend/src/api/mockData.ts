import { ago } from '../utils/formatting';
import type { Board, ContextEntry, WorkTask } from '../types';

const BOARD_ID = 'board-1';
export const COL_TODO = 'col-todo';
export const COL_SPEC = 'col-spec';
export const COL_PROGRESS = 'col-progress';
export const COL_REVIEW = 'col-review';
export const COL_TESTING = 'col-testing';
export const COL_DONE = 'col-done';

export const mockBoard: Board = {
  id: BOARD_ID,
  name: 'Main Project',
  globalInstructions:
    'Always follow existing code conventions. Write TypeScript. Use the project\'s existing test framework.',
  columns: [
    { id: COL_TODO,     boardId: BOARD_ID, name: 'To Do',            type: 'Input', position: 0, autoForward: false, timeoutSeconds: 0,   maxAgentTurns: 0  },
    { id: COL_SPEC,     boardId: BOARD_ID, name: 'In Specification', type: 'Agent', position: 1, autoForward: false, timeoutSeconds: 300, maxAgentTurns: 10,
      instructions: 'Produce a full technical specification. Define the branch name.',
      outputSchemaHint: 'branchName, acceptanceCriteria[], technicalApproach, filesToChange[]' },
    { id: COL_PROGRESS, boardId: BOARD_ID, name: 'In Progress',      type: 'Agent', position: 2, autoForward: true,  timeoutSeconds: 300, maxAgentTurns: 20,
      instructions: 'Implement the specification. Work in the provided worktrees. Commit your changes.' },
    { id: COL_REVIEW,   boardId: BOARD_ID, name: 'Code Review',      type: 'Agent', position: 3, autoForward: true,  timeoutSeconds: 300, maxAgentTurns: 10,
      backwardTargetColumnId: COL_PROGRESS,
      instructions: 'Review the diff against the spec\'s acceptance criteria. Check for correctness, style, and edge cases.' },
    { id: COL_TESTING,  boardId: BOARD_ID, name: 'Testing',          type: 'Agent', position: 4, autoForward: false, timeoutSeconds: 300, maxAgentTurns: 10,
      backwardTargetColumnId: COL_PROGRESS,
      instructions: 'Run the test suite in both repos. Verify all acceptance criteria are met.' },
    { id: COL_DONE,     boardId: BOARD_ID, name: 'Done',             type: 'Input', position: 5, autoForward: false, timeoutSeconds: 0,   maxAgentTurns: 0  },
  ],
  repositories: [
    { id: 'repo-1', boardId: BOARD_ID, name: 'api',      localPath: 'C:\\repos\\api',      defaultBranch: 'main' },
    { id: 'repo-2', boardId: BOARD_ID, name: 'frontend', localPath: 'C:\\repos\\frontend', defaultBranch: 'main' },
  ],
};

export const mockTasks: WorkTask[] = [
  {
    id: 'task-1', boardId: BOARD_ID, title: 'Add CSV export button',
    currentColumnId: COL_PROGRESS, status: 'Asking',
    branchName: 'feature/csv-export-button',
    pendingQuestion: 'Should soft-deleted rows be included in the CSV export? The spec says "all visible rows" but doesn\'t define whether soft-deleted rows count as visible.',
    createdAt: ago(120), updatedAt: ago(5),
  },
  {
    id: 'task-2', boardId: BOARD_ID, title: 'Auth middleware refactor',
    currentColumnId: COL_PROGRESS, status: 'Running',
    branchName: 'feature/auth-refactor',
    createdAt: ago(90), updatedAt: ago(2),
  },
  {
    id: 'task-3', boardId: BOARD_ID, title: 'Dark mode toggle',
    currentColumnId: COL_SPEC, status: 'Running',
    createdAt: ago(30), updatedAt: ago(1),
  },
  {
    id: 'task-4', boardId: BOARD_ID, title: 'Fix login redirect',
    currentColumnId: COL_REVIEW, status: 'Waiting',
    branchName: 'fix/login-redirect',
    createdAt: ago(200), updatedAt: ago(15),
  },
  {
    id: 'task-5', boardId: BOARD_ID, title: 'Update API rate limits',
    currentColumnId: COL_TESTING, status: 'Waiting',
    branchName: 'feature/rate-limits',
    createdAt: ago(300), updatedAt: ago(45),
  },
  {
    id: 'task-6', boardId: BOARD_ID, title: 'Password reset flow',
    currentColumnId: COL_SPEC, status: 'Error',
    errorMessage: 'Agent did not produce a valid structured output block.',
    createdAt: ago(60), updatedAt: ago(10),
  },
  {
    id: 'task-7', boardId: BOARD_ID, title: 'User avatar upload',
    currentColumnId: COL_DONE, status: 'Done',
    branchName: 'feature/avatar-upload',
    createdAt: ago(2880), updatedAt: ago(240),
  },
  {
    id: 'task-8', boardId: BOARD_ID, title: 'Mobile nav overflow fix',
    currentColumnId: COL_DONE, status: 'Done',
    branchName: 'fix/mobile-nav',
    createdAt: ago(4320), updatedAt: ago(1440),
  },
  {
    id: 'task-9', boardId: BOARD_ID, title: 'Email notifications',
    currentColumnId: COL_DONE, status: 'Done',
    branchName: 'feature/email-notifications',
    createdAt: ago(5760), updatedAt: ago(2880),
  },
];

export const mockLiveOutputs: Record<string, string> = {
  'task-2':
    'Reading src/middleware/auth.ts...\n' +
    'Found JWT validation logic at line 45\n' +
    'Analysing token expiry handling...\n' +
    'Refactoring to use consistent error types...\n' +
    'Writing updated middleware...\n' +
    'Running type-check...',
  'task-3':
    'Reading tailwind.config.ts...\n' +
    'Adding darkMode: \'class\' configuration\n' +
    'Writing ThemeProvider component...\n' +
    'Updating App.tsx to wrap with ThemeProvider...',
};

export const mockContextEntries: Record<string, ContextEntry[]> = {
  'task-7': [
    {
      id: 'ce-1', taskId: 'task-7', kind: 'AgentOutput',
      columnId: COL_SPEC, columnName: 'In Specification',
      content: 'Technical specification complete.\n\nBranch: feature/avatar-upload\nAcceptance Criteria:\n- User can upload a profile picture from the settings page\n- Image is resized to 256×256 on the server\n- Avatar appears in the nav bar within 2s of upload\n\nTechnical Approach: Add POST /api/profile/avatar endpoint. Store in /uploads/avatars/{userId}.webp. Return updated profile URL.',
      action: 'Forward',
      createdAt: ago(2880 + 60),
    },
    {
      id: 'ce-2', taskId: 'task-7', kind: 'AgentOutput',
      columnId: COL_PROGRESS, columnName: 'In Progress',
      content: 'Implementation complete.\n\nAdded avatar upload endpoint with Sharp for image resizing. Updated ProfileSettings component with file picker and preview. Committed as a1b2c3d.',
      action: 'Forward',
      createdAt: ago(2880 + 30),
    },
    {
      id: 'ce-3', taskId: 'task-7', kind: 'AgentOutput',
      columnId: COL_REVIEW, columnName: 'Code Review',
      content: 'Code review passed. No issues found.\n\n- Null check present for missing file\n- Image type validation in place\n- Error handling consistent with existing API patterns',
      action: 'Forward',
      createdAt: ago(2880),
    },
  ],
  'task-1': [
    {
      id: 'ce-4', taskId: 'task-1', kind: 'AgentOutput',
      columnId: COL_SPEC, columnName: 'In Specification',
      content: 'Technical specification complete.\n\nBranch: feature/csv-export-button\nAcceptance Criteria:\n- A "Download CSV" button appears above the data table\n- Clicking it downloads all visible (non-deleted) rows as a .csv file\n- CSV headers match the column display names',
      action: 'Forward',
      createdAt: ago(120 + 30),
    },
    {
      id: 'ce-5', taskId: 'task-1', kind: 'AgentOutput',
      columnId: COL_PROGRESS, columnName: 'In Progress',
      content: 'Running tests to verify export functionality...\nAll 47 tests pass.\nHowever, I noticed the export currently includes soft-deleted rows. The spec says "all visible rows" but does not define whether soft-deleted rows count as visible.',
      action: 'Ask',
      createdAt: ago(5),
    },
  ],
};
