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
export interface ExecuteAportResponse {
  aportExecutedId: number;
  transactionIds: number[];
  amountBrl: number;
  /** Verdadeiro quando o aporte já estava registrado e nada novo foi gravado. */
  replayed: boolean;
}
