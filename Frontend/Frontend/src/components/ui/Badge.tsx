import type { ReactNode } from 'react';

export type BadgeTone = 'green' | 'gray' | 'red' | 'blue' | 'amber';

const TONES: Record<BadgeTone, string> = {
  green: 'bg-brand-50 text-brand',
  gray: 'bg-gray-100 text-gray-600',
  red: 'bg-rose-50 text-rose-600',
  blue: 'bg-blue-50 text-blue-600',
  amber: 'bg-amber-50 text-amber-600',
};

interface BadgeProps {
  tone?: BadgeTone;
  className?: string;
  children: ReactNode;
}

export default function Badge({ tone = 'gray', className = '', children }: BadgeProps) {
  return (
    <span
      className={`inline-block rounded-full px-3 py-1 text-xs font-semibold ${TONES[tone]} ${className}`}
    >
      {children}
    </span>
  );
}
