import { useState } from 'react';
import type { ExchangeCategory, ExchangePost, ExchangeReply } from '../types';
import { addExchangeReply, createExchangePost, getExchangePosts, getExchangeReplies } from '../api/exchange';
import { useAsync } from '../hooks/useAsync';
import AsyncBoundary from '../components/AsyncBoundary';
import Card from '../components/ui/Card';
import Button from '../components/ui/Button';
import Avatar from '../components/ui/Avatar';
import Badge from '../components/ui/Badge';

const CATEGORIES: ExchangeCategory[] = [
  'General',
  'Tips',
  'Suppliers',
  'Regulation',
  'Marketplace',
];

function Composer({ onPost }: { onPost: (title: string, body: string, category: ExchangeCategory) => Promise<void> }) {
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [category, setCategory] = useState<ExchangeCategory>('General');
  const [posting, setPosting] = useState(false);
  const [error, setError] = useState('');

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !body.trim() || posting) return;
    setPosting(true);
    setError('');
    try {
      await onPost(title.trim(), body.trim(), category);
      setTitle('');
      setBody('');
      setCategory('General');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not publish your post.');
    } finally {
      setPosting(false);
    }
  };

  return (
    <Card className="mb-6 p-5">
      <form onSubmit={submit} className="space-y-3">
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Start a discussion…"
          className="w-full rounded-lg border border-gray-200 px-4 py-2.5 text-sm outline-none focus:border-brand"
        />
        <textarea
          value={body}
          onChange={(e) => setBody(e.target.value)}
          placeholder="Share details, ask a question, or recommend a supplier."
          rows={3}
          className="w-full resize-none rounded-lg border border-gray-200 px-4 py-2.5 text-sm outline-none focus:border-brand"
        />
        {error && <p className="text-sm text-red-600">{error}</p>}
        <div className="flex items-center justify-between gap-3">
          <select
            value={category}
            onChange={(e) => setCategory(e.target.value as ExchangeCategory)}
            className="rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-brand"
          >
            {CATEGORIES.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
          <Button type="submit" disabled={posting}>{posting ? 'Posting…' : 'Post'}</Button>
        </div>
      </form>
    </Card>
  );
}

function PostCard({ post }: { post: ExchangePost }) {
  const [replies, setReplies] = useState(post.replies);
  const [open, setOpen] = useState(false);
  const [thread, setThread] = useState<ExchangeReply[] | null>(null);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);

  const toggle = () => {
    setOpen((o) => !o);
    if (thread === null) {
      getExchangeReplies(post.id).then(setThread).catch(() => setThread([]));
    }
  };

  const sendReply = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!draft.trim() || sending) return;
    setSending(true);
    try {
      const reply = await addExchangeReply(post.id, draft.trim());
      setThread((t) => [...(t ?? []), reply]);
      setReplies((r) => r + 1);
      setDraft('');
    } catch {
      // keep the draft so the host can retry
    } finally {
      setSending(false);
    }
  };

  return (
    <Card className="p-5">
      <div className="flex items-start gap-3">
        <Avatar name={post.author} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="font-semibold text-ink">{post.author}</span>
            <span className="text-xs text-muted">· {post.role}</span>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <h3 className="font-semibold text-ink">{post.title}</h3>
            {post.pinned && <Badge tone="amber">Pinned</Badge>}
            <Badge tone="blue">{post.category}</Badge>
          </div>
          <p className="mt-2 text-sm text-muted">{post.body}</p>
          <div className="mt-3 flex items-center gap-4 text-sm text-muted">
            <span>{replies} replies</span>
            <span>·</span>
            <span>{post.createdAt}</span>
            <button onClick={toggle} className="ml-auto font-semibold text-brand hover:underline">
              Reply
            </button>
          </div>
          {open && (
            <>
              {thread && thread.length > 0 && (
                <div className="mt-3 space-y-3 border-l-2 border-gray-100 pl-4">
                  {thread.map((r) => (
                    <div key={r.id}>
                      <div className="flex items-center gap-2 text-sm">
                        <span className="font-semibold text-ink">{r.author}</span>
                        <span className="text-xs text-muted">· {r.createdAt}</span>
                      </div>
                      <p className="mt-0.5 text-sm text-muted">{r.body}</p>
                    </div>
                  ))}
                </div>
              )}
              <form onSubmit={sendReply} className="mt-3 flex gap-2">
                <input
                  value={draft}
                  onChange={(e) => setDraft(e.target.value)}
                  placeholder="Write a reply…"
                  className="flex-1 rounded-lg border border-gray-200 px-3 py-2 text-sm outline-none focus:border-brand"
                />
                <Button type="submit" size="sm" disabled={sending}>{sending ? 'Sending…' : 'Send'}</Button>
              </form>
            </>
          )}
        </div>
      </div>
    </Card>
  );
}

export default function OwnerExchangePage() {
  const state = useAsync(getExchangePosts, []);
  const [added, setAdded] = useState<ExchangePost[]>([]);

  const addPost = async (title: string, body: string, category: ExchangeCategory) => {
    const post = await createExchangePost(title, body, category);
    setAdded((prev) => [post, ...prev]);
  };

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="mb-8 text-4xl font-bold text-ink">Owner Exchange</h1>

      <Composer onPost={addPost} />

      <AsyncBoundary
        state={state}
        loadingMessage="Loading discussions…"
        errorMessage="Failed to load discussions."
      >
        {(rows) => {
          const fetched = rows.filter((p) => !added.some((a) => a.id === p.id));
          const all = [...added, ...fetched].sort(
            (a, b) => Number(b.pinned) - Number(a.pinned),
          );
          return all.length === 0 ? (
            <p className="text-muted">No discussions yet — start the first one above.</p>
          ) : (
            <div className="space-y-4">
              {all.map((p) => (
                <PostCard key={p.id} post={p} />
              ))}
            </div>
          );
        }}
      </AsyncBoundary>
    </div>
  );
}
