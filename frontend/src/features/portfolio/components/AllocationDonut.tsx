import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import type { HoldingDto } from '../types';

const COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#06b6d4', '#ec4899', '#22c55e'];

export default function AllocationDonut({ holdings }: { holdings: HoldingDto[] }) {
  const data = holdings.map((h) => ({ name: h.symbol, value: Number(h.valueBrl) }));
  const total = data.reduce((acc, d) => acc + d.value, 0);
  return (
    <div className="card">
      <h2 className="text-sm uppercase text-muted tracking-wide mb-3">Alocação atual</h2>
      {total === 0 ? (
        <p className="text-sm text-muted">Sem dados para exibir.</p>
      ) : (
        <div className="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie data={data} dataKey="value" innerRadius={56} outerRadius={92} paddingAngle={2} stroke="none">
                {data.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
              </Pie>
              <Tooltip
                contentStyle={{ background: '#12151c', border: '1px solid #1f242e', borderRadius: 8 }}
                formatter={(v: number, n) => [`${((v / total) * 100).toFixed(1)}%`, n as string]}
              />
            </PieChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}
