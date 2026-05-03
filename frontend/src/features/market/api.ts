import { apiClient } from '@/lib/apiClient';
import type { CoinIndicatorsDto, MarketOverviewDto, OhlcvDto } from './types';

export const marketApi = {
  overview: () => apiClient.get<MarketOverviewDto>('/market/overview').then((r) => r.data),
  history: (symbol: string, range: '30d' | '90d' | '1y' = '90d') =>
    apiClient.get<OhlcvDto[]>(`/market/coins/${symbol}/history`, { params: { range } }).then((r) => r.data),
  indicators: (symbol: string) =>
    apiClient.get<CoinIndicatorsDto>(`/market/coins/${symbol}/indicators`).then((r) => r.data),
};
