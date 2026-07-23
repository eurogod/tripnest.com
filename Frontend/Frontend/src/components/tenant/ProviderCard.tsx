import { useNavigate } from 'react-router-dom';
import type { ServiceProvider } from '../../types';
import { formatCedi } from '../../lib/format';
import Card from '../ui/Card';
import Button from '../ui/Button';
import Badge from '../ui/Badge';
import Avatar from '../ui/Avatar';
import { ShieldIcon, StarIcon, MapPinIcon } from './icons';

export default function ProviderCard({ provider }: { provider: ServiceProvider }) {
  const navigate = useNavigate();

  // Requests and chat live on the provider's profile page.
  const open = () => navigate(`/providers/${provider.id}`);

  return (
    <Card className="flex cursor-pointer flex-col p-5 transition-shadow hover:shadow-md" onClick={open}>
      <div className="flex items-center gap-3">
        <Avatar name={provider.name} size={48} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <p className="truncate font-semibold text-ink">{provider.name}</p>
            {provider.verified && (
              <Badge tone="green">
                <span className="inline-flex items-center gap-1">
                  <ShieldIcon size={11} /> Verified
                </span>
              </Badge>
            )}
          </div>
          <p className="text-sm text-muted">{provider.role}</p>
        </div>
      </div>

      <p className="mt-3 flex items-center gap-1.5 text-sm text-muted">
        <MapPinIcon size={14} /> {provider.location}
        <span className="mx-1">·</span>
        <StarIcon size={13} className="text-amber-400" />
        <span className="font-semibold text-ink">{provider.rating}</span>
        <span>({provider.reviews})</span>
      </p>

      <div className="mt-3 flex flex-wrap gap-1.5">
        {provider.skills.map((s) => (
          <span key={s} className="rounded-full bg-gray-100 px-2.5 py-0.5 text-xs text-gray-600">
            {s}
          </span>
        ))}
      </div>

      <div className="mt-4 flex items-center justify-between border-t border-gray-100 pt-4">
        <span className="text-sm text-ink">
          {provider.ratePeriod === 'commission' ? (
            <span className="text-muted">Commission based</span>
          ) : (
            <>
              <span className="font-bold text-brand">{formatCedi(provider.rate)}</span>
              <span className="text-muted"> / {provider.ratePeriod}</span>
            </>
          )}
        </span>
        <Button size="sm" onClick={(e) => { e.stopPropagation(); open(); }}>
          Request
        </Button>
      </div>
    </Card>
  );
}
