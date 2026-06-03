import { Link } from 'react-router-dom';
import type { Board } from '../../types';

interface Props {
  board: Board;
  onNewTask: () => void;
}

function SettingsIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path fillRule="evenodd" clipRule="evenodd" d="M6.5 1a.5.5 0 0 0-.491.401l-.24 1.197a5.02 5.02 0 0 0-.96.556l-1.14-.456a.5.5 0 0 0-.591.183l-1.5 2.598a.5.5 0 0 0 .101.624l.916.794a5.1 5.1 0 0 0 0 1.206l-.916.794a.5.5 0 0 0-.101.624l1.5 2.598a.5.5 0 0 0 .59.183l1.141-.456c.303.21.622.394.96.556l.24 1.197A.5.5 0 0 0 6.5 15h3a.5.5 0 0 0 .491-.401l.24-1.197c.338-.162.657-.346.96-.556l1.14.456a.5.5 0 0 0 .591-.183l1.5-2.598a.5.5 0 0 0-.101-.624l-.916-.794a5.1 5.1 0 0 0 0-1.206l.916-.794a.5.5 0 0 0 .101-.624l-1.5-2.598a.5.5 0 0 0-.59-.183l-1.141.456a5.02 5.02 0 0 0-.96-.556L9.991 1.4A.5.5 0 0 0 9.5 1h-3ZM8 10.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z" fill="currentColor" />
    </svg>
  );
}

function PlusIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 14 14" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M7 1v12M1 7h12" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" />
    </svg>
  );
}

function LogoMark() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="6" height="6" rx="1.5" fill="#6366f1" />
      <rect x="9" y="1" width="6" height="6" rx="1.5" fill="#6366f1" opacity="0.6" />
      <rect x="1" y="9" width="6" height="6" rx="1.5" fill="#6366f1" opacity="0.6" />
      <rect x="9" y="9" width="6" height="6" rx="1.5" fill="#6366f1" opacity="0.3" />
    </svg>
  );
}

export default function Header({ board, onNewTask }: Props) {
  return (
    <header className="h-12 flex items-center gap-4 px-4 border-b border-border bg-[var(--color-bg)] flex-none z-10 shadow-sm">
      {/* Brand */}
      <div className="flex items-center gap-2 select-none">
        <LogoMark />
        <span className="text-zinc-900 dark:text-zinc-100 text-sm font-semibold tracking-tight">autocoder</span>
      </div>

      <div className="w-px h-4 bg-zinc-300 dark:bg-zinc-800" />

      {/* Board name */}
      <span className="text-base font-medium text-zinc-800 dark:text-zinc-200">{board.name}</span>

      <div className="flex-1" />

      {/* Actions */}
      <button
        onClick={onNewTask}
        className="flex items-center gap-1.5 text-sm text-white bg-brand-500 hover:bg-brand-600 border-0 rounded px-2.5 py-1 transition-colors"
        title="New task (N)"
      >
        <PlusIcon />
        <span className="hidden sm:inline">New</span>
      </button>

      <Link
        to="/settings"
        className="p-1.5 text-zinc-500 dark:text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 transition-colors rounded"
        title="Settings"
      >
        <SettingsIcon />
      </Link>
    </header>
  );
}
