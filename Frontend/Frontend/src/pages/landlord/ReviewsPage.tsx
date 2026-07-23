import { useState } from 'react';
import type { LandlordReview } from '../../types';
import { getLandlordReviews } from '../../api/landlord';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Avatar from '../../components/ui/Avatar';
import { StarIcon } from '../../components/tenant/icons';

function Stars({ n }: { n: number }) {
  return (
    <span className="inline-flex">
      {Array.from({ length: 5 }).map((_, i) => (
        <StarIcon key={i} size={14} className={i < n ? 'text-amber-400' : 'text-gray-200'} />
      ))}
    </span>
  );
}

function ReviewCard({ review, onReply }: { review: LandlordReview; onReply: (id: string, text: string) => void }) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState('');

  const send = (e: React.FormEvent) => {
    e.preventDefault();
    if (!draft.trim()) return;
    onReply(review.id, draft.trim());
    setDraft('');
    setOpen(false);
  };

  return (
    <Card className="p-5">
      <div className="flex items-start gap-3">
        <Avatar name={review.guest} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-2">
            <p className="font-semibold text-ink">{review.guest}</p>
            <span className="text-xs text-muted">{review.date}</span>
          </div>
          <div className="flex items-center gap-2">
            <Stars n={review.rating} />
            <span className="text-xs text-muted">· {review.listing}</span>
          </div>
          <p className="mt-2 text-sm text-ink">{review.text}</p>

          {review.reply ? (
            <div className="mt-3 rounded-lg bg-gray-50 p-3">
              <p className="text-xs font-semibold text-ink">Your reply</p>
              <p className="text-sm text-muted">{review.reply}</p>
            </div>
          ) : open ? (
            <form onSubmit={send} className="mt-3 flex gap-2">
              <input
                autoFocus
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                placeholder="Write a public reply…"
                className="flex-1 rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-brand"
              />
              <Button variant="dark" type="submit" size="sm">Post</Button>
            </form>
          ) : (
            <Button size="sm" variant="ghost" className="mt-2 px-0 hover:bg-transparent" onClick={() => setOpen(true)}>
              Reply to review
            </Button>
          )}
        </div>
      </div>
    </Card>
  );
}

function ReviewsView({ initial }: { initial: LandlordReview[] }) {
  const [rows, setRows] = useState(initial);
  const reply = (id: string, text: string) =>
    setRows((rs) => rs.map((r) => (r.id === id ? { ...r, reply: text } : r)));

  const avg = rows.reduce((s, r) => s + r.rating, 0) / rows.length;
  const dist = [5, 4, 3, 2, 1].map((star) => ({ star, count: rows.filter((r) => r.rating === star).length }));

  return (
    <div>
      <h1 className="mb-6 text-3xl font-bold tracking-tight text-ink">Reviews</h1>

      <Card className="mb-6 flex flex-col gap-6 p-6 sm:flex-row sm:items-center">
        <div className="text-center">
          <p className="text-4xl font-bold text-ink">{avg.toFixed(1)}</p>
          <Stars n={Math.round(avg)} />
          <p className="mt-1 text-xs text-muted">{rows.length} reviews</p>
        </div>
        <div className="flex-1 space-y-1.5">
          {dist.map(({ star, count }) => (
            <div key={star} className="flex items-center gap-2 text-sm">
              <span className="w-3 text-muted">{star}</span>
              <StarIcon size={12} className="text-amber-400" />
              <div className="h-2 flex-1 overflow-hidden rounded-full bg-gray-100">
                <div className="h-full rounded-full bg-brand" style={{ width: `${rows.length ? (count / rows.length) * 100 : 0}%` }} />
              </div>
              <span className="w-6 text-right text-xs text-muted">{count}</span>
            </div>
          ))}
        </div>
      </Card>

      <div className="space-y-4">
        {rows.map((r) => (
          <ReviewCard key={r.id} review={r} onReply={reply} />
        ))}
      </div>
    </div>
  );
}

export default function ReviewsPage() {
  const state = useAsync(getLandlordReviews, []);
  return (
    <AsyncBoundary state={state} loadingMessage="Loading reviews…" errorMessage="Failed to load reviews." emptyMessage="No reviews yet." isEmpty={(r) => r.length === 0}>
      {(rows) => <ReviewsView initial={rows} />}
    </AsyncBoundary>
  );
}
