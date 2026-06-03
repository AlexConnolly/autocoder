import { useEffect, useState } from 'react';
import type { BoardRepository } from '../../types';
import * as api from '../../api/client';
import type { GitRepoResult } from '../../api/client';

const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true';
const BOARD_ID = '10000000-0000-0000-0000-000000000001';

export default function RepositoriesTab() {
  const [repos, setRepos] = useState<BoardRepository[]>([]);
  const [editing, setEditing] = useState<string | null>(null);
  const [showPicker, setShowPicker] = useState(false);

  useEffect(() => {
    if (USE_MOCK) return;
    api.fetchRepositories(BOARD_ID).then(setRepos).catch(console.error);
  }, []);

  const addRepo = async (found: GitRepoResult) => {
    if (USE_MOCK) {
      setRepos(prev => [...prev, { id: `repo-${Date.now()}`, boardId: BOARD_ID, name: found.name, localPath: found.path, defaultBranch: 'main' }]);
    } else {
      const repo = await api.addRepository(BOARD_ID, found.name, found.path, 'main').catch(console.error);
      if (repo) setRepos(prev => [...prev, repo]);
    }
    setShowPicker(false);
  };

  const save = async (repo: BoardRepository) => {
    if (!USE_MOCK) {
      const updated = await api.updateRepository(repo.id, repo.name, repo.localPath, repo.defaultBranch).catch(console.error);
      if (updated) setRepos(prev => prev.map(r => r.id === updated.id ? updated : r));
    }
    setEditing(null);
  };

  const remove = async (id: string) => {
    if (!USE_MOCK) await api.deleteRepository(id).catch(console.error);
    setRepos(prev => prev.filter(r => r.id !== id));
  };

  return (
    <div className="space-y-3 max-w-xl">
      {repos.map(repo => (
        <div key={repo.id} className="bg-[#1f1f23] border border-[#2a2a30] rounded-lg">
          {editing === repo.id ? (
            <RepoForm
              value={repo}
              onChange={v => setRepos(prev => prev.map(r => r.id === repo.id ? { ...r, ...v } : r))}
              onSave={() => save(repos.find(r => r.id === repo.id)!)}
              onCancel={() => setEditing(null)}
            />
          ) : (
            <div className="flex items-center gap-3 px-4 py-3">
              <div className="flex-1 min-w-0">
                <div className="text-sm font-medium text-zinc-200">{repo.name}</div>
                <div className="text-[12px] text-zinc-500 font-mono truncate">{repo.localPath}</div>
              </div>
              <span className="text-[11px] text-zinc-600 font-mono">{repo.defaultBranch}</span>
              <button onClick={() => setEditing(repo.id)} className="text-zinc-600 hover:text-zinc-300 text-xs px-2 py-1 rounded hover:bg-zinc-800 transition-colors">✎</button>
              <button onClick={() => remove(repo.id)} className="text-zinc-700 hover:text-red-400 text-xs px-2 py-1 rounded hover:bg-zinc-800 transition-colors">✕</button>
            </div>
          )}
        </div>
      ))}

      {showPicker ? (
        <RepoPicker
          existing={repos.map(r => r.localPath)}
          onAdd={addRepo}
          onClose={() => setShowPicker(false)}
        />
      ) : (
        <button
          onClick={() => setShowPicker(true)}
          className="text-sm text-zinc-500 hover:text-zinc-300 flex items-center gap-1.5 transition-colors"
        >
          <span>+</span> Add repository
        </button>
      )}
    </div>
  );
}

function RepoPicker({ existing, onAdd, onClose }: {
  existing: string[];
  onAdd: (r: GitRepoResult) => void;
  onClose: () => void;
}) {
  const [root, setRoot] = useState('');
  const [results, setResults] = useState<GitRepoResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [searched, setSearched] = useState(false);
  const [filter, setFilter] = useState('');
  const [manualPath, setManualPath] = useState('');

  const search = async () => {
    setLoading(true);
    setSearched(false);
    try {
      const found = await api.findGitRepos(root || undefined);
      setResults(found);
      setSearched(true);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const filtered = results.filter(r =>
    !existing.includes(r.path) &&
    (filter === '' || r.name.toLowerCase().includes(filter.toLowerCase()) || r.path.toLowerCase().includes(filter.toLowerCase()))
  );

  const addManual = () => {
    if (!manualPath.trim()) return;
    const name = manualPath.trim().replace(/\\/g, '/').split('/').pop() ?? manualPath.trim();
    onAdd({ name, path: manualPath.trim() });
  };

  return (
    <div className="bg-[#1f1f23] border border-[#2a2a30] rounded-lg p-4 space-y-4">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-zinc-200">Find git repositories</span>
        <button onClick={onClose} className="text-zinc-600 hover:text-zinc-300 text-xs transition-colors">✕</button>
      </div>

      {/* Scan controls */}
      <div className="flex gap-2">
        <input
          value={root}
          onChange={e => setRoot(e.target.value)}
          placeholder="Search root (default: home folder)"
          className={`${inputCls} flex-1 font-mono text-xs`}
          onKeyDown={e => e.key === 'Enter' && search()}
        />
        <button
          onClick={search}
          disabled={loading}
          className="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold rounded transition-colors disabled:opacity-50 whitespace-nowrap"
        >
          {loading ? 'Scanning…' : 'Scan'}
        </button>
      </div>

      {/* Results */}
      {searched && (
        <>
          {results.length === 0 ? (
            <p className="text-xs text-zinc-500">No git repositories found.</p>
          ) : (
            <div className="space-y-2">
              <input
                value={filter}
                onChange={e => setFilter(e.target.value)}
                placeholder="Filter…"
                className={`${inputCls} text-xs`}
              />
              <div className="max-h-48 overflow-y-auto space-y-1 pr-1">
                {filtered.length === 0 && (
                  <p className="text-xs text-zinc-600 py-2">No results match filter (or all already added).</p>
                )}
                {filtered.map(r => (
                  <button
                    key={r.path}
                    onClick={() => onAdd(r)}
                    className="w-full text-left px-3 py-2 rounded hover:bg-zinc-800 transition-colors group"
                  >
                    <div className="text-sm text-zinc-200 group-hover:text-white">{r.name}</div>
                    <div className="text-[11px] text-zinc-600 font-mono truncate">{r.path}</div>
                  </button>
                ))}
              </div>
            </div>
          )}
        </>
      )}

      {/* Manual entry fallback */}
      <div className="border-t border-[#2a2a30] pt-3">
        <p className="text-[11px] text-zinc-600 mb-2">Or enter a path manually:</p>
        <div className="flex gap-2">
          <input
            value={manualPath}
            onChange={e => setManualPath(e.target.value)}
            placeholder="C:\repos\myproject"
            className={`${inputCls} flex-1 font-mono text-xs`}
            onKeyDown={e => e.key === 'Enter' && addManual()}
          />
          <button
            onClick={addManual}
            disabled={!manualPath.trim()}
            className="px-3 py-1.5 bg-zinc-700 hover:bg-zinc-600 text-zinc-100 text-xs font-semibold rounded transition-colors disabled:opacity-40"
          >
            Add
          </button>
        </div>
      </div>
    </div>
  );
}

function RepoForm({ value, onChange, onSave, onCancel }: {
  value: BoardRepository;
  onChange: (v: Partial<BoardRepository>) => void;
  onSave: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="p-4 space-y-3">
      <Field label="Display name">
        <input value={value.name} onChange={e => onChange({ name: e.target.value })} className={inputCls} />
      </Field>
      <Field label="Local path">
        <input value={value.localPath} onChange={e => onChange({ localPath: e.target.value })} className={`${inputCls} font-mono`} />
      </Field>
      <Field label="Default branch">
        <input value={value.defaultBranch} onChange={e => onChange({ defaultBranch: e.target.value })} className={inputCls} />
      </Field>
      <div className="flex gap-2 pt-1">
        <button onClick={onSave} className="px-3 py-1.5 bg-zinc-100 text-zinc-900 text-xs font-semibold rounded hover:bg-white transition-colors">Save</button>
        <button onClick={onCancel} className="px-3 py-1.5 text-zinc-500 text-xs hover:text-zinc-300 transition-colors">Cancel</button>
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="block text-[11px] text-zinc-500 mb-1">{label}</label>
      {children}
    </div>
  );
}

const inputCls = 'w-full bg-[#0f0f10] border border-zinc-700 rounded px-2.5 py-1.5 text-sm text-zinc-100 placeholder-zinc-600 focus:outline-none focus:border-zinc-500 transition-colors';
