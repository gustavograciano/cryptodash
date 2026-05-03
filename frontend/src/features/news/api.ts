import { apiClient } from '@/lib/apiClient';
import type { AiDigestDto, NewsItemDto } from './types';

export const newsApi = {
  digestToday: () => apiClient.get<AiDigestDto>('/news/digest/today').then((r) => r.data).catch(() => null),
  feed: (coin?: string, take = 20) =>
    apiClient.get<NewsItemDto[]>('/news/feed', { params: { coin, take } }).then((r) => r.data),
};
