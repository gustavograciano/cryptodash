import { formatPct } from '@/lib/format';
import type { AllocationItemDto } from '../types';

const sevClass: Record<string, string> = { ok: 'pill-ok', warn: 'pill-warn', alert: 'pill-alert' };
const sevLabel: Record<string, string> = { ok: 'OK', warn: 'Monitorar', alert: 'Rebalancear' };

export default function DeviationTable({ items }: { items: AllocationItemDto[] }) {
  return (
    <div className="card">
      <h2 className="text-sm uppercase text-muted tracking-wide mb-3">Desvio vs alvo</h2>
      <table className="w-full text-sm num">
        <thead className="text-muted text-xs uppercase">
          <tr>
            <th className="text-left font-normal py-2">Moeda</th>
            <th className="text-right font-normal">Atual</th>
            <th className="text-right font-normal">Alvo</th>
            <th className="text-right font-normal" title="Desvio relativo ao alvo: (atual − alvo) / alvo">Desvio rel. ao alvo</th>
            <th className="text-right font-normal">Status</th>
          </tr>
        </thead>
        <tbody>
          {items.map((a) => (
            <tr key={a.coinId} className="border-t border-border">
              <td className="py-2 font-medium">{a.symbol}</td>
              <td className="text-right font-mono">{formatPct(a.currentPct)}</td>
              <td className="text-right font-mono text-muted">{formatPct(a.targetPct)}</td>
              <td className={`text-right font-mono ${a.deviationPct >= 0 ? 'text-ok' : 'text-alert'}`}>
                {a.deviationPct > 0 ? '+' : ''}{formatPct(a.deviationPct)}
              </td>
              <td className="text-right">
                <span className={sevClass[a.deviationSeverity]}>{sevLabel[a.deviationSeverity]}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
