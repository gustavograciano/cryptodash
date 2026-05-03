import { useQuery } from '@tanstack/react-query';
import { newsApi } from '../api';

export default function DigestCard() {
  const { data, isLoading } = useQuery({ queryKey: ['news', 'digest'], queryFn: newsApi.digestToday });

  return (
    <div className="card">
      <div className="flex items-center justify-between mb-3">
        <h2 className="text-sm uppercase text-muted tracking-wide">Resumo IA do dia</h2>
        {data && <span className="text-xs text-muted">{data.model}</span>}
      </div>
      {isLoading ? (
        <p className="text-sm text-muted">Carregando...</p>
      ) : !data ? (
        <p className="text-sm text-muted">Nenhum resumo disponível ainda. O job roda às 7h ou quando há notícias.</p>
      ) : (
        <pre className="whitespace-pre-wrap text-sm leading-relaxed font-sans">{data.summaryMd}</pre>
      )}
    </div>
  );
}
