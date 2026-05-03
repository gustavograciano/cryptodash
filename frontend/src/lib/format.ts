const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const pct = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const cryptoFmt = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 8 });

export const formatBRL = (v: number) => brl.format(v ?? 0);
export const formatPct = (v: number) => `${pct.format(v ?? 0)}%`;
export const formatCrypto = (v: number) => cryptoFmt.format(v ?? 0);
export const cls = (cond: boolean, t: string, f = '') => (cond ? t : f);
