import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { agreementsApi } from '@/lib/services';
import { api } from '@/lib/api';
import { PageHeader, Async } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { Modal } from '@/components/Modal';
import { AgreementStatusPill } from '@/components/badges';
import { Doc } from '@/components/icons';
import { fmtDate } from '@/lib/format';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import { AgreementStatus } from '@/lib/enums';
import type { Agreement } from '@/types/api';

export default function AgreementsPage() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const query = useQuery({ queryKey: ['agreements'], queryFn: agreementsApi.mine, enabled: !!user });
  const [view, setView] = useState<Agreement | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  async function sign(a: Agreement) {
    setBusy(a.agreementId);
    try {
      await agreementsApi.sign(a.agreementId);
      toast.success('Agreement signed');
      qc.invalidateQueries({ queryKey: ['agreements'] });
      setView(null);
    } catch {
      toast.error('Could not sign agreement');
    } finally {
      setBusy(null);
    }
  }

  async function download(a: Agreement) {
    try {
      const res = await api.blob(agreementsApi.downloadUrl(a.agreementId));
      const url = URL.createObjectURL(res.data as Blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `agreement-${a.agreementId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      toast.error('Download not available yet');
    }
  }

  return (
    <div>
      <PageHeader title="Rental agreements" subtitle="Read, sign and download your tenancy agreements." />
      <Async
        query={query}
        emptyIcon={<Doc className="h-6 w-6" />}
        emptyTitle="No agreements yet"
        emptySubtitle="Once a booking is confirmed, its agreement appears here to sign."
      >
        {(agreements) => (
          <div className="space-y-3">
            {agreements.map((a) => (
              <div key={a.agreementId} className="card flex flex-wrap items-center justify-between gap-3 p-4">
                <div className="flex items-center gap-3">
                  <div className="grid h-10 w-10 place-items-center rounded-lg bg-brand-50 text-brand-600">
                    <Doc className="h-5 w-5" />
                  </div>
                  <div>
                    <p className="font-semibold">Tenancy agreement</p>
                    <p className="text-xs text-muted">Created {fmtDate(a.createdAt)}{a.signedAt ? ` · Signed ${fmtDate(a.signedAt)}` : ''}</p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <AgreementStatusPill status={a.status} />
                  <Button variant="outline" size="sm" onClick={() => setView(a)}>
                    View
                  </Button>
                  {a.status === AgreementStatus.Signed && (
                    <Button variant="ghost" size="sm" onClick={() => download(a)}>
                      Download
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </Async>

      <Modal open={!!view} onClose={() => setView(null)} title="Tenancy agreement" maxWidth="max-w-2xl">
        <div className="whitespace-pre-wrap rounded-lg bg-surface p-4 text-sm leading-relaxed text-ink">
          {view?.termsContent || 'No terms content available.'}
        </div>
        {view && view.status === AgreementStatus.Pending && (
          <Button block className="mt-5" loading={busy === view.agreementId} onClick={() => sign(view)}>
            I agree — sign this agreement
          </Button>
        )}
      </Modal>
    </div>
  );
}
