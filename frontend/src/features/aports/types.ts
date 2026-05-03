export interface AportPlanDto { id: number; amountBrl: number; dayOfMonth: number; isActive: boolean; }
export interface AportAllocationDto {
  coinId: number;
  symbol: string;
  amountBrl: number;
  currentValueBrl: number;
  targetValueBrl: number;
  gapBrl: number;
}
export interface NextAportDto {
  planId: number | null;
  amountBrl: number;
  nextDate: string;
  daysUntil: number;
  suggested: AportAllocationDto[];
}
