import { useRef } from 'react';

export function OtpInput({ value, onChange, length = 6 }: { value: string; onChange: (v: string) => void; length?: number }) {
  const refs = useRef<(HTMLInputElement | null)[]>([]);

  const setChar = (i: number, ch: string) => {
    const clean = ch.replace(/\D/g, '').slice(-1);
    const arr = value.split('');
    arr[i] = clean;
    const next = arr.join('').slice(0, length);
    onChange(next);
    if (clean && i < length - 1) refs.current[i + 1]?.focus();
  };

  return (
    <div className="flex justify-between gap-2">
      {Array.from({ length }).map((_, i) => (
        <input
          key={i}
          ref={(el) => (refs.current[i] = el)}
          inputMode="numeric"
          maxLength={1}
          value={value[i] ?? ''}
          onChange={(e) => setChar(i, e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Backspace' && !value[i] && i > 0) refs.current[i - 1]?.focus();
          }}
          onPaste={(e) => {
            e.preventDefault();
            const pasted = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, length);
            onChange(pasted);
            refs.current[Math.min(pasted.length, length - 1)]?.focus();
          }}
          className="h-12 w-12 rounded-lg border border-line text-center text-lg font-bold outline-none focus:border-brand-600 focus:ring-2 focus:ring-brand-600/20"
        />
      ))}
    </div>
  );
}
