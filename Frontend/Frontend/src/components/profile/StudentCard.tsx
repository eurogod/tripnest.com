import { useEffect, useState } from 'react';
import { getStudentStatus, sendStudentOtp, verifyStudentOtp, type StudentStatus } from '../../api/profile';
import Card from '../ui/Card';
import Button from '../ui/Button';

/**
 * Student status: prove control of a university mailbox (code emailed to it) to unlock the
 * student discount on Student-type listings. Academic-domain check is server-side.
 */
export default function StudentCard() {
  const [status, setStatus] = useState<StudentStatus | null>(null);
  const [email, setEmail] = useState('');
  const [code, setCode] = useState('');
  const [step, setStep] = useState<'idle' | 'code'>('idle');
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);

  useEffect(() => {
    getStudentStatus().then(setStatus).catch(() => setStatus(null));
  }, []);

  const send = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setNote(null);
    try {
      await sendStudentOtp(email.trim());
      setStep('code');
      setNote(`Code sent to ${email.trim()} — check your university inbox.`);
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Could not send the code.');
    } finally {
      setBusy(false);
    }
  };

  const verify = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setNote(null);
    try {
      await verifyStudentOtp(code.trim());
      setStatus(await getStudentStatus().catch(() => status));
      setStep('idle');
      setNote('Verified — the student discount now applies on student listings.');
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Wrong or expired code.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card className="p-6">
      <h2 className="text-lg font-bold text-ink">Student discount</h2>

      {status?.isVerifiedStudent ? (
        <p className="mt-1 text-sm text-muted">
          Verified student ({status.studentEmail}).
          {status.expiresAt && ` Valid until ${new Date(status.expiresAt).toLocaleDateString()}.`}
        </p>
      ) : (
        <>
          <p className="mt-1 text-sm text-muted">
            Verify your university email to unlock discounted student stays.
          </p>
          {step === 'idle' ? (
            <form onSubmit={send} className="mt-3 flex gap-2">
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@st.ug.edu.gh"
                required
                className="flex-1 rounded-lg border border-gray-300 px-3 py-2 text-sm"
              />
              <Button size="sm" disabled={busy}>{busy ? 'Sending…' : 'Send code'}</Button>
            </form>
          ) : (
            <form onSubmit={verify} className="mt-3 flex gap-2">
              <input
                value={code}
                onChange={(e) => setCode(e.target.value)}
                placeholder="6-digit code"
                required
                inputMode="numeric"
                className="flex-1 rounded-lg border border-gray-300 px-3 py-2 text-sm"
              />
              <Button size="sm" disabled={busy}>{busy ? 'Checking…' : 'Verify'}</Button>
            </form>
          )}
        </>
      )}

      {note && <p className="mt-2 text-sm text-muted">{note}</p>}
    </Card>
  );
}
