import type { ChatMessage } from '../../types';

/** A thread item — extends the API message with optional recorded-audio fields. */
export interface ChatItem extends ChatMessage {
  audioUrl?: string;
  /** Recorded length in seconds. */
  duration?: number;
}
