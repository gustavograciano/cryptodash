import { apiClient } from '@/lib/apiClient';
import type { AportAllocationDto, AportPlanDto, ExecuteAportResponse, NextAportDto } from './types';

export const aportsApi = {
  plan: () => apiClient.get<AportPlanDto>('/aports/plan').then((r) => r.data).catch(() => null),
  next: () => apiClient.get<NextAportDto>('/aports/next').then((r) => r.data).catch(() => null),
  rebalance: (amountBrl?: number) =>
    apiClient.get<AportAllocationDto[]>('/portfolio/rebalance-suggestion', { params: { amountBrl } }).then((r) => r.data),
  // A chave identifica a INTENÇÃO de aporte, não a requisição: reenviar depois de um
  // timeout, ou depois de corrigir a data, tem de reusar a mesma chave para o servidor
  // reconhecer que é a mesma operação e devolver o resultado original em vez de duplicar.
  execute: (
    req: { planId: number; amountBrl: number; executedAt: string; allocations: AportAllocationDto[] },
    idempotencyKey: string
  ) =>
    apiClient
      .post<ExecuteAportResponse>('/aports/execute', req, { headers: { 'Idempotency-Key': idempotencyKey } })
      .then((r) => r.data),
  updatePlan: (req: { amountBrl: number; dayOfMonth: number; isActive: boolean }) =>
    apiClient.put<AportPlanDto>('/aports/plan', req).then((r) => r.data),
};
