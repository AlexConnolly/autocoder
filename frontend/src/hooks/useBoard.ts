import { useCallback, useEffect, useRef, useState } from 'react';
import * as api from '../api/client';
import { mockBoard, mockContextEntries, mockLiveOutputs, mockTasks } from '../api/mockData';
import type { Board, ContextEntry, WorkTask } from '../types';
import { useNotifications } from './useNotifications';
import { useSignalR } from './useSignalR';

const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true';
const DEFAULT_BOARD_ID = '10000000-0000-0000-0000-000000000001';

const EMPTY_BOARD: import('../types').Board = {
  id: DEFAULT_BOARD_ID,
  name: '',
  columns: [],
  repositories: [],
};

export interface BoardState {
  board: Board;
  tasks: WorkTask[];
  liveOutputs: Record<string, string>;
  contextEntries: Record<string, ContextEntry[]>;
  selectedTaskId: string | null;
  isCreateModalOpen: boolean;
  isLoading: boolean;
  apiError: string | null;
  selectTask: (id: string | null) => void;
  openCreateModal: () => void;
  closeCreateModal: () => void;
  handleAnswer: (taskId: string, answer: string) => void;
  handleApprove: (taskId: string) => void;
  handleRetry: (taskId: string) => void;
  handleDeleteTask: (taskId: string) => void;
  handleCreateTask: (title: string, description?: string) => void;
}

export function useBoard(boardId = DEFAULT_BOARD_ID): BoardState {
  const { notify } = useNotifications();
  const taskStatusRef = useRef<Record<string, string>>({});
  const [board, setBoard]           = useState<Board>(USE_MOCK ? mockBoard : EMPTY_BOARD);
  const [tasks, setTasks]           = useState<WorkTask[]>(USE_MOCK ? mockTasks : []);
  const [liveOutputs, setLiveOutputs]   = useState<Record<string, string>>(USE_MOCK ? mockLiveOutputs : {});
  const [contextEntries, setContextEntries] = useState<Record<string, ContextEntry[]>>(USE_MOCK ? mockContextEntries : {});
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isLoading, setIsLoading]   = useState(!USE_MOCK);
  const [apiError, setApiError]     = useState<string | null>(null);

  // ── Load from API on mount ────────────────────────────────────────────────
  useEffect(() => {
    if (USE_MOCK) return;

    let cancelled = false;
    const load = async () => {
      try {
        const [boardData, tasksData] = await Promise.all([
          api.fetchBoard(boardId),
          api.fetchTasks(boardId),
        ]);
        if (cancelled) return;
        setBoard(boardData);
        setTasks(tasksData);
        tasksData.forEach(t => { taskStatusRef.current[t.id] = t.status; });
      } catch (err) {
        console.error('[useBoard] Failed to load board from API:', err);
        const port = import.meta.env.VITE_API_PORT || '7100';
        setApiError(`Cannot reach backend at http://localhost:${port}. Is dotnet run running?`);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    load();
    return () => { cancelled = true; };
  }, [boardId]);

  // ── SignalR integration ───────────────────────────────────────────────────
  useSignalR({
    boardId,
    enabled: !USE_MOCK,
    onTaskUpdated: (task) => {
      const prevStatus = taskStatusRef.current[task.id];
      const statusChanged = prevStatus !== undefined && prevStatus !== task.status;
      taskStatusRef.current[task.id] = task.status;

      if (statusChanged) {
        if (task.status === 'Done') {
          notify(`Task complete: ${task.title}`, {
            body: 'Your task has finished processing.',
            tag: `task-done-${task.id}`,
          });
        } else if (task.status === 'Asking') {
          notify(`Input needed: ${task.title}`, {
            body: task.pendingQuestion ?? 'A task is waiting for your response.',
            tag: `task-asking-${task.id}`,
          });
        }
      }

      setTasks(prev => {
        const exists = prev.some(t => t.id === task.id);
        return exists
          ? prev.map(t => t.id === task.id ? task : t)
          : [...prev, task];
      });
      if (task.status !== 'Running') {
        setLiveOutputs(prev => {
          if (!prev[task.id]) return prev;
          const next = { ...prev };
          delete next[task.id];
          return next;
        });
      }
    },
    onLiveOutput: (taskId, line) => {
      setLiveOutputs(prev => ({
        ...prev,
        [taskId]: (prev[taskId] ? prev[taskId] + '\n' : '') + line,
      }));
    },
    onContextEntryAdded: (taskId, entry) => {
      setContextEntries(prev => ({
        ...prev,
        [taskId]: [...(prev[taskId] ?? []), entry],
      }));
    },
    onTaskDeleted: (taskId) => {
      setTasks(prev => prev.filter(t => t.id !== taskId));
      setLiveOutputs(prev => { const next = { ...prev }; delete next[taskId]; return next; });
      setContextEntries(prev => { const next = { ...prev }; delete next[taskId]; return next; });
      setSelectedTaskId(prev => prev === taskId ? null : prev);
    },
  });

  // ── Load context entries when a task is selected ─────────────────────────
  useEffect(() => {
    if (USE_MOCK || !selectedTaskId) return;
    api.fetchContextEntries(selectedTaskId)
      .then(entries => setContextEntries(prev => ({ ...prev, [selectedTaskId]: entries })))
      .catch(console.error);
  }, [selectedTaskId]);

  // ── Action handlers ───────────────────────────────────────────────────────
  const handleAnswer = useCallback((taskId: string, answer: string) => {
    // Optimistic: mark as Waiting immediately
    setTasks(prev => prev.map(t =>
      t.id === taskId ? { ...t, status: 'Waiting', pendingQuestion: undefined, updatedAt: new Date().toISOString() } : t
    ));
    if (!USE_MOCK) {
      api.submitAnswer(taskId, answer).catch(console.error);
    }
  }, []);

  const handleApprove = useCallback((taskId: string) => {
    setTasks(prev => prev.map(t =>
      t.id === taskId ? { ...t, status: 'Waiting', updatedAt: new Date().toISOString() } : t
    ));
    if (!USE_MOCK) {
      api.approveTask(taskId).catch(console.error);
    }
  }, []);

  const handleRetry = useCallback((taskId: string) => {
    setTasks(prev => prev.map(t =>
      t.id === taskId ? { ...t, status: 'Waiting', errorMessage: undefined, updatedAt: new Date().toISOString() } : t
    ));
    if (!USE_MOCK) {
      api.retryTask(taskId).catch(console.error);
    }
  }, []);

  const handleDeleteTask = useCallback((taskId: string) => {
    setTasks(prev => prev.filter(t => t.id !== taskId));
    setLiveOutputs(prev => { const next = { ...prev }; delete next[taskId]; return next; });
    setContextEntries(prev => { const next = { ...prev }; delete next[taskId]; return next; });
    setSelectedTaskId(prev => prev === taskId ? null : prev);
    if (!USE_MOCK) {
      api.deleteTask(taskId).catch(console.error);
    }
  }, []);

  const handleCreateTask = useCallback((title: string, description?: string) => {
    setIsCreateModalOpen(false);

    if (USE_MOCK) {
      const newTask: WorkTask = {
        id: `task-${Date.now()}`,
        boardId,
        title,
        description,
        currentColumnId: mockBoard.columns.find(c => c.type === 'Agent')?.id ?? mockBoard.columns[0].id,
        status: 'Waiting',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };
      setTasks(prev => [newTask, ...prev]);
      return;
    }

    api.createTask(boardId, title, description)
      .then(task => setTasks(prev => [task, ...prev]))
      .catch(console.error);
  }, [boardId]);

  return {
    board,
    tasks,
    liveOutputs,
    contextEntries,
    selectedTaskId,
    isCreateModalOpen,
    isLoading,
    apiError,
    selectTask: setSelectedTaskId,
    openCreateModal: () => setIsCreateModalOpen(true),
    closeCreateModal: () => setIsCreateModalOpen(false),
    handleAnswer,
    handleApprove,
    handleRetry,
    handleDeleteTask,
    handleCreateTask,
  };
}
