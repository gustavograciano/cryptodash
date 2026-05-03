export interface HoldingDto {
  coinId: number;
  symbol: string;
  name: string;
  quantity: number;
  avgCostBrl: number;
  priceBrl: number;
  valueBrl: number;
  change24hPct: number;
  pnlBrl: number;
  pnlPct: number;
  priceUpdatedAt: string | null;
}

export interface AllocationItemDto {
  coinId: number;
  symbol: string;
  currentPct: number;
  targetPct: number;
  deviationPct: number;
  deviationSeverity: 'ok' | 'warn' | 'alert';
}

export interface PortfolioSummaryDto {
  totalBrl: number;
  totalCostBrl: number;
  totalPnlBrl: number;
  totalPnlPct: number;
  change24hBrl: number;
  change24hPct: number;
  holdings: HoldingDto[];
  allocation: AllocationItemDto[];
}

export interface CoinDto {
  id: number;
  symbol: string;
  name: string;
  iconUrl: string | null;
  riskTier: string;
}

export type TransactionType = 'Buy' | 'Sell';

export interface TransactionDto {
  id: number;
  coinId: number;
  symbol: string;
  type: TransactionType;
  quantity: number;
  priceBrl: number;
  totalBrl: number;
  feeBrl: number;
  executedAt: string;
  notes: string | null;
}

export interface CreateTransactionRequest {
  coinId: number;
  type: TransactionType;
  quantity: number;
  priceBrl: number;
  feeBrl?: number;
  executedAt: string;
  notes?: string;
}
