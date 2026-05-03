import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { portfolioApi } from './api';
import { formatBRL, formatPct } from '@/lib/format';
import HoldingsCard from './components/HoldingsCard';
import AllocationDonut from './components/AllocationDonut';
import DeviationTable from './components/DeviationTable';
import TransactionModal from './components/TransactionModal';
import { useAuthStore } from '@/stores/authStore';
import MarketOverviewCard from '@/features/market/components/MarketOverviewCard';
import CoinSignalsTable from '@/features/market/components/CoinSignalsTable';
import PriceChartModal from '@/features/market/components/PriceChartModal';
import NextAportCard from '@/features/aports/components/NextAportCard';
import AportExecuteModal from '@/features/aports/components/AportExecuteModal';
import DigestCard from '@/features/news/components/DigestCard';
import NewsFeed from '@/features/news/components/NewsFeed';

export default function PortfolioPage() {
  const [openModal, setOpenModal] = useState(false);
  const [chartSymbol, setChartSymbol] = useState<string | null>(null);
  const [aportModal, setAportModal] = useState(false);
  const logout = useAuthStore((s) => s.logout);
  const { data, isLoading, error } = useQuery({
    queryKey: ['portfolio', 'summary'],
    queryFn: portfolioApi.summary,
    refetchInterval: 60_000,
  });

  return (
    <div className="min-h-screen">
      <header className="border-b border-border">
        <div className="max-w-6xl mx-auto px-4 py-4 flex items-center justify-between">
          <div>
            <h1 className="text-lg font-semibold">CryptoDash</h1>
            {data && (
              <p className="text-xs text-muted num">
                Total: <span className="font-mono text-text">{formatBRL(data.totalBrl)}</span>
                {' · '}
                <span className={data.change24hBrl >= 0 ? 'text-ok' : 'text-alert'}>
                  {data.change24hBrl >= 0 ? '+' : ''}{formatBRL(data.change24hBrl)} ({formatPct(data.change24hPct)} 24h)
                </span>
              </p>
            )}
          </div>
          <div className="flex items-center gap-2">
            <button className="btn-primary" onClick={() => setOpenModal(true)}>+ Transação</button>
            <button className="btn-ghost" onClick={logout}>Sair</button>
          </div>
        </div>
      </header>

      <main className="max-w-6xl mx-auto px-4 py-6 space-y-6">
        {isLoading && <p className="text-sm text-muted">Carregando...</p>}
        {error && <p className="text-sm text-alert">Falha ao carregar portfolio.</p>}
        {data && (
          <>
            <NextAportCard onExecute={() => setAportModal(true)} />

            <section className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <Stat label="Patrimônio total" value={formatBRL(data.totalBrl)} />
              <Stat label="Custo total" value={formatBRL(data.totalCostBrl)} muted />
              <Stat
                label="P&L total"
                value={`${formatBRL(data.totalPnlBrl)} (${formatPct(data.totalPnlPct)})`}
                tone={data.totalPnlBrl >= 0 ? 'ok' : 'alert'}
              />
            </section>

            <section className="grid grid-cols-1 lg:grid-cols-3 gap-4">
              <div className="lg:col-span-2"><HoldingsCard holdings={data.holdings} /></div>
              <AllocationDonut holdings={data.holdings} />
            </section>

            <section><DeviationTable items={data.allocation} /></section>

            <section><MarketOverviewCard /></section>

            {data.holdings.length > 0 && (
              <section>
                <CoinSignalsTable
                  symbols={data.holdings.map((h) => h.symbol)}
                  onSelect={setChartSymbol}
                />
              </section>
            )}

            <section><DigestCard /></section>
            <section><NewsFeed /></section>
          </>
        )}
      </main>

      {openModal && <TransactionModal onClose={() => setOpenModal(false)} />}
      {chartSymbol && <PriceChartModal symbol={chartSymbol} onClose={() => setChartSymbol(null)} />}
      {aportModal && <AportExecuteModal onClose={() => setAportModal(false)} />}
    </div>
  );
}

function Stat({ label, value, tone, muted }: { label: string; value: string; tone?: 'ok' | 'alert'; muted?: boolean }) {
  const toneCls = tone === 'ok' ? 'text-ok' : tone === 'alert' ? 'text-alert' : muted ? 'text-muted' : 'text-text';
  return (
    <div className="card">
      <p className="text-xs uppercase text-muted tracking-wide">{label}</p>
      <p className={`text-2xl font-semibold mt-1 num font-mono ${toneCls}`}>{value}</p>
    </div>
  );
}
