import { useState } from 'react';
import { requestPasswordReset, resetPassword } from '../store/authStore';

const input = 'w-full rounded-xl border border-gray-300 px-4 py-3 text-sm text-ink outline-none transition-colors focus:border-brand';

/** Two-step password reset: request a code by email, then set a new password with it. */
export default function ForgotPassword({ onDone }: { onDone: () => void }) {
  const [step, setStep] = useState<'request' | 'reset' | 'done'>('request');
  const [email, setEmail] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      if (step === 'request') {
        await requestPasswordReset(email.trim().toLowerCase());
        setStep('reset');
      } else {
        await resetPassword(email.trim().toLowerCase(), code.trim(), password);
        setStep('done');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  };

  if (step === 'done') {
    return (
      <div className="space-y-3 text-center">
        <p className="text-sm text-ink">Your password has been reset.</p>
        <button type="button" onClick={onDone} className="text-sm font-semibold text-brand">Back to sign in</button>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="space-y-3">
      <p className="text-sm text-muted">
        {step === 'request'
          ? 'Enter your email and we’ll send a reset code.'
          : `Enter the code sent to ${email} and choose a new password.`}
      </p>
      {step === 'request' ? (
        <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="Email address" required className={input} />
      ) : (
        <>
          <input value={code} onChange={(e) => setCode(e.target.value)} placeholder="Reset code" required className={input} />
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="New password" required className={input} />
        </>
      )}
      {error && <p className="text-xs text-rose-600" role="alert">{error}</p>}
       <button type="submit" disabled={busy} className="w-full rounded-xl bg-brand py-3 text-sm font-semibold text-white hover:bg-brand/90 disabled:opacity-60">
        {busy ? 'Please wait…' : step === 'request' ? 'Send reset code' : 'Reset password'}
      </button>
      <button type="button" onClick={onDone} className="w-full text-center text-sm font-semibold text-brand">Back to sign in</button>
    </form>
  );
}
