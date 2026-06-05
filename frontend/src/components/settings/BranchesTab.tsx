import { useEffect, useState } from 'react';
import type { BranchInfo } from '../../types';
import * as api from '../../api/client';
import { mockTasks } from '../../api/mockData';
import { cn } from '../../utils/cn';

const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true';
const BOARD_ID = '10000000-0000-0000-0000-000000000001';

function statusColor(status?: string): string {
  switch (status) {
    case 'Done':    return 'text-green-500';
    case 'Running': return 'text-blue-500';
    case 'Error':   return 'text-red-400';
    case 'Asking':  return 'text-amber-500';
    default:        return 'text-zinc-500';
  }
}

function GitBranchIcon() {
  return (
    <svg className="w-3.5 h-3.5 flex-none" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
      <circle cx="4" cy="4" r="1.5" stroke="currentColor" strokeWidth="1.25" />
      <circle cx="4" cy="12" r="1.5" stroke="currentColor" strokeWidth="1.25" />
      <circle cx="12" cy="4" r="1.5" stroke="currentColor" strokeWidth="1.25" />
      <path d="M4 5.5V10.5" stroke="currentColor" strokeWidth="1.25" strokeLinecap="round" />
      <path d="M12 5.5C12 7.5 4 8 4 10.5" stroke="currentColor" strokeWidth="1.25" strokeLinecap="round" />
    </svg>
  );
}

export default function BranchesTab() {
  const [branches, setBranches] = useState<BranchInfo[]>([]);
  const [loading, setLoading] = useState(!USE_MOCK);
  const [confirming, setConfirming] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<string | null>(null);

  useEffect(() => {
    if (USE_MOCK) {
      const mockBranches: BranchInfo[] = mockTasks
        .filter(t => t.branchName)
        .map(t => ({
          name: t.branchName!,
          taskId: t.id,
          taskTitle: t.title,
          taskStatus: t.status,
        }));
      setBranches(mockBranches);
      return;
    }

    setLoading(true);
    api.fetchBranches(BOARD_ID)
      .then(setBranches)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const remove = async (name: string) => {
    setDeleting(name);
    try {
      if (!USE_MOCK) await api.deleteBranch(BOARD_ID, name);
      setBranches(prev => prev.filter(b => b.name !== name));
    } catch (err) {
      console.error(err);
    } finally {
      setDeleting(null);
      setConfirming(null);
    }
  };

  if (loading) {
    return <p className="text-sm text-zinc-500">Loading branches…</p>;
  }

  if (branches.length === 0) {
    return (
      <div className="max-w-xl">
        <p className="text-sm text-zinc-500">No autocoder branches found.</p>
        <p className="text-[12px] text-zinc-600 mt-1">
          Branches are created automatically when tasks run and are prefixed with <code className="font-mono">autocoder/</code>.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-2 max-w-xl">
      {branches.map(branch => (
        <div key={branch.name} className="bg-surface-raised border border-border rounded-lg">
          {confirming === branch.name ? (
            <div className="flex items-center gap-3 px-4 py-3">
              <span className="text-sm text-zinc-700 dark:text-zinc-300 flex-1">
                Delete <span className="font-mono text-[12px]">{branch.name}</span>?
              </span>
              <button
                onClick={() => remove(branch.name)}
                disabled={deleting === branch.name}
                className="text-xs px-2.5 py-1 bg-red-600 hover:bg-red-500 text-white rounded transition-colors disabled:opacity-50"
              >
                {deleting === branch.name ? 'Deleting…' : 'Delete'}
              </button>
              <button
                onClick={() => setConfirming(null)}
                className="text-xs text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300 transition-colors"
              >
                Cancel
              </button>
            </div>
          ) : (
            <div className="flex items-center gap-3 px-4 py-3">
              <span className={cn('flex-none', branch.taskStatus ? statusColor(branch.taskStatus) : 'text-zinc-400')}>
                <GitBranchIcon />
              </span>
              <div className="flex-1 min-w-0">
                <div className="text-[12px] font-mono text-zinc-700 dark:text-zinc-300 truncate">{branch.name}</div>
                {branch.taskTitle && (
                  <div className="text-[11px] text-zinc-500 truncate mt-0.5">
                    {branch.taskTitle}
                    {branch.taskStatus && (
                      <span className={cn('ml-1.5', statusColor(branch.taskStatus))}>
                        · {branch.taskStatus}
                      </span>
                    )}
                  </div>
                )}
                {!branch.taskTitle && (
                  <div className="text-[11px] text-zinc-600 mt-0.5">No linked task</div>
                )}
              </div>
              <button
                onClick={() => setConfirming(branch.name)}
                className="text-zinc-700 hover:text-red-400 text-xs px-2 py-1 rounded hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors flex-none"
              >
                ✕
              </button>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
