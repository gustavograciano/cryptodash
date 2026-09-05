import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Area, AreaChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { marketApi } from '../api';
import { formatBRL } from '@/lib/format';

type Range = '30d' | '90d' | '1y';

export default function PriceChartModal({ symbol, onClose }: { symbol: string; onClose: () => void }) {
  const [range, setRange] = useState<Range>('90d');
  const { data = [], isLoading, isError } = useQuery({
    queryKey: ['history', symbol, range],
    queryFn: () => marketApi.history(symbol, range),
  });
  const chartData = data.map((d) => ({ date: d.date, close: Number(d.close) }));

  return (
    <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center px-4">
      <div className="card w-full max-w-3xl">
        <header className="flex items-center justify-between mb-3">
          <h3 className="text-lg font-semibold">{symbol} — preço</h3>
          <button onClick={onClose} className="text-muted hover:text-text">×</button>
        </header>
        <div className="flex gap-1 mb-3">
          {(['30d', '90d', '1y'] as Range[]).map((r) => (
            <button key={r} onClick={() => setRange(r)}
              className={`text-xs px-3 py-1 rounded-md ${r === range ? 'bg-accent text-white' : 'bg-bg border border-border text-muted'}`}>
              {r}
            </button>
          ))}
        </div>
        <div className="h-72">
          {isLoading ? (
            <p className="text-sm text-muted">Carregando...</p>
          ) : isError ? (
            // Antes, falha de API caía no ramo de lista vazia e mandava o usuário aguardar
            // um job que não ia resolver nada.
            <p className="text-sm text-alert">
              Não foi possível carregar o histórico. Verifique a conexão e tente de novo.
            </p>
          ) : chartData.length === 0 ? (
            <p className="text-sm text-muted">Sem histórico — aguarde o job rodar.</p>
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={chartData}>
                <defs>
                  <linearGradient id="g" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#3b82f6" stopOpacity={0.4} />
                    <stop offset="100%" stopColor="#3b82f6" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <XAxis dataKey="date" tick={{ fill: '#8a93a4', fontSize: 11 }} />
                <YAxis tick={{ fill: '#8a93a4', fontSize: 11 }}
                  tickFormatter={(v) => `R$${(v / 1000).toFixed(1)}k`} width={70} />
                <Tooltip contentStyle={{ background: '#12151c', border: '1px solid #1f242e', borderRadius: 8 }}
                  formatter={(v: number) => [formatBRL(v), 'Close']} />
                <Area type="monotone" dataKey="close" stroke="#3b82f6" fill="url(#g)" strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>
    </div>
  );
}
