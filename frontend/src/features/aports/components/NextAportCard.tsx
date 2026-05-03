import { useQuery } from '@tanstack/react-query';
import { aportsApi } from '../api';
import { formatBRL } from '@/lib/format';

export default function NextAportCard({ onExecute }: { onExecute: () => void }) {
  const { data, isLoading } = useQuery({ queryKey: ['aports', 'next'], queryFn: aportsApi.next });
  if (isLoading) return <div className="card"><p className="text-sm text-muted">Carregando aporte...</p></div>;
  if (!data) return null;

  const tone = data.daysUntil === 0 ? 'border-accent' : data.daysUntil < 0 ? 'border-alert' : 'border-border';
  const label = data.daysUntil === 0 ? 'Hoje é dia de aporte' : data.daysUntil < 0 ? 'Aporte atrasado' : `Próximo aporte em ${data.daysUntil} dias`;

  return (
    <div className={`card border ${tone}`}>
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <p className="label">Próximo aporte</p>
          <p className="text-xl font-semibold">{label}</p>
          <p className="text-sm text-muted">
            {new Date(data.nextDate).toLocaleDateString('pt-BR')} · {formatBRL(data.amountBrl)}
          </p>
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 flex-1">
          {data.suggested.filter((s) => s.amountBrl > 0).map((s) => (
            <div key={s.coinId} className="bg-bg border border-border rounded-md px-3 py-2">
              <p className="text-xs text-muted">{s.symbol}</p>
              <p className="font-mono num text-sm">{formatBRL(s.amountBrl)}</p>
            </div>
          ))}
        </div>
        <button className="btn-primary" onClick={onExecute}>Executei</button>
      </div>
    </div>
  );
}
