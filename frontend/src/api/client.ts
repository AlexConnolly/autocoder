import type { Board, BoardRepository, Column, ColumnShellCommand, ContextEntry, WorkTask } from '../types';

const BASE = '/api';

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`);
  if (!res.ok) throw new Error(`GET ${path} failed: ${res.status}`);
  return res.json() as Promise<T>;
}

async function post<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(`POST ${path} failed: ${res.status}`);
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

async function put<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(`PUT ${path} failed: ${res.status}`);
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

async function del(path: string): Promise<void> {
  const res = await fetch(`${BASE}${path}`, { method: 'DELETE' });
  if (!res.ok) throw new Error(`DELETE ${path} failed: ${res.status}`);
}

// ── Board ─────────────────────────────────────────────────────────────────────

export const fetchBoard = (boardId: string) =>
  get<Board>(`/boards/${boardId}`);

export const fetchTasks = (boardId: string) =>
  get<WorkTask[]>(`/boards/${boardId}/tasks`);

export const fetchContextEntries = (taskId: string) =>
  get<ContextEntry[]>(`/tasks/${taskId}/context`);

// ── Tasks ─────────────────────────────────────────────────────────────────────

export const createTask = (boardId: string, title: string, description?: string) =>
  post<WorkTask>(`/boards/${boardId}/tasks`, { title, description });

export const submitAnswer = (taskId: string, text: string) =>
  post<void>(`/tasks/${taskId}/answer`, { text });

export const retryTask = (taskId: string) =>
  post<void>(`/tasks/${taskId}/retry`);

export const approveTask = (taskId: string) =>
  post<void>(`/tasks/${taskId}/approve`);

export const deleteTask = (taskId: string) =>
  del(`/tasks/${taskId}`);

// ── Settings ──────────────────────────────────────────────────────────────────

export const updateBoard = (boardId: string, name: string, globalInstructions?: string, maxInProgress?: number | null, cavemanMode?: boolean) =>
  put<Board>(`/boards/${boardId}`, { name, globalInstructions, maxInProgress, cavemanMode: cavemanMode ?? false });

export const createColumn = (boardId: string, name: string, type: 'Input' | 'Agent') =>
  post<Column>(`/boards/${boardId}/columns`, { name, type });

export const updateColumn = (columnId: string, patch: {
  name: string; instructions?: string; outputSchemaHint?: string;
  autoForward: boolean; agentEnabled: boolean;
  backwardTargetColumnId?: string; timeoutSeconds: number; maxAgentTurns: number;
}) => put<Column>(`/columns/${columnId}`, patch);

export const deleteColumn = (columnId: string) =>
  del(`/columns/${columnId}`);

export const reorderColumns = (boardId: string, ids: string[]) =>
  put<void>(`/boards/${boardId}/columns/reorder`, { ids });

export const addShellCommand = (columnId: string, command: string, workingDirectory?: string, phase: 'Pre' | 'Post' = 'Post') =>
  post<ColumnShellCommand>(`/columns/${columnId}/shell-commands`, { command, workingDirectory, phase });

export const updateShellCommand = (cmdId: string, command: string, workingDirectory?: string, phase: 'Pre' | 'Post' = 'Post') =>
  put<ColumnShellCommand>(`/shell-commands/${cmdId}`, { command, workingDirectory, phase });

export const deleteShellCommand = (cmdId: string) =>
  del(`/shell-commands/${cmdId}`);

export const fetchRepositories = (boardId: string) =>
  get<BoardRepository[]>(`/boards/${boardId}/repositories`);

export const addRepository = (boardId: string, name: string, localPath: string, defaultBranch = 'main') =>
  post<BoardRepository>(`/boards/${boardId}/repositories`, { name, localPath, defaultBranch });

export const updateRepository = (repoId: string, name: string, localPath: string, defaultBranch: string) =>
  put<BoardRepository>(`/repositories/${repoId}`, { name, localPath, defaultBranch });

export const deleteRepository = (repoId: string) =>
  del(`/repositories/${repoId}`);

export interface GitRepoResult { name: string; path: string; }

export const findGitRepos = (root?: string) =>
  get<GitRepoResult[]>(`/system/git-repos${root ? `?root=${encodeURIComponent(root)}` : ''}`);
