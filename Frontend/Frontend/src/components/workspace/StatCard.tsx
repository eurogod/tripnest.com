import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import Card from '../ui/Card';
import { ChevronRightIcon } from '../tenant/icons';

/**
 * Linked metric card in the landlord-home style: icon chip, staggered entrance
 * animation and a chevron that lights up on hover. Used across the agent /
 * caretaker / admin workspace overviews.
 */
export default function StatCard({ to, icon, label, value, sub, index = 0 }: {
  to: string;
  icon: ReactNode;
  label: string;
  value: string | number;
  sub?: ReactNode;
  index?: number;
}) {
  return (
    <Link to={to} className="no-underline">
      <Card
        className="tn-card-in tn-lift group h-full p-5"
        style={{ animationDelay: `${index * 60}ms` }}
      >
        <div className="flex items-start justify-between">
          <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-brand-50 text-brand">
            {icon}
          </span>
          <ChevronRightIcon size={16} className="text-gray-300 transition-colors group-hover:text-brand" />
        </div>
        <p className="mt-3 text-2xl font-bold tracking-tight text-ink">{value}</p>
        <p className="mt-0.5 text-sm text-muted">{label}</p>
        {sub && <p className="mt-1 text-xs">{sub}</p>}
      </Card>
    </Link>
  );
}
