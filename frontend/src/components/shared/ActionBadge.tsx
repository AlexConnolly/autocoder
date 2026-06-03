import { cn } from '../../utils/cn';
import type { TransitionAction } from '../../types';

interface Props {
  action: TransitionAction;
}

export default function ActionBadge({ action }: Props) {
  return (
    <span className={cn(
      'inline-flex items-center gap-1 text-[10px] font-medium px-1.5 py-0.5 rounded',
      action === 'Forward'  && 'bg-green-500/15 text-green-400',
      action === 'Backward' && 'bg-amber-500/15 text-amber-400',
      action === 'Ask'      && 'bg-amber-500/15 text-amber-400',
    )}>
      {action === 'Ask' && <span>⚡</span>}
      {action}
    </span>
  );
}
