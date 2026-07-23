import { useMemo, useState } from 'react';
import type { Inquiry, InquiryStatus } from '../../types';
import { getInquiries, setInquiryStatus } from '../../api/landlord';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import Avatar from '../../components/ui/Avatar';

const STATUS: Record<InquiryStatus, { tone: BadgeTone; label: string }> = {
  new: { tone: 'green', label: 'New' },
  replied: { tone: 'blue', label: 'Replied' },
  archived: { tone: 'gray', label: 'Archived' },
};

const TABS: { id: InquiryStatus | 'all'; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'new', label: 'New' },
  { id: 'replied', label: 'Replied' },
  { id: 'archived', label: 'Archived' },
];

function InquiryCard({ inquiry, onReply, onArchive }: {
  inquiry: Inquiry;
  onReply: (id: string) => void;
  onArchive: (id: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState('');

  const send = (e: React.FormEvent) => {
    e.preventDefault();
    if (!draft.trim()) return;
    onReply(inquiry.id);
    setDraft('');
    setOpen(false);
  };

  return (
    <Card className="p-5">
      <div className="flex items-start gap-3">
        <Avatar name={inquiry.guest} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-2">
            <p className="font-semibold text-ink">{inquiry.guest}</p>
            <span className="flex items-center gap-2 text-xs text-muted">
              {inquiry.date}
              <Badge tone={STATUS[inquiry.status].tone}>{STATUS[inquiry.status].label}</Badge>
            </span>
          </div>
          <p className="text-xs text-muted">on {inquiry.listing}</p>
          <p className="mt-2 text-sm text-ink">{inquiry.message}</p>

          {open ? (
            <form onSubmit={send} className="mt-3 flex gap-2">
              <input
                autoFocus
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                placeholder="Write a reply…"
                className="flex-1 rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-brand"
              />
              <Button variant="dark" type="submit" size="sm">Send</Button>
            </form>
          ) : (
            inquiry.status !== 'archived' && (
              <div className="mt-3 flex gap-2">
                <Button variant="dark" size="sm" onClick={() => setOpen(true)}>Reply</Button>
                <Button size="sm" variant="ghost" onClick={() => onArchive(inquiry.id)}>Archive</Button>
              </div>
            )
          )}
        </div>
      </div>
    </Card>
  );
}

function InquiriesView({ initial }: { initial: Inquiry[] }) {
  const [rows, setRows] = useState(initial);
  const [tab, setTab] = useState<InquiryStatus | 'all'>('all');

  const setStatus = (id: string, status: 'replied' | 'archived') => {
    setRows((rs) => rs.map((q) => (q.id === id ? { ...q, status } : q)));
    setInquiryStatus(id, status).catch(() => {});
  };
  const reply = (id: string) => setStatus(id, 'replied');
  const archive = (id: string) => setStatus(id, 'archived');

  const visible = useMemo(() => (tab === 'all' ? rows : rows.filter((q) => q.status === tab)), [rows, tab]);
  const newCount = rows.filter((q) => q.status === 'new').length;

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">Inquiries</h1>
      <p className="mt-1 mb-6 text-sm text-muted">{newCount} new message{newCount === 1 ? '' : 's'} awaiting your reply.</p>

      <div className="mb-6 flex flex-wrap gap-2">
        {TABS.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            className={`rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors ${
              tab === t.id ? 'border-brand bg-brand-50 text-brand' : 'border-gray-200 text-gray-600 hover:bg-gray-100'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {visible.length === 0 ? (
        <p className="text-muted">No {tab === 'all' ? '' : tab} inquiries.</p>
      ) : (
        <div className="space-y-4">
          {visible.map((q) => (
            <InquiryCard key={q.id} inquiry={q} onReply={reply} onArchive={archive} />
          ))}
        </div>
      )}
    </div>
  );
}

export default function InquiriesPage() {
  const state = useAsync(getInquiries, []);
  return (
    <AsyncBoundary state={state} loadingMessage="Loading inquiries…" errorMessage="Failed to load inquiries." emptyMessage="No inquiries yet." isEmpty={(r) => r.length === 0}>
      {(rows) => <InquiriesView initial={rows} />}
    </AsyncBoundary>
  );
}
