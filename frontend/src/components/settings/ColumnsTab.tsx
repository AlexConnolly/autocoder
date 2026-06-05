import { useEffect, useRef, useState } from 'react';
import type { Column, ColumnShellCommand, ColumnType, ShellCommandPhase } from '../../types';
import * as api from '../../api/client';
import { cn } from '../../utils/cn';

const BOARD_ID = '10000000-0000-0000-0000-000000000001';

type ColState = Column & { shellCommands: ColumnShellCommand[] };

export default function ColumnsTab() {
  const [cols, setCols] = useState<ColState[]>([]);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [saving, setSaving] = useState<string | null>(null);
  const [addForm, setAddForm] = useState(false);
  const [newName, setNewName] = useState('');
  const [newType, setNewType] = useState<ColumnType>('Agent');

  useEffect(() => {
    api.fetchBoard(BOARD_ID)
      .then(b => setCols(
        [...b.columns]
          .sort((a, b) => a.position - b.position)
          .map(c => ({ ...c, agentEnabled: c.agentEnabled ?? true, shellCommands: c.shellCommands ?? [] }))
      ))
      .catch(console.error);
  }, []);

  const sorted = [...cols].sort((a, b) => a.position - b.position);
  const agentCols = sorted.filter(c => c.type === 'Agent');

  // ── Column operations ──────────────────────────────────────────────────────

  const move = async (id: string, dir: -1 | 1) => {
    const idx = sorted.findIndex(c => c.id === id);
    const newIdx = idx + dir;
    if (newIdx < 0 || newIdx >= sorted.length) return;
    const other = sorted[newIdx];
    const updated = cols.map(c => {
      if (c.id === id) return { ...c, position: other.position };
      if (c.id === other.id) return { ...c, position: sorted[idx].position };
      return c;
    });
    setCols(updated);
    const newOrder = [...updated].sort((a, b) => a.position - b.position).map(c => c.id);
    await api.reorderColumns(BOARD_ID, newOrder).catch(console.error);
  };

  const patch = (id: string, p: Partial<ColState>) =>
    setCols(prev => prev.map(c => c.id === id ? { ...c, ...p } : c));

  const saveColumn = async (col: ColState) => {
    setSaving(col.id);
    try {
      await api.updateColumn(col.id, {
        name: col.name,
        instructions: col.instructions,
        outputSchemaHint: col.outputSchemaHint,
        autoForward: col.autoForward,
        agentEnabled: col.agentEnabled,
        backwardTargetColumnId: col.backwardTargetColumnId,
        timeoutSeconds: col.timeoutSeconds,
        maxAgentTurns: col.maxAgentTurns,
      });
    } catch (err) {
      console.error(err);
    } finally {
      setSaving(null);
    }
  };

  const deleteColumn = async (id: string) => {
    await api.deleteColumn(id).catch(console.error);
    setCols(prev => prev.filter(c => c.id !== id));
    if (expanded === id) setExpanded(null);
  };

  const createColumn = async () => {
    if (!newName.trim()) return;
    try {
      const col = await api.createColumn(BOARD_ID, newName.trim(), newType);
      setCols(prev => [...prev, { ...col, agentEnabled: col.agentEnabled ?? true, shellCommands: [] }]);
      setNewName('');
      setAddForm(false);
    } catch (err) {
      console.error(err);
    }
  };

  // ── Shell command operations ───────────────────────────────────────────────

  const addCmd = async (columnId: string, command: string, workingDir: string, phase: ShellCommandPhase) => {
    try {
      const cmd = await api.addShellCommand(columnId, command, workingDir || undefined, phase);
      patch(columnId, {
        shellCommands: [...(cols.find(c => c.id === columnId)?.shellCommands ?? []), cmd]
      });
    } catch (err) {
      console.error(err);
    }
  };

  const saveCmd = async (columnId: string, cmdId: string, command: string, workingDir: string, phase: ShellCommandPhase) => {
    if (!command.trim()) return;
    try {
      const updated = await api.updateShellCommand(cmdId, command, workingDir || undefined, phase);
      patch(columnId, {
        shellCommands: cols.find(c => c.id === columnId)?.shellCommands
          .map(s => s.id === cmdId ? updated : s) ?? []
      });
    } catch (err) {
      console.error(err);
    }
  };

  const deleteCmd = async (columnId: string, cmdId: string) => {
    await api.deleteShellCommand(cmdId).catch(console.error);
    patch(columnId, {
      shellCommands: cols.find(c => c.id === columnId)?.shellCommands.filter(s => s.id !== cmdId) ?? []
    });
  };

  return (
    <div className="space-y-2 max-w-2xl">
      {sorted.map((col, idx) => (
        <div key={col.id} className="bg-surface-raised border border-border rounded-lg overflow-hidden">
          {/* Header row */}
          <div className="flex items-center gap-2 px-3 py-2.5">
            <div className="flex flex-col gap-0.5 flex-none">
              <button disabled={idx === 0} onClick={() => move(col.id, -1)}
                className="text-zinc-600 hover:text-zinc-300 disabled:opacity-20 leading-none text-xs">▲</button>
              <button disabled={idx === sorted.length - 1} onClick={() => move(col.id, 1)}
                className="text-zinc-600 hover:text-zinc-300 disabled:opacity-20 leading-none text-xs">▼</button>
            </div>
            <span className="flex-1 text-sm text-zinc-800 dark:text-zinc-200 truncate">{col.name}</span>
            <span className={cn(
              'text-[10px] px-1.5 py-0.5 rounded font-medium flex-none',
              col.type === 'Agent'
                ? 'bg-indigo-500/15 text-indigo-400'
                : 'bg-zinc-200/80 dark:bg-zinc-700/50 text-zinc-500'
            )}>
              {col.type.toLowerCase()}
            </span>
            <button
              onClick={() => setExpanded(expanded === col.id ? null : col.id)}
              className="text-zinc-600 hover:text-zinc-300 text-xs px-2 py-1 rounded hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
            >
              {expanded === col.id ? '▲' : '▼'}
            </button>
            <button
              onClick={() => deleteColumn(col.id)}
              className="text-zinc-600 hover:text-red-400 text-xs px-1.5 py-1 rounded hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
              title="Delete column"
            >
              ✕
            </button>
          </div>

          {/* Expand panel */}
          {expanded === col.id && (
            <div className="border-t border-border px-4 py-4 space-y-4">
              {/* Name */}
              <div>
                <Label>Name</Label>
                <input
                  type="text"
                  value={col.name}
                  onChange={e => patch(col.id, { name: e.target.value })}
                  className={`${inputCls} mt-1`}
                />
              </div>

              {col.type === 'Agent' && (<>
                {/* Instructions */}
                <div>
                  <Label>Instructions</Label>
                  <textarea
                    value={col.instructions ?? ''}
                    onChange={e => patch(col.id, { instructions: e.target.value })}
                    rows={3}
                    className={`${textareaCls} mt-1`}
                    placeholder="Instructions injected into every agent prompt for this column…"
                  />
                </div>

                {/* Toggles */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="flex items-center justify-between">
                    <div>
                      <Label>Auto-forward</Label>
                      <p className="text-[11px] text-zinc-500 mt-0.5">Skip approval when agent forwards.</p>
                    </div>
                    <Toggle value={col.autoForward} onChange={v => patch(col.id, { autoForward: v })} />
                  </div>
                  <div className="flex items-center justify-between">
                    <div>
                      <Label>Agent enabled</Label>
                      <p className="text-[11px] text-zinc-500 mt-0.5">Disable to run shell commands only.</p>
                    </div>
                    <Toggle value={col.agentEnabled} onChange={v => patch(col.id, { agentEnabled: v })} />
                  </div>
                </div>

                {/* Backward target */}
                <div>
                  <Label>Backward target column</Label>
                  <select
                    value={col.backwardTargetColumnId ?? ''}
                    onChange={e => patch(col.id, { backwardTargetColumnId: e.target.value || undefined })}
                    className={`${selectCls} mt-1`}
                  >
                    <option value="">Previous agent column (default)</option>
                    {agentCols.filter(c => c.id !== col.id).map(c => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>

                {/* Timeout / MaxTurns */}
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label>Timeout (seconds)</Label>
                    <input type="number" value={col.timeoutSeconds} min={0}
                      onChange={e => patch(col.id, { timeoutSeconds: parseInt(e.target.value) || 0 })}
                      className={`${inputCls} mt-1`} />
                  </div>
                  <div>
                    <Label>Max agent turns</Label>
                    <input type="number" value={col.maxAgentTurns} min={1}
                      onChange={e => patch(col.id, { maxAgentTurns: parseInt(e.target.value) || 1 })}
                      className={`${inputCls} mt-1`} />
                  </div>
                </div>

                {/* Shell commands — Pre */}
                <div>
                  <Label>Pre-execution commands</Label>
                  <p className="text-[11px] text-zinc-500 mt-0.5 mb-2">
                    Run <strong className="text-zinc-400">before</strong> the agent (setup, checkout, install…)
                  </p>
                  <div className="space-y-2">
                    {[...col.shellCommands]
                      .filter(c => c.phase === 'Pre')
                      .sort((a, b) => a.position - b.position)
                      .map(cmd => (
                        <CmdRow
                          key={cmd.id}
                          cmd={cmd}
                          onSave={(c, d) => saveCmd(col.id, cmd.id, c, d, 'Pre')}
                          onDelete={() => deleteCmd(col.id, cmd.id)}
                        />
                      ))
                    }
                    <AddCmdForm phase="Pre" onAdd={(c, d) => addCmd(col.id, c, d, 'Pre')} />
                  </div>
                </div>

                {/* Shell commands — Post */}
                <div>
                  <Label>Post-execution commands</Label>
                  <p className="text-[11px] text-zinc-500 mt-0.5 mb-2">
                    Run <strong className="text-zinc-400">after</strong> the agent (commit, test, lint…). Output feeds into routing.
                  </p>
                  <div className="space-y-2">
                    {[...col.shellCommands]
                      .filter(c => c.phase === 'Post')
                      .sort((a, b) => a.position - b.position)
                      .map(cmd => (
                        <CmdRow
                          key={cmd.id}
                          cmd={cmd}
                          onSave={(c, d) => saveCmd(col.id, cmd.id, c, d, 'Post')}
                          onDelete={() => deleteCmd(col.id, cmd.id)}
                        />
                      ))
                    }
                    <AddCmdForm phase="Post" onAdd={(c, d) => addCmd(col.id, c, d, 'Post')} />
                  </div>
                </div>
              </>)}

              {/* Save */}
              <div className="pt-1">
                <button
                  onClick={() => saveColumn(col)}
                  disabled={saving === col.id}
                  className="px-3 py-1.5 bg-zinc-800 text-zinc-100 dark:bg-zinc-100 dark:text-zinc-900 text-xs font-semibold rounded hover:bg-zinc-700 dark:hover:bg-white transition-colors disabled:opacity-50"
                >
                  {saving === col.id ? 'Saving…' : 'Save'}
                </button>
              </div>
            </div>
          )}
        </div>
      ))}

      {/* Add column */}
      {addForm ? (
        <div className="bg-surface-raised border border-border rounded-lg px-4 py-3 space-y-3">
          <Label>New column</Label>
          <input
            autoFocus
            type="text"
            value={newName}
            onChange={e => setNewName(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && createColumn()}
            placeholder="Column name…"
            className={inputCls}
          />
          <div className="flex items-center gap-3">
            <select value={newType} onChange={e => setNewType(e.target.value as ColumnType)} className={selectCls}>
              <option value="Agent">Agent</option>
              <option value="Input">Input</option>
            </select>
            <button
              onClick={createColumn}
              disabled={!newName.trim()}
              className="px-3 py-1.5 bg-zinc-800 text-zinc-100 dark:bg-zinc-100 dark:text-zinc-900 text-xs font-semibold rounded hover:bg-zinc-700 dark:hover:bg-white transition-colors disabled:opacity-50"
            >
              Create
            </button>
            <button
              onClick={() => { setAddForm(false); setNewName(''); }}
              className="text-xs text-zinc-500 hover:text-zinc-300 transition-colors"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <button
          onClick={() => setAddForm(true)}
          className="w-full py-2 text-xs text-zinc-500 hover:text-zinc-300 border border-dashed border-zinc-300 dark:border-zinc-700 rounded-lg hover:border-zinc-500 transition-colors"
        >
          + Add column
        </button>
      )}
    </div>
  );
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function CmdRow({ cmd, onSave, onDelete }: {
  cmd: ColumnShellCommand;
  onSave: (command: string, workingDir: string) => void;
  onDelete: () => void;
}) {
  const [command, setCommand] = useState(cmd.command);
  const [workDir, setWorkDir] = useState(cmd.workingDirectory ?? '');
  const origCmd = useRef(cmd.command);
  const origDir = useRef(cmd.workingDirectory ?? '');

  const handleBlur = () => {
    if (command !== origCmd.current || workDir !== origDir.current) {
      onSave(command, workDir);
      origCmd.current = command;
      origDir.current = workDir;
    }
  };

  return (
    <div className="flex gap-2 items-center">
      <span className="text-zinc-500 font-mono text-xs flex-none">$</span>
      <input
        value={command}
        onChange={e => setCommand(e.target.value)}
        onBlur={handleBlur}
        className={`${inputCls} flex-1`}
        placeholder="command"
      />
      <input
        value={workDir}
        onChange={e => setWorkDir(e.target.value)}
        onBlur={handleBlur}
        className={`${inputCls} w-32 flex-none`}
        placeholder="dir"
        title="Working directory (relative to worktree)"
      />
      <button
        onClick={onDelete}
        className="text-zinc-600 hover:text-red-400 text-xs px-1.5 py-1 rounded hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors flex-none"
      >
        ✕
      </button>
    </div>
  );
}

function AddCmdForm({ onAdd, phase }: { onAdd: (command: string, workingDir: string) => void; phase: ShellCommandPhase }) {
  const [command, setCommand] = useState('');
  const [workDir, setWorkDir] = useState('');
  const placeholder = phase === 'Pre' ? 'e.g. npm install' : 'e.g. git add -A && git commit -m "wip"';

  const submit = () => {
    if (!command.trim()) return;
    onAdd(command.trim(), workDir.trim());
    setCommand('');
    setWorkDir('');
  };

  return (
    <div className="flex gap-2 items-center">
      <span className="text-zinc-500 font-mono text-xs flex-none">$</span>
      <input
        value={command}
        onChange={e => setCommand(e.target.value)}
        onKeyDown={e => e.key === 'Enter' && submit()}
        className={`${inputCls} flex-1`}
        placeholder={placeholder}
      />
      <input
        value={workDir}
        onChange={e => setWorkDir(e.target.value)}
        onKeyDown={e => e.key === 'Enter' && submit()}
        className={`${inputCls} w-32 flex-none`}
        placeholder="dir"
        title="Working directory"
      />
      <button
        onClick={submit}
        disabled={!command.trim()}
        className="text-xs px-2 py-1.5 bg-zinc-200 dark:bg-zinc-700 text-zinc-700 dark:text-zinc-300 rounded hover:bg-zinc-300 dark:hover:bg-zinc-600 transition-colors disabled:opacity-40 flex-none"
      >
        Add
      </button>
    </div>
  );
}

// ── Primitives ─────────────────────────────────────────────────────────────────

function Label({ children }: { children: React.ReactNode }) {
  return <div className="text-[11px] text-zinc-500">{children}</div>;
}

function Toggle({ value, onChange }: { value: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      onClick={() => onChange(!value)}
      className={cn(
        'relative w-9 h-5 rounded-full transition-colors flex-none',
        value ? 'bg-indigo-500' : 'bg-zinc-300 dark:bg-zinc-700'
      )}
    >
      <span className={cn(
        'absolute top-0.5 w-4 h-4 bg-white rounded-full shadow transition-transform',
        value ? 'translate-x-4' : 'translate-x-0.5'
      )} />
    </button>
  );
}

const inputCls = 'w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded px-2.5 py-1.5 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:border-zinc-500 transition-colors';
const textareaCls = 'w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded px-2.5 py-1.5 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-600 focus:outline-none focus:border-zinc-500 resize-none transition-colors';
const selectCls = 'w-full bg-[var(--color-bg)] border border-zinc-300 dark:border-zinc-700 rounded px-2.5 py-1.5 text-sm text-zinc-700 dark:text-zinc-300 focus:outline-none focus:border-zinc-500 transition-colors';
