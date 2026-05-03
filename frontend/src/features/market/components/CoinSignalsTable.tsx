import { useQueries } from '@tanstack/react-query';
import { marketApi } from '../api';
import { formatPct } from '@/lib/format';
import type { CoinIndicatorsDto } from '../types';

const sigClass: Record<string, string> = {
  above: 'text-ok',
  below: 'text-alert',
  oversold: 'text-ok',
  overbought: 'text-alert',
  neutral: 'text-muted',
  'n/a': 'text-muted',
};

export default function CoinSignalsTable({ symbols, onSelect }: { symbols: string[]; onSelect: (s: string) => void }) {
  const queries = useQueries({
    queries: symbols.map((s) => ({
      queryKey: ['indicators', s],
      queryFn: () => marketApi.indicators(s),
      staleTime: 5 * 60_000,
    })),
  });

  return (
    <div className="card">
      <h2 className="text-sm uppercase text-muted tracking-wide mb-3">Sinais técnicos</h2>
      <table className="w-full text-sm num">
        <thead className="text-muted text-xs uppercase">
          <tr>
            <th className="text-left font-normal py-2">Moeda</th>
            <th className="text-right font-normal">MA200</th>
            <th className="text-right font-normal">RSI 14w</th>
            <th className="text-right font-normal">Vol. 30d</th>
            <th className="text-right font-normal">Dist. máx 90d</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {queries.map((q, i) => {
            const sym = symbols[i];
            const ind = q.data as CoinIndicatorsDto | undefined;
            return (
              <tr key={sym} className="border-t border-border">
                <td className="py-2 font-medium">{sym}</td>
                <td className={`text-right font-mono ${sigClass[ind?.ma200Signal ?? 'n/a']}`}>
                  {ind?.ma200Signal ?? '—'}
                </td>
                <td className={`text-right font-mono ${sigClass[ind?.rsiSignal ?? 'n/a']}`}>
                  {ind?.rsiWeekly14 != null ? ind.rsiWeekly14.toFixed(1) : '—'}
                  <span className="ml-1 text-xs text-muted">{ind?.rsiSignal && ind.rsiSignal !== 'n/a' ? `(${ind.rsiSignal})` : ''}</span>
                </td>
                <td className="text-right font-mono">{ind?.volatility30dAnnualized != null ? formatPct(ind.volatility30dAnnualized) : '—'}</td>
                <td className="text-right font-mono">{ind?.distanceFromMax90dPct != null ? `-${formatPct(ind.distanceFromMax90dPct)}` : '—'}</td>
                <td className="text-right">
                  <button className="btn-ghost text-xs px-2 py-1" onClick={() => onSelect(sym)}>Gráfico</button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
