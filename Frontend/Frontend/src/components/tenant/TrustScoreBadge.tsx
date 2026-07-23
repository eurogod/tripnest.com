import { useEffect, useState } from 'react';
import { getPropertyTrustScore, type TrustScore } from '../../api/trustscore';

/**
 * Compact trust-score pill for a listing (0–100 + label, e.g. "86 · Excellent"). Renders
 * nothing until the score loads — a listing without history simply shows no pill.
 */
export default function TrustScoreBadge({ propertyId }: { propertyId: string }) {
  const [score, setScore] = useState<TrustScore | null>(null);

  useEffect(() => {
    getPropertyTrustScore(propertyId).then(setScore).catch(() => setScore(null));
  }, [propertyId]);

  if (!score) return null;

  const tone =
    score.finalScore >= 75 ? 'bg-green-50 text-green-700'
    : score.finalScore >= 50 ? 'bg-amber-50 text-amber-700'
    : 'bg-gray-100 text-muted';

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold ${tone}`}
      title={`Trust score ${Math.round(score.finalScore)}/100 (${score.trend})`}
    >
      🛡 {Math.round(score.finalScore)} · {score.label}
    </span>
  );
}
