import { useState } from 'react';
import { Link } from 'react-router-dom';
import BoardTab from '../components/settings/BoardTab';
import ColumnsTab from '../components/settings/ColumnsTab';
import RepositoriesTab from '../components/settings/RepositoriesTab';
import { cn } from '../utils/cn';

type Tab = 'repositories' | 'board' | 'columns';

function ArrowLeftIcon() {
  return (
    <svg className="w-4 h-4" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M10 3L5 8l5 5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export default function SettingsPage() {
  const [active, setActive] = useState<Tab>('repositories');

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <header className="h-12 flex items-center gap-4 px-4 border-b border-[#2a2a30] bg-[#0f0f10] flex-none">
        <Link to="/" className="text-zinc-500 hover:text-zinc-300 transition-colors text-sm flex items-center gap-1">
          <ArrowLeftIcon />
          Board
        </Link>
        <div className="w-px h-4 bg-zinc-800" />
        <span className="text-base font-medium text-zinc-200">Settings</span>
      </header>

      <div className="flex flex-1 overflow-hidden">
        {/* Sidebar nav */}
        <nav className="w-44 flex-none border-r border-[#2a2a30] p-3 space-y-1 hidden sm:block">
          {(['repositories', 'board', 'columns'] as Tab[]).map(tab => (
            <button
              key={tab}
              onClick={() => setActive(tab)}
              className={cn(
                'w-full text-left text-sm px-3 py-2 rounded-lg capitalize transition-colors',
                active === tab
                  ? 'bg-brand-500/10 text-brand-400 font-medium'
                  : 'text-zinc-500 hover:text-zinc-300 hover:bg-zinc-800/50'
              )}
            >
              {tab}
            </button>
          ))}
        </nav>

        {/* Mobile tab strip */}
        <div className="sm:hidden flex border-b border-[#2a2a30] bg-[#0f0f10]">
          {(['repositories', 'board', 'columns'] as Tab[]).map(tab => (
            <button
              key={tab}
              onClick={() => setActive(tab)}
              className={cn(
                'flex-1 text-xs py-2.5 capitalize border-b-2 transition-colors',
                active === tab
                  ? 'border-brand-500 text-brand-400'
                  : 'border-transparent text-zinc-500'
              )}
            >
              {tab}
            </button>
          ))}
        </div>

        {/* Content */}
        <main className="flex-1 overflow-y-auto p-6">
          <h1 className="text-[11px] font-semibold uppercase tracking-widest text-zinc-500 mb-6">
            {active}
          </h1>
          {active === 'repositories' && <RepositoriesTab />}
          {active === 'board'        && <BoardTab />}
          {active === 'columns'      && <ColumnsTab />}
        </main>
      </div>
    </div>
  );
}
