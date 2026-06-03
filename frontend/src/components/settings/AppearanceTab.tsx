import { type ThemePreference, useTheme } from '../../hooks/useTheme';
import { cn } from '../../utils/cn';

function MoonIcon() {
  return (
    <svg className="w-5 h-5" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M17.293 13.293A8 8 0 016.707 2.707a8.001 8.001 0 1010.586 10.586z" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function SunIcon() {
  return (
    <svg className="w-5 h-5" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
      <circle cx="10" cy="10" r="3" stroke="currentColor" strokeWidth="1.5" />
      <path d="M10 2v2M10 16v2M2 10h2M16 10h2M4.93 4.93l1.41 1.41M13.66 13.66l1.41 1.41M4.93 15.07l1.41-1.41M13.66 6.34l1.41-1.41" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  );
}

function MonitorIcon() {
  return (
    <svg className="w-5 h-5" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="3" width="16" height="11" rx="2" stroke="currentColor" strokeWidth="1.5" />
      <path d="M7 17h6M10 14v3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
    </svg>
  );
}

const OPTIONS: { value: ThemePreference; label: string; description: string; icon: React.ReactNode }[] = [
  { value: 'dark',   label: 'Dark',   description: 'Dark background, optimized for low-light environments', icon: <MoonIcon /> },
  { value: 'light',  label: 'Light',  description: 'Light background, optimized for bright environments',   icon: <SunIcon /> },
  { value: 'system', label: 'System', description: "Follows your operating system's appearance setting",   icon: <MonitorIcon /> },
];

export default function AppearanceTab() {
  const { preference, setPreference } = useTheme();

  return (
    <div className="space-y-3 max-w-lg">
      {OPTIONS.map(opt => (
        <button
          key={opt.value}
          onClick={() => setPreference(opt.value)}
          className={cn(
            'flex items-center gap-4 w-full text-left p-4 rounded-lg border transition-all',
            preference === opt.value
              ? 'border-brand-500 bg-brand-500/5 ring-2 ring-brand-500/30'
              : 'border-border bg-surface-raised hover:border-zinc-400 dark:hover:border-zinc-500'
          )}
        >
          <span className={cn(
            'flex-none',
            preference === opt.value ? 'text-brand-400' : 'text-zinc-500'
          )}>
            {opt.icon}
          </span>
          <div className="flex-1 min-w-0">
            <div className={cn(
              'text-sm font-medium',
              preference === opt.value ? 'text-brand-400' : 'text-zinc-800 dark:text-zinc-200'
            )}>
              {opt.label}
            </div>
            <div className="text-[12px] text-zinc-500 mt-0.5">{opt.description}</div>
          </div>
          {preference === opt.value && (
            <span className="w-4 h-4 rounded-full bg-brand-500 flex items-center justify-center flex-none">
              <span className="w-2 h-2 rounded-full bg-white" />
            </span>
          )}
        </button>
      ))}
    </div>
  );
}
