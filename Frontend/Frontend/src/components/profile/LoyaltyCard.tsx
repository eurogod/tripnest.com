import { useEffect, useState } from 'react';
import { getLoyaltyStatus, type LoyaltyStatus } from '../../api/loyalty';
import Card from '../ui/Card';
import Badge from '../ui/Badge';

/** Loyalty tier from completed stays — the discount applies automatically at checkout. */
export default function LoyaltyCard() {
  const [status, setStatus] = useState<LoyaltyStatus | null>(null);

  useEffect(() => {
    getLoyaltyStatus().then(setStatus).catch(() => setStatus(null));
  }, []);

  if (!status) return null;

  return (
    <Card className="p-6">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-bold text-ink">Loyalty</h2>
        <Badge tone="blue">{status.tier}</Badge>
      </div>
      <p className="mt-1 text-sm text-muted">
        {status.completedStays} completed {status.completedStays === 1 ? 'stay' : 'stays'}
        {status.discountPercent > 0 && ` · ${status.discountPercent}% off every booking, on us`}
      </p>
      {status.nextTier && status.staysToNextTier != null && (
        <p className="mt-1 text-xs text-muted">
          {status.staysToNextTier} more {status.staysToNextTier === 1 ? 'stay' : 'stays'} to {status.nextTier}.
        </p>
      )}
    </Card>
  );
}
