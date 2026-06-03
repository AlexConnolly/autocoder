import { useEffect, useRef } from 'react';

interface Props {
  output: string;
}

export default function LiveOutputPanel({ output }: Props) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (ref.current) ref.current.scrollTop = ref.current.scrollHeight;
  }, [output]);

  const lines = output.split('\n');
  const sentinelIdx = lines.findIndex(l => l.includes('<<<STRUCTURED_OUTPUT>>>'));
  const displayLines = sentinelIdx === -1 ? lines : lines.slice(0, sentinelIdx);
  const isRouting = sentinelIdx !== -1;

  return (
    <div className="space-y-2">
      <div
        ref={ref}
        className="bg-[#0a0a0b] rounded-lg p-3 overflow-y-auto max-h-64 font-mono text-[13px] leading-relaxed"
      >
        {displayLines.map((line, i) => (
          <div key={i} className="text-zinc-300">
            {line || ' '}
          </div>
        ))}
        {!isRouting && (
          <span className="inline-block w-2 h-4 bg-zinc-400 animate-pulse align-middle" />
        )}
      </div>
      {isRouting && (
        <div className="flex items-center gap-2 text-[12px] text-zinc-500">
          <span className="w-1.5 h-1.5 rounded-full bg-blue-400 animate-pulse flex-none" />
          Making routing decision…
        </div>
      )}
    </div>
  );
}
