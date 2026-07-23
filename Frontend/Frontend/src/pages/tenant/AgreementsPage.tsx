import { useState } from 'react';
import type { Agreement, AgreementStatus } from '../../types';
import {
  downloadAgreementPdf, getAgreements, getAgreementSummary, signAgreement, terminateAgreement,
  type AgreementSummary,
} from '../../api/agreements';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { formatCedi } from '../../lib/format';
import { useT } from '../../lib/i18n';
import { useSession } from '../../store/authStore';
import { FileIcon, CheckIcon, ClockIcon } from '../../components/tenant/icons';

const STATUS: Record<AgreementStatus, { tone: BadgeTone; label: string }> = {
  draft: { tone: 'blue', label: 'Draft' },
  pending: { tone: 'amber', label: 'Pending signature' },
  active: { tone: 'green', label: 'Active' },
  expired: { tone: 'gray', label: 'Expired' },
  terminated: { tone: 'red', label: 'Terminated' },
};

/** Who has signed so far. The agreement only becomes active once both parties have. */
function SignatureState({ agreement, isLandlord }: { agreement: Agreement; isLandlord: boolean }) {
  const row = (label: string, signed: boolean) => (
    <span className="inline-flex items-center gap-1.5">
      {signed
        ? <CheckIcon size={14} className="text-brand" />
        : <ClockIcon size={14} className="text-muted" />}
      <span className={signed ? 'text-ink' : 'text-muted'}>
        {label} {signed ? 'signed' : 'not signed yet'}
      </span>
    </span>
  );
  const youSigned = isLandlord ? agreement.landlordSigned : agreement.tenantSigned;
  const otherSigned = isLandlord ? agreement.tenantSigned : agreement.landlordSigned;
  return (
    <div className="mt-1.5 flex flex-wrap gap-x-4 gap-y-1 text-xs">
      {row('You', youSigned)}
      {row(isLandlord ? 'Tenant' : 'Landlord', otherSigned)}
    </div>
  );
}

function AgreementRow({
  agreement, onChange, notify, isLandlord,
}: {
  agreement: Agreement;
  onChange: (next: Agreement) => void;
  notify: (msg: string | null) => void;
  isLandlord: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const [showTerms, setShowTerms] = useState(false);
  const [summary, setSummary] = useState<AgreementSummary | null>(null);
  const [terminating, setTerminating] = useState(false);
  const [reason, setReason] = useState('');
  const a = agreement;

  // Signing is two-party, so it is only offered while the document is still open AND the
  // caller (this party — tenant or landlord) has not already signed it.
  const youSigned = isLandlord ? a.landlordSigned : a.tenantSigned;
  const otherSigned = isLandlord ? a.tenantSigned : a.landlordSigned;
  const canSign = (a.status === 'draft' || a.status === 'pending') && !youSigned;
  const awaitingOther = a.status === 'pending' && youSigned && !otherSigned;
  const canTerminate = a.status === 'active';

  const run = async (fn: () => Promise<void>) => {
    setBusy(true);
    notify(null);
    try { await fn(); } finally { setBusy(false); }
  };

  const sign = () => run(async () => {
    try {
      await signAgreement(a.id);
      // Both signed -> the server marks it Signed; otherwise it waits for the other party.
      const bothSigned = otherSigned;
      onChange({
        ...a,
        tenantSigned: isLandlord ? a.tenantSigned : true,
        landlordSigned: isLandlord ? true : a.landlordSigned,
        status: bothSigned ? 'active' : 'pending',
        signedAt: bothSigned ? new Date().toISOString() : a.signedAt,
      });
      notify(bothSigned
        ? 'Signed — the agreement is now active.'
        : `Signed. Waiting for the ${isLandlord ? 'tenant' : 'landlord'} to sign.`);
    } catch (e) {
      // Common rejections: no signature on the profile yet, or the terms changed since the
      // first signature (the server refuses to bind a document that was altered).
      notify(e instanceof Error ? e.message : 'Could not sign the agreement.');
    }
  });

  const terminate = () => run(async () => {
    try {
      await terminateAgreement(a.id, reason.trim());
      onChange({ ...a, status: 'terminated' });
      setTerminating(false);
      setReason('');
      notify('Agreement terminated.');
    } catch (e) {
      notify(e instanceof Error ? e.message : 'Could not terminate the agreement.');
    }
  });

  const download = () => run(async () => {
    try { await downloadAgreementPdf(a.id); }
    catch { notify('Could not download the agreement PDF.'); }
  });

  const explain = () => run(async () => {
    if (summary) { setSummary(null); return; }
    try { setSummary(await getAgreementSummary(a.id)); }
    catch (e) { notify(e instanceof Error ? e.message : 'The AI explanation is unavailable right now.'); }
  });

  return (
    <Card className="p-5">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand">
          <FileIcon size={22} />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="font-semibold text-ink">{a.property}</h3>
            <Badge tone={STATUS[a.status].tone}>{STATUS[a.status].label}</Badge>
          </div>
          <p className="text-xs text-muted">{a.id} · Landlord: {a.landlord}</p>
          <p className="mt-1 text-sm text-muted">
            {a.startDate} – {a.endDate} · {formatCedi(a.rent)} / {a.period}
          </p>
          <SignatureState agreement={a} isLandlord={isLandlord} />
          {awaitingOther && (
            <p className="mt-1 text-xs text-muted">
              You have signed. The agreement becomes active once the {isLandlord ? 'tenant' : 'landlord'} signs too.
            </p>
          )}
        </div>

        <div className="flex flex-wrap gap-2">
          <Button size="sm" variant="ghost" disabled={busy} onClick={() => setShowTerms((s) => !s)}>
            {showTerms ? 'Hide terms' : 'Review terms'}
          </Button>
          {canSign && (
            <Button size="sm" disabled={busy} onClick={() => { void sign(); }}>
              {busy ? 'Signing…' : 'Sign'}
            </Button>
          )}
          <Button size="sm" variant="ghost" disabled={busy} onClick={() => { void explain(); }}>
            {summary ? 'Hide explanation' : 'Explain'}
          </Button>
          <Button size="sm" variant="ghost" disabled={busy} onClick={() => { void download(); }}>
            Download PDF
          </Button>
          {canTerminate && (
            <Button size="sm" variant="ghost" disabled={busy} onClick={() => setTerminating((s) => !s)}>
              Terminate
            </Button>
          )}
        </div>
      </div>

      {/* The actual contract text — what you are bound to. Shown before signing. */}
      {showTerms && (
        <div className="mt-4 rounded-xl border border-gray-200 bg-gray-50 p-4">
          <pre className="max-h-72 overflow-auto whitespace-pre-wrap break-words font-mono text-xs text-ink">
            {a.termsContent}
          </pre>
          {a.termsHash && (
            <p className="mt-3 border-t border-gray-200 pt-2 text-[11px] text-muted">
              <span className="font-semibold">Document integrity (SHA-256):</span>{' '}
              <span className="break-all font-mono">{a.termsHash}</span>
              <br />
              Both signatures bind to the terms hashing to this value; a different hash means
              the text was altered after signing.
            </p>
          )}
        </div>
      )}

      {/* The backend requires a reason to terminate. */}
      {terminating && (
        <div className="mt-4 rounded-xl border border-gray-200 p-4">
          <label className="block text-sm font-medium text-ink" htmlFor={`reason-${a.id}`}>
            Why are you ending this agreement?
          </label>
          <p className="mb-2 text-xs text-muted">
            This is a record-keeping action — refunds and cancellation charges are handled by
            the booking, not here.
          </p>
          <input
            id={`reason-${a.id}`}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Reason for termination"
            className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm"
          />
          <div className="mt-2 flex gap-2">
            <Button size="sm" disabled={busy || !reason.trim()} onClick={() => { void terminate(); }}>
              {busy ? 'Terminating…' : 'Confirm termination'}
            </Button>
            <Button size="sm" variant="ghost" disabled={busy} onClick={() => setTerminating(false)}>
              Cancel
            </Button>
          </div>
        </div>
      )}

      {summary && (
        <div className="mt-4 space-y-3 rounded-xl border border-gray-200 p-4">
          <h4 className="font-semibold text-ink">What this agreement says</h4>
          <p className="text-sm text-ink">{summary.summary}</p>
          {summary.keyTerms.length > 0 && (
            <div>
              <h5 className="text-sm font-medium text-ink">Key terms</h5>
              <ul className="mt-1 list-disc pl-5 text-sm text-muted">
                {summary.keyTerms.map((t) => <li key={t}>{t}</li>)}
              </ul>
            </div>
          )}
          {summary.yourObligations.length > 0 && (
            <div>
              <h5 className="text-sm font-medium text-ink">Your obligations</h5>
              <ul className="mt-1 list-disc pl-5 text-sm text-muted">
                {summary.yourObligations.map((t) => <li key={t}>{t}</li>)}
              </ul>
            </div>
          )}
          <p className="text-xs text-muted">{summary.disclaimer}</p>
        </div>
      )}
    </Card>
  );
}

function AgreementsView({ initial }: { initial: Agreement[] }) {
  const [rows, setRows] = useState(initial);
  const [note, setNote] = useState<string | null>(null);
  const session = useSession();
  // Agreements are between the booking's tenant and the property's landlord; a landlord (owner)
  // signs the landlord side, everyone else the tenant side.
  const isLandlord = session?.role === 'landlord';

  return (
    <div className="space-y-4">
      {rows.map((a) => (
        <AgreementRow
          key={a.id}
          agreement={a}
          isLandlord={isLandlord}
          onChange={(next) => setRows((rs) => rs.map((r) => (r.id === next.id ? next : r)))}
          notify={setNote}
        />
      ))}
      {note && <p className="text-sm text-muted">{note}</p>}
    </div>
  );
}

export default function AgreementsPage() {
  const state = useAsync(getAgreements, []);
  const t = useT();

  return (
    <div>
      <h1 className="mb-6 text-3xl font-bold text-ink">Agreements</h1>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading agreements…"
        errorMessage="Failed to load agreements."
        emptyMessage={t('You have no agreements yet.')}
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => <AgreementsView initial={rows} />}
      </AsyncBoundary>
    </div>
  );
}
