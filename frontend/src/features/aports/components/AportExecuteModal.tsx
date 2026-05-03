import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { aportsApi } from '../api';
import { formatBRL } from '@/lib/format';

export default function AportExecuteModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const { data: next } = useQuery({ queryKey: ['aports', 'next'], queryFn: aportsApi.next });
  const [executedAt, setExecutedAt] = useState(new Date().toISOString().slice(0, 16));
  const [error, setError] = useState<string | null>(null);

  const m = useMutation({
    mutationFn: aportsApi.execute,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['portfolio'] });
      qc.invalidateQueries({ queryKey: ['aports'] });
      onClose();
    },
    onError: (e: any) => setError(e?.response?.data?.error ?? 'Erro ao executar aporte'),
  });

  if (!next || next.planId == null) return null;
  const total = next.suggested.reduce((acc, s) => acc + s.amountBrl, 0);

  return (
    <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center px-4">
      <div className="card w-full max-w-md space-y-3">
        <header className="flex items-center justify-between">
          <h3 className="text-lg font-semibold">Executar aporte</h3>
          <button onClick={onClose} className="text-muted hover:text-text">×</button>
        </header>
        <p className="text-sm text-muted">
          Confirme as compras realizadas no Itaú íon. Cada linha gera uma transaction.
        </p>
        <div>
          <label className="label">Data/hora da execução</label>
          <input type="datetime-local" className="input" value={executedAt} onChange={(e) => setExecutedAt(e.target.value)} />
        </div>
        <table className="w-full text-sm num">
          <thead className="text-muted text-xs uppercase">
            <tr>
              <th className="text-left font-normal py-2">Moeda</th>
              <th className="text-right font-normal">Valor</th>
            </tr>
          </thead>
          <tbody>
            {next.suggested.map((s) => (
              <tr key={s.coinId} className="border-t border-border">
                <td className="py-1.5">{s.symbol}</td>
                <td className="text-right font-mono">{formatBRL(s.amountBrl)}</td>
              </tr>
            ))}
            <tr className="border-t border-border font-semibold">
              <td className="py-1.5">Total</td>
              <td className="text-right font-mono">{formatBRL(total)}</td>
            </tr>
          </tbody>
        </table>
        {error && <div className="text-sm text-alert">{error}</div>}
        <div className="flex gap-2 justify-end pt-2">
          <button className="btn-ghost" onClick={onClose}>Cancelar</button>
          <button className="btn-primary" disabled={m.isPending}
            onClick={() => m.mutate({
              planId: next.planId!,
              amountBrl: total,
              executedAt: new Date(executedAt).toISOString(),
              allocations: next.suggested,
            })}>
            {m.isPending ? 'Executando...' : 'Confirmar'}
          </button>
        </div>
      </div>
    </div>
  );
}
