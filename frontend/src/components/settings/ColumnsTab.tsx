import { useEffect, useState } from 'react';
import type { Column } from '../../types';
import { mockBoard } from '../../api/mockData';
import * as api from '../../api/client';
import { cn } from '../../utils/cn';

const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true';
const BOARD_ID = '10000000-0000-0000-0000-000000000001';

export default function ColumnsTab() {
  const [columns, setColumns] = useState<Column[]>(
    USE_MOCK ? [...mockBoard.columns].sort((a, b) => a.position - b.position) : []
  );
  const [expanded, setExpanded] = useState<string | null>(null);
  const [saving, setSaving] = useState<string | null>(null);

  useEffect(() => {
    if (USE_MOCK) return;
    api.fetchBoard(BOARD_ID)
      .then(b => setColumns([...b.columns].sort((a, b) => a.position - b.position)))
      .catch(console.error);
  }, []);

  const move = async (id: string, dir: -1 | 1) => {
    const sorted = [...columns].sort((a, b) => a.position - b.position);
    const idx = sorted.findIndex(c => c.id === id);
    const newIdx = idx + dir;
    if (newIdx < 0 || newIdx >= sorted.length) return;
    const next = sorted[newIdx];
    const updated = columns.map(c => {
      if (c.id === id) return { ...c, position: next.position };
      if (c.id === next.id) return { ...c, position: sorted[idx].position };
      return c;
    });
    setColumns(updated);
    if (!USE_MOCK) {
      const newOrder = [...updated].sort((a, b) => a.position - b.position).map(c => c.id);
      await api.reorderColumns(BOARD_ID, newOrder).catch(console.error);
    }
  };

  const saveColumn = async (col: Column) => {
    setSaving(col.id);
    try {
      if (!USE_MOCK) {
        await api.updateColumn(col.id, {
          instructions: col.instructions ?? undefined,
          outputSchemaHint: col.outputSchemaHint ?? undefined,
          autoForward: col.autoForward,
          backwardTargetColumnId: col.backwardTargetColumnId ?? undefined,
          timeoutSeconds: col.timeoutSeconds,
          maxAgentTurns: col.maxAgentTurns,
        });
      }
    } catch (err) {
      console.error(err);
    } finally {
      setSaving(null);
    }
  };

  const update = (id: string, patch: Partial<Column>) => {
    setColumns(prev => prev.map(c => c.id === id ? { ...c, ...patch } : c));
  };

  const agentColumns = columns.filter(c => c.type === 'Agent');
  const sorted = [...columns].sort((a, b) => a.position - b.position);

  return (
    <div className="space-y-2 max-w-2xl">
      {sorted.map((col, idx) => (
        <div key={col.id} className="bg-[#1f1f23] border border-[#2a2a30] rounded-lg overflow-hidden">
          <div className="flex items-center gap-2 px-3 py-2.5">
            <div className="flex flex-col gap-0.5 flex-none">
              <button disabled={idx === 0} onClick={() => move(col.id, -1)} className="text-zinc-600 hover:text-zinc-300 disabled:opacity-20 leading-none text-xs">▲</button>
              <button disabled={idx === sorted.length - 1} onClick={() => move(col.id, 1)} className="text-zinc-600 hover:text-zinc-300 disabled:opacity-20 leading-none text-xs">▼</button>
            </div>
            <span className="flex-1 text-sm text-zinc-200">{col.name}</span>
            <span className={cn(
              'text-[10px] px-1.5 py-0.5 rounded font-medium',
              col.type === 'Agent' ? 'bg-indigo-500/15 text-indigo-400' : 'bg-zinc-700/50 text-zinc-500'
            )}>
              {col.type.toLowerCase()}
            </span>
            {col.type === 'Agent' && (
              <button
                onClick={() => setExpanded(expanded === col.id ? null : col.id)}
                className="text-zinc-600 hover:text-zinc-300 text-xs px-2 py-1 rounded hover:bg-zinc-800 transition-colors ml-1"
              >
                {expanded === col.id ? '▲' : '▼'}
              </button>
            )}
          </div>

          {expanded === col.id && col.type === 'Agent' && (
            <div className="border-t border-[#2a2a30] px-4 py-4 space-y-4">
              <div>
                <Label>Instructions</Label>
                <textarea
                  value={col.instructions ?? ''}
                  onChange={e => update(col.id, { instructions: e.target.value })}
                  rows={3}
                  className={`${textareaCls} mt-1`}
                  placeholder="Instructions injected into every agent prompt for this column…"
                />
              </div>

              <div className="flex items-center justify-between">
                <div>
                  <Label>Auto-forward after agent finishes</Label>
                  <p className="text-[11px] text-zinc-600 mt-0.5">Skip human approval when agent produces "forward".</p>
                </div>
                <Toggle value={col.autoForward} onChange={v => update(col.id, { autoForward: v })} />
              </div>

              <div>
                <Label>Backward target column</Label>
                <select
                  value={col.backwardTargetColumnId ?? ''}
                  onChange={e => update(col.id, { backwardTargetColumnId: e.target.value || undefined })}
                  className={`${selectCls} mt-1`}
                >
                  <option value="">Previous agent column (default)</option>
                  {agentColumns.filter(c => c.id !== col.id).map(c => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label>Timeout (seconds)</Label>
                  <input
                    type="number"
                    value={col.timeoutSeconds}
                    onChange={e => update(col.id, { timeoutSeconds: parseInt(e.target.value) || 0 })}
                    className={`${inputCls} mt-1`}
                    min={0}
                  />
                </div>
                <div>
                  <Label>Max agent turns</Label>
                  <input
                    type="number"
                    value={col.maxAgentTurns}
                    onChange={e => update(col.id, { maxAgentTurns: parseInt(e.target.value) || 0 })}
                    className={`${inputCls} mt-1`}
                    min={1}
                  />
                </div>
              </div>

              <div className="pt-1">
                <button
                  onClick={() => saveColumn(col)}
                  disabled={saving === col.id}
                  className="px-3 py-1.5 bg-zinc-100 text-zinc-900 text-xs font-semibold rounded hover:bg-white transition-colors disabled:opacity-50"
                >
                  {saving === col.id ? 'Saving…' : 'Save'}
                </button>
              </div>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function Label({ children }: { children: React.ReactNode }) {
  return <div className="text-[11px] text-zinc-500">{children}</div>;
}

function Toggle({ value, onChange }: { value: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      onClick={() => onChange(!value)}
      className={cn(
        'relative w-9 h-5 rounded-full transition-colors',
        value ? 'bg-indigo-500' : 'bg-zinc-700'
      )}
    >
      <span className={cn(
        'absolute top-0.5 w-4 h-4 bg-white rounded-full shadow transition-transform',
        value ? 'translate-x-4' : 'translate-x-0.5'
      )} />
    </button>
  );
}

const inputCls = 'w-full bg-[#0f0f10] border border-zinc-700 rounded px-2.5 py-1.5 text-sm text-zinc-100 focus:outline-none focus:border-zinc-500 transition-colors';
const textareaCls = 'w-full bg-[#0f0f10] border border-zinc-700 rounded px-2.5 py-1.5 text-sm text-zinc-100 placeholder-zinc-600 focus:outline-none focus:border-zinc-500 resize-none transition-colors';
const selectCls = 'w-full bg-[#0f0f10] border border-zinc-700 rounded px-2.5 py-1.5 text-sm text-zinc-300 focus:outline-none focus:border-zinc-500 transition-colors';
