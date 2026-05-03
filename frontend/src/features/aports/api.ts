import { apiClient } from '@/lib/apiClient';
import type { AportAllocationDto, AportPlanDto, NextAportDto } from './types';

export const aportsApi = {
  plan: () => apiClient.get<AportPlanDto>('/aports/plan').then((r) => r.data).catch(() => null),
  next: () => apiClient.get<NextAportDto>('/aports/next').then((r) => r.data).catch(() => null),
  rebalance: (amountBrl?: number) =>
    apiClient.get<AportAllocationDto[]>('/portfolio/rebalance-suggestion', { params: { amountBrl } }).then((r) => r.data),
  execute: (req: { planId: number; amountBrl: number; executedAt: string; allocations: AportAllocationDto[] }) =>
    apiClient.post('/aports/execute', req).then((r) => r.data),
  updatePlan: (req: { amountBrl: number; dayOfMonth: number; isActive: boolean }) =>
    apiClient.put<AportPlanDto>('/aports/plan', req).then((r) => r.data),
};
