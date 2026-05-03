export interface MoverDto { symbol: string; name: string; priceBrl: number; change24hPct: number; }
export interface MarketOverviewDto {
  fearGreedIndex: number;
  fearGreedLabel: string;
  btcDominancePct: number;
  totalMarketCapUsd: number;
  updatedAt: string | null;
  topGainers: MoverDto[];
  topLosers: MoverDto[];
}

export interface OhlcvDto { date: string; open: number; high: number; low: number; close: number; volume: number; }

export interface CoinIndicatorsDto {
  symbol: string;
  ma200: number | null;
  ma200Signal: 'above' | 'below' | 'n/a';
  rsiWeekly14: number | null;
  rsiSignal: 'oversold' | 'overbought' | 'neutral' | 'n/a';
  volatility30dAnnualized: number | null;
  distanceFromMax90dPct: number | null;
  distanceFromMax365dPct: number | null;
  distanceFromMin90dPct: number | null;
}
