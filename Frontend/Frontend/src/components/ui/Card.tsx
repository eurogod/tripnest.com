import type { HTMLAttributes } from 'react';

/** Bordered, rounded white surface used to group dashboard content. */
export default function Card({ className = '', ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={`rounded-xl border border-gray-200 bg-white ${className}`}
      {...props}
    />
  );
}
