import { cn } from '../../utils/cn';

export type MobileTab = 'focus' | 'board';

interface Props {
  active: MobileTab;
  attentionCount: number;
  onChange: (tab: MobileTab) => void;
}

function FocusIcon() {
  return (
    <svg className="w-5 h-5" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M11.5 2L5 11h6l-1.5 7 7-9h-6l2-7z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" fill="none" />
    </svg>
  );
}

function BoardIcon() {
  return (
    <svg className="w-5 h-5" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="7" height="7" rx="1.5" stroke="currentColor" strokeWidth="1.5" />
      <rect x="11" y="2" width="7" height="7" rx="1.5" stroke="currentColor" strokeWidth="1.5" />
      <rect x="2" y="11" width="7" height="7" rx="1.5" stroke="currentColor" strokeWidth="1.5" />
      <rect x="11" y="11" width="7" height="7" rx="1.5" stroke="currentColor" strokeWidth="1.5" />
    </svg>
  );
}

export default function BottomNav({ active, attentionCount, onChange }: Props) {
  return (
    <nav className="md:hidden fixed bottom-0 left-0 right-0 h-14 bg-[#18181b]/95 backdrop-blur-sm border-t border-[#2a2a30] shadow-[0_-1px_0_#2a2a30] flex items-center z-20">
      <TabButton
        label="Focus"
        icon={<FocusIcon />}
        badge={attentionCount}
        active={active === 'focus'}
        onClick={() => onChange('focus')}
      />
      <TabButton
        label="Board"
        icon={<BoardIcon />}
        active={active === 'board'}
        onClick={() => onChange('board')}
      />
    </nav>
  );
}

function TabButton({ label, icon, badge, active, onClick }: { label: string; icon: React.ReactNode; badge?: number; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      className={cn(
        'flex-1 flex flex-col items-center justify-center gap-0.5 py-2 transition-colors',
        active ? 'text-brand-400' : 'text-zinc-600 hover:text-zinc-400'
      )}
    >
      <div className="relative">
        {icon}
        {badge !== undefined && badge > 0 && (
          <span className="absolute -top-1.5 -right-2 min-w-[16px] h-4 bg-amber-500 text-zinc-950 text-[10px] font-bold rounded-full flex items-center justify-center px-1">
            {badge}
          </span>
        )}
      </div>
      <span className="text-[11px] font-medium">{label}</span>
    </button>
  );
}
