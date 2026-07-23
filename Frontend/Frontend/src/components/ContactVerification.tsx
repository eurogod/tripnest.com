import { useState } from 'react';
import Badge from './ui/Badge';
import Button from './ui/Button';
import { ApiError } from '../api/client';
import { sendEmailOtp, sendPhoneOtp, verifyEmailOtp, verifyPhoneOtp } from '../api/verification';
import { refreshSessionFromServer } from '../store/authStore';

interface ContactVerificationProps {
  kind: 'email' | 'phone';
  verified: boolean;
  /** Called after a successful verification (session is already refreshed). */
  onVerified?: () => void;
}

/**
 * Inline OTP flow for verifying the account's own email address or phone
 * number: send a 6-digit code, enter it, done. Renders a green badge once
 * the flag is set.
 */
export default function ContactVerification({ kind, verified, onVerified }: ContactVerificationProps) {
  const [stage, setStage] = useState<'idle' | 'sending' | 'sent' | 'checking' | 'done'>('idle');
  const [code, setCode] = useState('');
  const [error, setError] = useState('');

  const label = kind === 'email' ? 'email' : 'phone number';

  if (verified || stage === 'done') {
    return <Badge tone="green">✓ {kind === 'email' ? 'Email verified' : 'Phone verified'}</Badge>;
  }

  const send = async () => {
    setStage('sending');
    setError('');
    try {
      await (kind === 'email' ? sendEmailOtp() : sendPhoneOtp());
      setStage('sent');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : `Could not send a code to your ${label}.`);
      setStage('idle');
    }
  };

  const verify = async (e: React.FormEvent) => {
    e.preventDefault();
    if (code.trim().length === 0 || stage === 'checking') return;
    setStage('checking');
    setError('');
    try {
      await (kind === 'email' ? verifyEmailOtp(code.trim()) : verifyPhoneOtp(code.trim()));
      await refreshSessionFromServer();
      setStage('done');
      onVerified?.();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'That code didn’t match. Try again.');
      setStage('sent');
    }
  };

  if (stage === 'idle' || stage === 'sending') {
    return (
      <span className="inline-flex flex-wrap items-center gap-2">
        <Button size="sm" variant="ghost" className="border border-gray-200" onClick={send} disabled={stage === 'sending'}>
          {stage === 'sending' ? 'Sending code…' : `Verify ${label}`}
        </Button>
        {error && <span className="text-xs text-rose-600">{error}</span>}
      </span>
    );
  }

  return (
    <form onSubmit={verify} className="inline-flex flex-wrap items-center gap-2">
      <input
        value={code}
        onChange={(e) => setCode(e.target.value)}
        inputMode="numeric"
        maxLength={6}
        placeholder="6-digit code"
        className="w-28 rounded-lg border border-gray-200 px-3 py-1.5 text-sm outline-none focus:border-brand"
        autoFocus
      />
      <Button type="submit" size="sm" disabled={stage === 'checking'}>
        {stage === 'checking' ? 'Checking…' : 'Confirm'}
      </Button>
      <button type="button" onClick={send} className="text-xs font-semibold text-brand hover:underline">
        Resend
      </button>
      {error && <span className="text-xs text-rose-600">{error}</span>}
    </form>
  );
}
