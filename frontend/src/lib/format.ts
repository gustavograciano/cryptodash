const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const pct = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const cryptoFmt = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 8 });

export const formatBRL = (v: number) => brl.format(v ?? 0);
export const formatPct = (v: number) => `${pct.format(v ?? 0)}%`;
export const formatCrypto = (v: number) => cryptoFmt.format(v ?? 0);
export const cls = (cond: boolean, t: string, f = '') => (cond ? t : f);

/**
 * Valor para `<input type="datetime-local">` a partir dos componentes LOCAIS da data.
 *
 * O input interpreta o valor como hora local. Usar `toISOString().slice(0,16)` coloca ali a
 * hora UTC, então o campo exibe a hora errada e, no submit, `new Date(valor).toISOString()`
 * reinterpreta esse valor como local e converte de novo — somando o offset do fuso.
 * Em UTC-3 isso gravava todo ExecutedAt 3 horas no futuro, e uma compra em 31/12 às 22h
 * caía em 01/01: ano fiscal errado.
 */
export const paraDatetimeLocal = (d: Date = new Date()) => {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
};

/** Limiar a partir do qual o preço é tratado como obsoleto na interface. */
export const PRECO_OBSOLETO_MINUTOS = 30;

/**
 * Idade legível do preço, com sinalização de obsolescência.
 *
 * O DTO carrega `priceUpdatedAt` desde sempre e a interface nunca o exibia: se o job de preço
 * parava, o dashboard seguia mostrando números com aparência normal por tempo indefinido.
 */
export const idadeDoPreco = (iso?: string | null) => {
  if (!iso) return { texto: 'sem cotação', obsoleto: true };

  const minutos = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 60000));
  const obsoleto = minutos >= PRECO_OBSOLETO_MINUTOS;

  if (minutos < 1) return { texto: 'agora', obsoleto };
  if (minutos < 60) return { texto: `há ${minutos} min`, obsoleto };

  const horas = Math.floor(minutos / 60);
  if (horas < 24) return { texto: `há ${horas} h`, obsoleto };
  return { texto: `há ${Math.floor(horas / 24)} d`, obsoleto };
};
