import { apiClient } from '@/lib/apiClient';
import type { CoinDto, CreateTransactionRequest, PortfolioSummaryDto, TransactionDto } from './types';

export const portfolioApi = {
  summary: () => apiClient.get<PortfolioSummaryDto>('/portfolio/summary').then((r) => r.data),
  coins: () => apiClient.get<CoinDto[]>('/coins').then((r) => r.data),
  listTransactions: () => apiClient.get<TransactionDto[]>('/portfolio/transactions').then((r) => r.data),
  createTransaction: (req: CreateTransactionRequest) =>
    apiClient.post<TransactionDto>('/portfolio/transactions', req).then((r) => r.data),
  deleteTransaction: (id: number) => apiClient.delete(`/portfolio/transactions/${id}`),
};
