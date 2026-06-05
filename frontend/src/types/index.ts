export type WorkTaskStatus = 'Waiting' | 'Running' | 'PendingApproval' | 'Asking' | 'Done' | 'Error';
export type ColumnType = 'Input' | 'Agent';
export type TransitionAction = 'Forward' | 'Backward' | 'Ask';
export type ContextEntryKind = 'AgentOutput' | 'UserAnswer' | 'SystemNote' | 'ShellOutput';

export interface ColumnShellCommand {
  id: string;
  columnId: string;
  command: string;
  workingDirectory?: string;
  position: number;
}

export interface WorkTask {
  id: string;
  boardId: string;
  title: string;
  description?: string;
  currentColumnId: string;
  status: WorkTaskStatus;
  branchName?: string;
  worktreePath?: string;
  pendingQuestion?: string;
  errorMessage?: string;
  createdAt: string;
  updatedAt: string;
}

export interface Column {
  id: string;
  boardId: string;
  name: string;
  type: ColumnType;
  position: number;
  instructions?: string;
  outputSchemaHint?: string;
  backwardTargetColumnId?: string;
  autoForward: boolean;
  agentEnabled: boolean;
  timeoutSeconds: number;
  maxAgentTurns: number;
  shellCommands?: ColumnShellCommand[];
}

export interface Board {
  id: string;
  name: string;
  globalInstructions?: string;
  maxInProgress?: number;
  cavemanMode: boolean;
  columns: Column[];
  repositories: BoardRepository[];
}

export interface BoardRepository {
  id: string;
  boardId: string;
  name: string;
  localPath: string;
  defaultBranch: string;
}

export interface ContextEntry {
  id: string;
  taskId: string;
  kind: ContextEntryKind;
  columnId?: string;
  columnName?: string;
  content: string;
  structuredData?: string;
  action?: TransitionAction;
  createdAt: string;
}
