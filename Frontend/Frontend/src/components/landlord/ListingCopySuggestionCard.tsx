import type { ListingCopySuggestion } from '../../api/listings';
import Button from '../ui/Button';
import { SparkleIcon } from '../tenant/icons';

interface ListingCopySuggestionCardProps {
  suggestion: ListingCopySuggestion;
  onApplyTitle: () => void;
  onApplyDescription: () => void;
  onApplyAll: () => void;
  onDismiss: () => void;
}

/** AI-drafted listing copy for the host to review — nothing is applied automatically. */
export default function ListingCopySuggestionCard({
  suggestion, onApplyTitle, onApplyDescription, onApplyAll, onDismiss,
}: ListingCopySuggestionCardProps) {
  return (
    <div className="rounded-xl border border-amber-200 bg-amber-50/50 p-4">
      <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-amber-800">
        <SparkleIcon size={16} />
        AI suggestion — review before applying
      </div>

      <div className="space-y-3">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="text-xs font-semibold uppercase tracking-wider text-muted">Title</p>
            <p className="text-sm font-medium text-ink">{suggestion.title}</p>
          </div>
          <Button type="button" variant="ghost" size="sm" onClick={onApplyTitle}>Use</Button>
        </div>

        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="text-xs font-semibold uppercase tracking-wider text-muted">Description</p>
            <p className="whitespace-pre-wrap text-sm text-ink">{suggestion.description}</p>
            {suggestion.highlights.length > 0 && (
              <ul className="mt-2 list-inside list-disc text-sm text-ink">
                {suggestion.highlights.map((h) => <li key={h}>{h}</li>)}
              </ul>
            )}
          </div>
          <Button type="button" variant="ghost" size="sm" onClick={onApplyDescription}>Use</Button>
        </div>
      </div>

      <div className="mt-4 flex justify-end gap-2 border-t border-amber-200/60 pt-3">
        <Button type="button" variant="ghost" size="sm" onClick={onDismiss}>Dismiss</Button>
        <Button type="button" size="sm" onClick={onApplyAll}>Apply all</Button>
      </div>
    </div>
  );
}
