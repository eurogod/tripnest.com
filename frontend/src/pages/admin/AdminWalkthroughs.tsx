import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { walkthroughsApi } from '@/lib/services';
import { PageHeader, Async } from '@/components/dashboard';
import { Button, Field } from '@/components/ui';
import { Modal } from '@/components/Modal';
import { Camera, Check } from '@/components/icons';
import { parsePhotos } from '@/lib/format';
import { usePropertyLookup } from '@/lib/hooks';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import type { PropertyWalkthroughStatus } from '@/types/api';

function videoSrc(path?: string | null): string | undefined {
  if (!path) return undefined;
  return parsePhotos(path)[0];
}

export default function AdminWalkthroughs() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const query = useQuery({ queryKey: ['pending-walkthroughs'], queryFn: walkthroughsApi.pending, enabled: !!user });
  const props = usePropertyLookup('all');
  const [rejecting, setRejecting] = useState<PropertyWalkthroughStatus | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState<string | null>(null);

  async function review(propertyId: string, approved: boolean, rejectionReason?: string) {
    setBusy(propertyId);
    try {
      await walkthroughsApi.review(propertyId, approved, rejectionReason);
      toast.success(approved ? 'Walkthrough approved' : 'Walkthrough rejected');
      qc.invalidateQueries({ queryKey: ['pending-walkthroughs'] });
      qc.invalidateQueries({ queryKey: ['admin-stats'] });
      setRejecting(null);
      setReason('');
    } catch {
      toast.error('Could not submit review');
    } finally {
      setBusy(null);
    }
  }

  return (
    <div>
      <PageHeader title="Walkthrough reviews" subtitle="Approve walkthrough videos so honest listings can go live — reject anything misleading." />
      <Async
        query={query}
        emptyIcon={<Check className="h-6 w-6" />}
        emptyTitle="Queue is clear"
        emptySubtitle="No walkthrough videos are waiting for review right now."
      >
        {(items) => (
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            {items.map((w) => {
              const src = videoSrc(w.videoPath);
              return (
                <div key={w.propertyId} className="card overflow-hidden">
                  <div className="aspect-video bg-ink/90">
                    {src ? (
                      <video src={src} controls className="h-full w-full object-contain" />
                    ) : (
                      <div className="grid h-full place-items-center text-white/60">
                        <Camera className="h-8 w-8" />
                      </div>
                    )}
                  </div>
                  <div className="p-4">
                    <h3 className="font-bold">{props.get(w.propertyId)?.title ?? 'Property'}</h3>
                    <p className="text-sm text-muted">{props.get(w.propertyId)?.location ?? w.propertyId}</p>
                    <div className="mt-3 flex gap-2">
                      <Button size="sm" loading={busy === w.propertyId} onClick={() => review(w.propertyId, true)}>
                        <Check className="h-4 w-4" /> Approve
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => setRejecting(w)}>
                        Reject
                      </Button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </Async>

      <Modal open={!!rejecting} onClose={() => setRejecting(null)} title="Reject walkthrough" maxWidth="max-w-md">
        <p className="text-sm text-muted">Tell the landlord why — they’ll see this and can re-upload.</p>
        <div className="mt-4">
          <Field label="Reason">
            <textarea
              className="input min-h-[90px] resize-y"
              placeholder="e.g. Video doesn’t show the kitchen or bathroom…"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </Field>
        </div>
        <Button
          variant="danger"
          block
          className="mt-4"
          loading={busy === rejecting?.propertyId}
          disabled={!reason.trim()}
          onClick={() => rejecting && review(rejecting.propertyId, false, reason.trim())}
        >
          Reject walkthrough
        </Button>
      </Modal>
    </div>
  );
}
