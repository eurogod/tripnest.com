import type { ReactNode } from 'react';
import { Logo } from '@/components/Logo';
import { Check, Shield } from '@/components/icons';

export function AuthShell({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="grid min-h-full lg:grid-cols-2">
      {/* Brand panel */}
      <div className="relative hidden overflow-hidden lg:block">
        <img
          src="https://images.unsplash.com/photo-1582268611958-ebfd161ef9cf?auto=format&fit=crop&w=1200&q=70"
          alt=""
          className="h-full w-full object-cover"
        />
        <div className="absolute inset-0 bg-gradient-to-br from-brand-900/95 via-brand-800/85 to-brand-700/60" />
        <div className="absolute inset-0 flex flex-col justify-between p-12 text-white">
          <Logo light />
          <div>
            <h2 className="max-w-sm text-3xl font-extrabold leading-tight">
              Ghana's trust-first home for verified rentals.
            </h2>
            <ul className="mt-6 space-y-3 text-sm">
              {[
                'Ghana Card-verified hosts & tenants',
                'Escrow-protected payments — money held until you move in',
                'Digital agreements & SMS safe-arrival alerts',
              ].map((f) => (
                <li key={f} className="flex items-center gap-2">
                  <span className="grid h-6 w-6 place-items-center rounded-full bg-white/15">
                    <Check className="h-3.5 w-3.5 text-gold-400" />
                  </span>
                  {f}
                </li>
              ))}
            </ul>
            <p className="mt-8 flex items-center gap-2 text-xs text-white/70">
              <Shield className="h-4 w-4" /> Trusted by thousands across Ghana
            </p>
          </div>
        </div>
      </div>

      {/* Form panel */}
      <div className="flex items-center justify-center bg-surface px-4 py-10">
        <div className="w-full max-w-md">
          <div className="mb-6 lg:hidden">
            <Logo />
          </div>
          <h1 className="text-2xl font-extrabold">{title}</h1>
          {subtitle && <p className="mt-1 text-sm text-muted">{subtitle}</p>}
          <div className="mt-6">{children}</div>
        </div>
      </div>
    </div>
  );
}
