import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { portfolioApi } from '../api';
import type { TransactionType } from '../types';

export default function TransactionModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const { data: coins = [] } = useQuery({ queryKey: ['coins'], queryFn: portfolioApi.coins });

  const [coinId, setCoinId] = useState<number | ''>('');
  const [type, setType] = useState<TransactionType>('Buy');
  const [quantity, setQuantity] = useState('');
  const [priceBrl, setPriceBrl] = useState('');
  const [feeBrl, setFeeBrl] = useState('0');
  const [executedAt, setExecutedAt] = useState(new Date().toISOString().slice(0, 16));
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);

  const m = useMutation({
    mutationFn: portfolioApi.createTransaction,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['portfolio'] });
      qc.invalidateQueries({ queryKey: ['transactions'] });
      onClose();
    },
    onError: (e: any) => setError(e?.response?.data?.error ?? 'Erro ao salvar'),
  });

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (coinId === '') { setError('Escolha uma moeda'); return; }
    m.mutate({
      coinId: Number(coinId),
      type,
      quantity: Number(quantity),
      priceBrl: Number(priceBrl),
      feeBrl: Number(feeBrl) || 0,
      executedAt: new Date(executedAt).toISOString(),
      notes: notes || undefined,
    });
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center px-4">
      <form onSubmit={onSubmit} className="card w-full max-w-md space-y-3">
        <header className="flex items-center justify-between">
          <h3 className="text-lg font-semibold">Nova transação</h3>
          <button type="button" onClick={onClose} className="text-muted hover:text-text">×</button>
        </header>
        <div>
          <label className="label">Moeda</label>
          <select className="input" value={coinId} onChange={(e) => setCoinId(e.target.value ? Number(e.target.value) : '')}>
            <option value="">Selecione...</option>
            {coins.map((c) => <option key={c.id} value={c.id}>{c.symbol} — {c.name}</option>)}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="label">Tipo</label>
            <select className="input" value={type} onChange={(e) => setType(e.target.value as TransactionType)}>
              <option value="Buy">Compra</option>
              <option value="Sell">Venda</option>
            </select>
          </div>
          <div>
            <label className="label">Data/hora</label>
            <input type="datetime-local" className="input" value={executedAt} onChange={(e) => setExecutedAt(e.target.value)} required />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="label">Quantidade</label>
            <input type="number" step="any" className="input" value={quantity} onChange={(e) => setQuantity(e.target.value)} required />
          </div>
          <div>
            <label className="label">Preço unitário (BRL)</label>
            <input type="number" step="0.01" className="input" value={priceBrl} onChange={(e) => setPriceBrl(e.target.value)} required />
          </div>
        </div>
        <div>
          <label className="label">Taxa (BRL)</label>
          <input type="number" step="0.01" className="input" value={feeBrl} onChange={(e) => setFeeBrl(e.target.value)} />
        </div>
        <div>
          <label className="label">Notas (opcional)</label>
          <input type="text" className="input" value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>
        {error && <div className="text-sm text-alert">{error}</div>}
        <div className="flex gap-2 justify-end pt-2">
          <button type="button" className="btn-ghost" onClick={onClose}>Cancelar</button>
          <button type="submit" className="btn-primary" disabled={m.isPending}>
            {m.isPending ? 'Salvando...' : 'Salvar'}
          </button>
        </div>
      </form>
    </div>
  );
}
