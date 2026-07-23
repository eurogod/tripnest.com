import type { ExchangeCategory, ExchangePost, ExchangeReply } from '../types';
import { apiGet, apiGetList, apiPost } from './client';
import {
  mapExchangePost,
  mapExchangeReply,
  type ExchangePostResponseDto,
  type ExchangeReplyResponseDto,
  type PagedResultDto,
} from './backend';

// Owner community board, backed by TripNest.Core's /api/exchange endpoints.
// The list is paged server-side (pinned first, then newest).

export async function getExchangePosts(): Promise<ExchangePost[]> {
  const page = await apiGet<PagedResultDto<ExchangePostResponseDto>>(
    '/api/exchange/posts?page=1&pageSize=50',
  );
  return page.items.map(mapExchangePost);
}

export async function createExchangePost(
  title: string,
  body: string,
  category: ExchangeCategory,
): Promise<ExchangePost> {
  const dto = await apiPost<ExchangePostResponseDto>('/api/exchange/posts', {
    title,
    body,
    category,
  });
  return mapExchangePost(dto);
}

export async function getExchangeReplies(postId: string): Promise<ExchangeReply[]> {
  const dtos = await apiGetList<ExchangeReplyResponseDto>(`/api/exchange/posts/${postId}/replies`);
  return dtos.map(mapExchangeReply);
}

export async function addExchangeReply(postId: string, body: string): Promise<ExchangeReply> {
  const dto = await apiPost<ExchangeReplyResponseDto>(`/api/exchange/posts/${postId}/replies`, {
    body,
  });
  return mapExchangeReply(dto);
}
