import { useNavigate } from 'react-router-dom';
import { SparkleIcon } from './icons';
import { useSession } from '../../store/authStore';
import { openAssistant, useAssistantOpen } from '../../store/assistantStore';
import AssistantPanel from '../assistant/AssistantPanel';

/** Floating AI assistant launcher shown across the marketplace. */
export default function ChatButton() {
  const navigate = useNavigate();
  const session = useSession();
  const open = useAssistantOpen();
  return (
    <>
      <button
        onClick={() => (session ? openAssistant() : navigate('/welcome'))}
        className="fixed bottom-6 right-6 z-30 flex items-center gap-2 rounded-full bg-brand px-5 py-3 text-sm font-semibold text-white shadow-lg hover:bg-brand/90"
      >
        <SparkleIcon size={18} />
        Ask TripNest
      </button>
      {open && <AssistantPanel />}
    </>
  );
}
