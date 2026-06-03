import * as signalR from '@microsoft/signalr';
import { useEffect, useRef } from 'react';
import type { ContextEntry, WorkTask } from '../types';

interface Options {
  boardId: string;
  enabled: boolean;
  onTaskUpdated: (task: WorkTask) => void;
  onLiveOutput: (taskId: string, line: string) => void;
  onContextEntryAdded: (taskId: string, entry: ContextEntry) => void;
  onTaskDeleted?: (taskId: string) => void;
}

export function useSignalR({ boardId, enabled, onTaskUpdated, onLiveOutput, onContextEntryAdded, onTaskDeleted }: Options) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    if (!enabled) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/orchestrator`)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('TaskUpdated', (task: WorkTask) => onTaskUpdated(task));
    connection.on('LiveOutput', (taskId: string, line: string) => onLiveOutput(taskId, line));
    connection.on('ContextEntryAdded', (taskId: string, entry: ContextEntry) => onContextEntryAdded(taskId, entry));
    connection.on('TaskDeleted', (taskId: string) => onTaskDeleted?.(taskId));

    connection
      .start()
      .then(() => {
        console.log('[signalr] connected');
        return connection.invoke('JoinBoard', boardId);
      })
      .catch(err => console.warn('[signalr] connection failed (backend not running?):', err));

    connectionRef.current = connection;

    return () => {
      connection.stop().catch(() => {});
      connectionRef.current = null;
    };
  }, [boardId, enabled]);
}
