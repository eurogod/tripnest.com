import type { ChatMessage, Conversation } from '../types';
import { getSession } from '../store/authStore';
import { apiGet, apiGetList, apiPatch, apiPost, apiUpload } from './client';
import {
  mapConversation,
  mapMessage,
  timeAgo,
  type ConversationResponseDto,
  type MessageResponseDto,
  type PagedResultDto,
  type SuggestedReplyResponseDto,
} from './backend';
import { getPropertyById } from './properties';
import { getProviderDirectory } from './services';
import { getContact } from '../lib/chatContacts';

/**
 * Conversations for the signed-in user, enriched client-side because the
 * DTO carries ids only: property-linked chats are named after the property,
 * direct chats after the locally-known contact or the public provider
 * directory, and the newest message (API pages newest-first) supplies the
 * sidebar preview, timestamp and unread flag. Enrichment is best-effort —
 * on any failure the bare conversation still renders.
 */
export async function getConversations(): Promise<Conversation[]> {
  const me = getSession()?.userId ?? '';
  const dtos = await apiGetList<ConversationResponseDto>('/api/chat/conversations/mine');
  // Only direct (non-property) chats need the agents/caretakers directory.
  const directory = dtos.some((d) => !d.propertyId) ? await getProviderDirectory() : {};
  return Promise.all(dtos.map(async (dto) => {
    const conversation = mapConversation(dto, me);
    const otherId = dto.user1Id === me ? dto.user2Id : dto.user1Id;
    const known = getContact(otherId) ?? directory[otherId];
    if (known) {
      conversation.name = known.name;
      if (known.role) conversation.role = known.role;
    }
    try {
      const [latest, property] = await Promise.all([
        apiGet<PagedResultDto<MessageResponseDto>>(
          `/api/chat/conversations/${dto.conversationId}/messages?page=1&pageSize=1`,
        ).then((p) => p.items[0]),
        dto.propertyId ? getPropertyById(dto.propertyId) : Promise.resolve(undefined),
      ]);
      // A known person beats the property title; the title fills the gap otherwise.
      if (property && !known) conversation.name = property.title;
      if (latest) {
        conversation.lastMessage = latest.content;
        conversation.time = timeAgo(latest.createdAt);
        conversation.unread = latest.senderId !== me && !latest.isRead ? 1 : 0;
      }
    } catch { /* enrichment only */ }
    return conversation;
  }));
}

export async function getMessages(conversationId: string | number): Promise<ChatMessage[]> {
  const me = getSession()?.userId ?? '';
  const page = await apiGet<PagedResultDto<MessageResponseDto>>(
    `/api/chat/conversations/${conversationId}/messages?page=1&pageSize=100`,
  );
  // The API orders newest-first; the thread renders oldest-first.
  return [...page.items].reverse().map((dto) => mapMessage(dto, me));
}

/** Open (or reuse) a conversation with another user; returns its id. */
export async function startConversation(otherUserId: string, propertyId?: string): Promise<string> {
  const dto = await apiPost<ConversationResponseDto>('/api/chat/conversations', {
    otherUserId,
    propertyId,
  });
  return dto.conversationId;
}

export async function sendMessage(conversationId: string | number, body: string): Promise<ChatMessage> {
  const me = getSession()?.userId ?? '';
  const dto = await apiPost<MessageResponseDto>(
    `/api/chat/conversations/${conversationId}/messages`,
    { body },
  );
  return mapMessage(dto, me);
}

/**
 * Send an attachment — an image, a voice note or a document (≤25 MB, validated server-side).
 * The backend infers the message type from the file and broadcasts it over SignalR like text.
 */
export async function sendAttachment(
  conversationId: string | number,
  file: File,
  caption?: string,
): Promise<ChatMessage> {
  const me = getSession()?.userId ?? '';
  const form = new FormData();
  form.append('file', file);
  if (caption) form.append('caption', caption);
  const dto = await apiUpload<MessageResponseDto>(
    `/api/chat/conversations/${conversationId}/messages/attachment`,
    form,
  );
  return mapMessage(dto, me);
}

/** AI-drafted reply grounded in the listing and recent messages, for the user to edit. */
export async function suggestReply(conversationId: string | number): Promise<string> {
  const dto = await apiPost<SuggestedReplyResponseDto>(
    `/api/chat/conversations/${conversationId}/suggest-reply`,
  );
  return dto.reply;
}

/** Clear the unread state server-side when a conversation is opened. */
export function markConversationRead(conversationId: string | number): Promise<unknown> {
  return apiPatch(`/api/chat/conversations/${conversationId}/mark-read`);
}

// saved chat when conversation is opened or closed
export function saveConversation(conversationId: string | number, saved: boolean) : Promise<{ id: string; saved: boolean } | null>{
  return apiPatch(`/api/chat/conversation/${conversationId}/saved`, {saved})
}
