import { useCallback, useEffect, useState } from 'react';
import AttentionBanner from '../components/layout/AttentionBanner';
import BottomNav, { type MobileTab } from '../components/layout/BottomNav';
import Header from '../components/layout/Header';
import KanbanBoard from '../components/board/KanbanBoard';
import MobileColumnFilter from '../components/board/MobileColumnFilter';
import TaskCard from '../components/task/TaskCard';
import TaskDetailDrawer from '../components/task/TaskDetailDrawer';
import CreateTaskModal from '../modals/CreateTaskModal';
import FocusPage from './FocusPage';
import { useBoard } from '../hooks/useBoard';

const STATUS_ORDER: Record<string, number> = {
  Asking: 0, Error: 1, Running: 2, Waiting: 3, PendingApproval: 3, Done: 4,
};

export default function BoardPage() {
  const {
    board, tasks, liveOutputs, contextEntries, selectedTaskId, isCreateModalOpen,
    isLoading, apiError,
    selectTask, openCreateModal, closeCreateModal,
    handleAnswer, handleApprove, handleRetry, handleDeleteTask, handleCreateTask,
  } = useBoard();

  const [mobileTab, setMobileTab] = useState<MobileTab>('focus');
  const [mobileColumnId, setMobileColumnId] = useState<string | null>(null);

  const selectedTask = tasks.find(t => t.id === selectedTaskId) ?? null;
  const attentionCount = tasks.filter(t => t.status === 'Asking').length;

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'n' || e.key === 'N') {
        const tag = (e.target as HTMLElement).tagName;
        if (tag !== 'INPUT' && tag !== 'TEXTAREA') {
          e.preventDefault();
          openCreateModal();
        }
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [openCreateModal]);

  const mobileTasks = useCallback(() => {
    const filtered = mobileColumnId
      ? tasks.filter(t => t.currentColumnId === mobileColumnId)
      : tasks;
    return [...filtered].sort((a, b) => (STATUS_ORDER[a.status] ?? 9) - (STATUS_ORDER[b.status] ?? 9));
  }, [tasks, mobileColumnId])();

  if (isLoading) return (
    <div className="flex items-center justify-center h-full gap-2 text-zinc-500 text-sm">
      <svg className="animate-spin w-4 h-4 text-brand-500" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
      </svg>
      Loading…
    </div>
  );

  if (apiError) return (
    <div className="flex items-center justify-center h-full">
      <div className="bg-red-950/50 border border-red-800 rounded-xl p-6 max-w-sm text-center space-y-2 shadow-lg">
        <div className="text-red-400 font-semibold text-sm">Backend not reachable</div>
        <div className="text-red-300/70 text-xs">{apiError}</div>
      </div>
    </div>
  );

  return (
    <div className="flex flex-col h-full">
      <Header board={board} onNewTask={openCreateModal} />
      <AttentionBanner tasks={tasks} columns={board.columns} onSelectTask={selectTask} />

      {/* ── Desktop board ─────────────────────────────────────────────────── */}
      <div className="hidden md:flex flex-1 overflow-hidden">
        <KanbanBoard
          board={board}
          tasks={tasks}
          liveOutputs={liveOutputs}
          onRetry={handleRetry}
          onSelectTask={selectTask}
        />
      </div>

      {/* ── Mobile views ──────────────────────────────────────────────────── */}
      <div className="flex md:hidden flex-col flex-1 overflow-hidden">
        {mobileTab === 'focus' ? (
          <FocusPage
            board={board}
            tasks={tasks}
            liveOutputs={liveOutputs}
            onRetry={handleRetry}
            onSelectTask={selectTask}
            onAnswer={handleAnswer}
          />
        ) : (
          <div className="flex flex-col flex-1 overflow-hidden">
            <MobileColumnFilter
              columns={board.columns}
              activeId={mobileColumnId}
              onSelect={setMobileColumnId}
            />
            <div className="flex-1 overflow-y-auto p-3 pb-20 space-y-2">
              {mobileTasks.map(task => {
                const col = board.columns.find(c => c.id === task.currentColumnId);
                if (!col) return null;
                return (
                  <TaskCard
                    key={task.id}
                    task={task}
                    column={col}
                    liveOutput={liveOutputs[task.id]}
                    onRetry={handleRetry}
                    onClick={selectTask}
                  />
                );
              })}
            </div>
          </div>
        )}
      </div>

      <BottomNav active={mobileTab} attentionCount={attentionCount} onChange={setMobileTab} onNewTask={openCreateModal} />

      <TaskDetailDrawer
        task={selectedTask}
        board={board}
        liveOutput={selectedTask ? liveOutputs[selectedTask.id] : undefined}
        contextEntries={selectedTask ? (contextEntries[selectedTask.id] ?? []) : []}
        onClose={() => selectTask(null)}
        onAnswer={handleAnswer}
        onApprove={handleApprove}
        onDelete={handleDeleteTask}
      />

      <CreateTaskModal
        isOpen={isCreateModalOpen}
        onClose={closeCreateModal}
        onCreate={handleCreateTask}
        repositories={board.repositories}
      />
    </div>
  );
}
