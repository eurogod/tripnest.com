import { ApiError, apiGetList, apiPost } from './client';
import {
  timeAgo,
  type AssistantHistoryItemDto,
  type AssistantReplyResponseDto,
} from './backend';

export interface AssistantMessage {
  id: string | number;
  fromMe: boolean;
  text: string;
  time: string;
  /** Set when this turn was escalated to human support. */
  supportTicketId?: string;
}

export interface AssistantReply {
  answer: string;
  escalated: boolean;
  supportConversationId: string | null;
}

/** Ask the platform assistant a question (2–2000 chars). */
export async function askAssistant(question: string): Promise<AssistantReply> {
  const dto = await apiPost<AssistantReplyResponseDto>('/api/assistant/ask', { question });
  return {
    answer: dto.answer,
    escalated: dto.escalated,
    supportConversationId: dto.supportConversationId ?? null,
  };
}

/** The caller's conversation with the assistant, oldest first. */
export async function getAssistantHistory(limit = 50): Promise<AssistantMessage[]> {
  const dtos = await apiGetList<AssistantHistoryItemDto>(`/api/assistant/history?limit=${limit}`);
  return dtos.map((dto) => ({
    id: dto.id,
    fromMe: dto.isFromUser,
    text: dto.content,
    time: timeAgo(dto.createdAt),
    supportTicketId: dto.supportTicketId ?? undefined,
  }));
}

/**
 * Friendly copy for AI endpoint failures. The backend's 400s carry a clear
 * message in the envelope ("not configured", "unavailable right now"), but the
 * `ai` rate limiter rejects 429s with an empty body — supply our own copy.
 */
export function aiErrorMessage(err: unknown): string {
  if (err instanceof ApiError) {
    if (err.statusCode === 429) return 'You’re sending requests too quickly — please wait a minute and try again.';
    if (err.statusCode === 400) return err.message;
  }
  return 'Something went wrong. Please try again.';
}
