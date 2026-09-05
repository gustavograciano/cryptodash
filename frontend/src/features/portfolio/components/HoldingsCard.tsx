import { formatBRL, formatCrypto, formatPct, idadeDoPreco } from '@/lib/format';
import type { HoldingDto } from '../types';

export default function HoldingsCard({ holdings }: { holdings: HoldingDto[] }) {
  return (
    <div className="card">
      <h2 className="text-sm uppercase text-muted tracking-wide mb-3">Posições</h2>
      {holdings.length === 0 ? (
        <p className="text-sm text-muted">Nenhuma posição. Registre uma transação para começar.</p>
      ) : (
        <table className="w-full text-sm num">
          <thead className="text-muted text-xs uppercase">
            <tr>
              <th className="text-left font-normal py-2">Moeda</th>
              <th className="text-right font-normal">Qtd</th>
              <th className="text-right font-normal">Preço</th>
              <th className="text-right font-normal">Valor</th>
              <th className="text-right font-normal">P&L</th>
            </tr>
          </thead>
          <tbody>
            {holdings.map((h) => (
              <tr key={h.coinId} className="border-t border-border">
                <td className="py-2 font-medium">{h.symbol}<span className="text-muted ml-2 text-xs">{h.name}</span></td>
                <td className="text-right font-mono">{formatCrypto(h.quantity)}</td>
                <td className="text-right font-mono">
                  {formatBRL(h.priceBrl)}
                  {(() => {
                    const { texto, obsoleto } = idadeDoPreco(h.priceUpdatedAt);
                    return (
                      <span
                        className={`block text-xs ${obsoleto ? 'text-alert font-semibold' : 'text-muted'}`}
                        title={h.priceUpdatedAt ?? 'sem cotação registrada'}
                      >
                        {obsoleto ? `⚠ ${texto}` : texto}
                      </span>
                    );
                  })()}
                </td>
                <td className="text-right font-mono">{formatBRL(h.valueBrl)}</td>
                <td className={`text-right font-mono ${h.pnlBrl >= 0 ? 'text-ok' : 'text-alert'}`}>
                  {formatBRL(h.pnlBrl)} <span className="text-xs">({formatPct(h.pnlPct)})</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
