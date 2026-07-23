import { useMemo, useState } from 'react';
import type { HostTask, TaskPriority, TaskStatus } from '../types';
import { getHostTasks, setHostTaskStatus } from '../api/hostTasks';
import MaintenanceSection from '../components/landlord/MaintenanceSection';
import { useAsync } from '../hooks/useAsync';
import AsyncBoundary from '../components/AsyncBoundary';
import Card from '../components/ui/Card';
import Badge, { type BadgeTone } from '../components/ui/Badge';
import Checkbox from '../components/ui/Checkbox';

const PRIORITY_TONE: Record<TaskPriority, BadgeTone> = {
  high: 'red',
  medium: 'amber',
  low: 'gray',
};

const FILTERS: { id: TaskStatus | 'all'; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'todo', label: 'To do' },
  { id: 'in-progress', label: 'In progress' },
  { id: 'done', label: 'Done' },
];

function TaskRow({
  task,
  done,
  onToggle,
}: {
  task: HostTask;
  done: boolean;
  onToggle: () => void;
}) {
  return (
    <li className="flex items-center gap-4 px-6 py-4">
      <Checkbox checked={done} onChange={onToggle} ariaLabel={`Mark "${task.title}" as done`} />
      <div className="min-w-0 flex-1">
        <p className={`truncate font-semibold ${done ? 'text-muted line-through' : 'text-ink'}`}>
          {task.title}
        </p>
        <p className="text-sm text-muted">
          {task.property} · {task.assignee}
        </p>
      </div>
      <span className="hidden text-sm text-muted sm:block">{task.dueDate}</span>
      <Badge tone={PRIORITY_TONE[task.priority]}>{task.priority}</Badge>
    </li>
  );
}

export default function TasksPage() {
  const state = useAsync(getHostTasks, []);
  const [filter, setFilter] = useState<TaskStatus | 'all'>('all');

  return (
    <div>
      <h1 className="mb-8 text-4xl font-bold text-ink">Tasks</h1>

      <div className="mb-5 flex flex-wrap gap-2">
        {FILTERS.map((f) => (
          <button
            key={f.id}
            onClick={() => setFilter(f.id)}
            className={`rounded-full px-4 py-1.5 text-sm font-semibold transition-colors ${
              filter === f.id
                ? 'bg-ink text-white'
                : 'bg-gray-100 text-muted hover:bg-gray-200'
            }`}
          >
            {f.label}
          </button>
        ))}
      </div>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading tasks…"
        errorMessage="Failed to load tasks."
        emptyMessage="No tasks yet."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => <TaskList initial={rows} filter={filter} />}
      </AsyncBoundary>

      <MaintenanceSection />
    </div>
  );
}

function TaskList({ initial, filter }: { initial: HostTask[]; filter: TaskStatus | 'all' }) {
  const [rows, setRows] = useState(initial);

  // Optimistic toggle persisted through PATCH /api/tasks/{id}.
  const toggle = (task: HostTask) => {
    const next: TaskStatus = task.status === 'done' ? 'todo' : 'done';
    setRows((rs) => rs.map((t) => (t.id === task.id ? { ...t, status: next } : t)));
    setHostTaskStatus(task.id, next).catch(() =>
      setRows((rs) => rs.map((t) => (t.id === task.id ? { ...t, status: task.status } : t))),
    );
  };

  const visible = useMemo(
    () => (filter === 'all' ? rows : rows.filter((t) => t.status === filter)),
    [rows, filter],
  );

  if (visible.length === 0) return <p className="text-muted">Nothing here.</p>;

  return (
    <Card>
      <ul className="divide-y divide-gray-100">
        {visible.map((t) => (
          <TaskRow key={t.id} task={t} done={t.status === 'done'} onToggle={() => toggle(t)} />
        ))}
      </ul>
    </Card>
  );
}
