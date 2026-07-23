import { useState } from 'react';

interface AvatarProps {
  name: string;
  /** Photo URL; initials are shown until it loads (or if it fails). */
  src?: string | null;
  size?: number;
  className?: string;
}

function initials(name: string): string {
  return name
    .split(' ')
    .map((part) => part[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

/** Circular avatar showing the user's photo when available, else initials. */
export default function Avatar({ name, src, size = 36, className = '' }: AvatarProps) {
  const [failed, setFailed] = useState(false);
  const showPhoto = src && !failed;
  return (
    <span
      className={`inline-flex shrink-0 items-center justify-center overflow-hidden rounded-full bg-brand-50 font-semibold text-brand ${className}`}
      style={{ width: size, height: size, fontSize: size * 0.4 }}
    >
      {showPhoto ? (
        <img
          src={src}
          alt={name}
          className="h-full w-full object-cover"
          onError={() => setFailed(true)}
        />
      ) : (
        initials(name)
      )}
    </span>
  );
}
