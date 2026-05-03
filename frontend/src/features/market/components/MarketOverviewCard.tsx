import { useQuery } from '@tanstack/react-query';
import { marketApi } from '../api';
import { formatBRL, formatPct } from '@/lib/format';
import FearGreedGauge from './FearGreedGauge';
import type { MoverDto } from '../types';

export default function MarketOverviewCard() {
  const { data, isLoading } = useQuery({ queryKey: ['market', 'overview'], queryFn: marketApi.overview, refetchInterval: 5 * 60_000 });

  return (
    <div className="card">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-sm uppercase text-muted tracking-wide">Mercado</h2>
        {data?.updatedAt && (
          <span className="text-xs text-muted">{new Date(data.updatedAt).toLocaleDateString('pt-BR')}</span>
        )}
      </div>
      {isLoading || !data ? (
        <p className="text-sm text-muted">Carregando...</p>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div>
            <p className="label">Fear & Greed</p>
            <FearGreedGauge value={data.fearGreedIndex} label={data.fearGreedLabel} />
          </div>
          <div>
            <p className="label">BTC Dominance</p>
            <p className="text-2xl font-mono num">{formatPct(data.btcDominancePct)}</p>
            <p className="label mt-3">Market cap total</p>
            <p className="text-base font-mono num text-muted">USD {data.totalMarketCapUsd.toLocaleString('en-US')}</p>
          </div>
          <div>
            <MoversList title="Top altas 24h" items={data.topGainers} positive />
            <div className="mt-3"><MoversList title="Top quedas 24h" items={data.topLosers} /></div>
          </div>
        </div>
      )}
    </div>
  );
}

function MoversList({ title, items, positive }: { title: string; items: MoverDto[]; positive?: boolean }) {
  return (
    <div>
      <p className="label">{title}</p>
      <ul className="text-sm space-y-0.5">
        {items.map((m) => (
          <li key={m.symbol} className="flex items-center justify-between">
            <span className="font-medium">{m.symbol}</span>
            <span className="text-muted text-xs num">{formatBRL(m.priceBrl)}</span>
            <span className={`num font-mono ${positive ? 'text-ok' : 'text-alert'}`}>
              {m.change24hPct > 0 ? '+' : ''}{formatPct(m.change24hPct)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
