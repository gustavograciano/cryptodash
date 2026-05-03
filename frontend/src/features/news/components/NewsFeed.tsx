import { useQuery } from '@tanstack/react-query';
import { newsApi } from '../api';

export default function NewsFeed() {
  const { data = [], isLoading } = useQuery({ queryKey: ['news', 'feed'], queryFn: () => newsApi.feed(undefined, 15) });
  return (
    <div className="card">
      <h2 className="text-sm uppercase text-muted tracking-wide mb-3">Notícias recentes</h2>
      {isLoading ? (
        <p className="text-sm text-muted">Carregando...</p>
      ) : data.length === 0 ? (
        <p className="text-sm text-muted">Nenhuma notícia em cache.</p>
      ) : (
        <ul className="divide-y divide-border">
          {data.map((n) => (
            <li key={n.id} className="py-2">
              <a href={n.url} target="_blank" rel="noreferrer" className="text-sm hover:text-accent">
                {n.title}
              </a>
              <p className="text-xs text-muted mt-0.5">
                {n.source} · {new Date(n.publishedAt).toLocaleString('pt-BR')}
                {n.coinsMentioned.length > 0 && <span> · {n.coinsMentioned.join(', ')}</span>}
              </p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
