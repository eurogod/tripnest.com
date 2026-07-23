import type { Conversation } from '../../types';
import Avatar from '../ui/Avatar';
import { PhoneIcon, ShieldIcon } from '../tenant/icons';

/** Right-hand details panel for the open conversation (lg screens only). */
export default function ThreadDetails({ conversation, onCall }: {
  conversation: Conversation;
  onCall: () => void;
}) {
  return (
    <aside className="hidden w-64 shrink-0 flex-col border-l border-gray-100 p-5 text-center lg:flex">
      <Avatar name={conversation.name} size={64} className="mx-auto" />
      <p className="mt-3 font-semibold text-ink">{conversation.name}</p>
      <p className="text-xs text-muted">{conversation.role}</p>
      <div className="mt-4 space-y-2 text-left">
        <p className="flex items-center gap-2 text-sm text-ink">
          <ShieldIcon size={15} className="text-brand" /> Verified {conversation.role.toLowerCase()}
        </p>
      </div>
      <button
        type="button"
        onClick={onCall}
        className="mt-5 flex items-center justify-center gap-2 rounded-lg bg-brand-50 py-2 text-sm font-semibold text-brand hover:bg-brand-50/70"
      >
        <PhoneIcon size={15} /> Call
      </button>
    </aside>
  );
}
