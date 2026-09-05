import { describe, expect, it } from 'vitest';
import { PRECO_OBSOLETO_MINUTOS, idadeDoPreco, paraDatetimeLocal } from './format';

/**
 * O ciclo real do produto: o valor vai para um `<input type="datetime-local">`, o usuário
 * confirma, e o submit faz `new Date(valor).toISOString()`. O instante que chega ao servidor
 * tem de ser o mesmo que o usuário escolheu.
 *
 * O bug anterior usava `toISOString().slice(0,16)` como valor inicial — hora UTC dentro de um
 * campo que o navegador lê como local. Em UTC-3 gravava tudo 3 horas no futuro.
 */
describe('paraDatetimeLocal', () => {
  const submeter = (valorDoInput: string) => new Date(valorDoInput).toISOString();

  it('preserva o instante no round-trip input -> submit', () => {
    const agora = new Date(2026, 8, 5, 14, 30, 0, 0); // 5/set/2026 14:30 local
    const noInput = paraDatetimeLocal(agora);
    const enviado = new Date(submeter(noInput));

    expect(enviado.getFullYear()).toBe(2026);
    expect(enviado.getMonth()).toBe(8);
    expect(enviado.getDate()).toBe(5);
    expect(enviado.getHours()).toBe(14);
    expect(enviado.getMinutes()).toBe(30);
  });

  it('exibe a hora LOCAL, nao a UTC', () => {
    const d = new Date(2026, 8, 5, 14, 30);
    expect(paraDatetimeLocal(d)).toBe('2026-09-05T14:30');
  });

  it('nao adianta o instante — a regressao que existia', () => {
    const agora = new Date(2026, 8, 5, 14, 30);
    const desvioMs = new Date(submeter(paraDatetimeLocal(agora))).getTime() - agora.getTime();
    expect(desvioMs).toBe(0);
  });

  it('a virada de ano nao escorrega para o ano fiscal seguinte', () => {
    // 31/12 às 22h local: com o bug, em UTC-3 isto virava 01/01 do ano seguinte.
    const reveillon = new Date(2026, 11, 31, 22, 0);
    const enviado = new Date(submeter(paraDatetimeLocal(reveillon)));

    expect(enviado.getFullYear()).toBe(2026);
    expect(enviado.getMonth()).toBe(11);
    expect(enviado.getDate()).toBe(31);
  });

  it('zero-padding de mes, dia, hora e minuto', () => {
    expect(paraDatetimeLocal(new Date(2026, 0, 3, 7, 5))).toBe('2026-01-03T07:05');
  });

  it('o formato antigo produzia valor diferente do local — prova do bug', () => {
    const d = new Date(2026, 8, 5, 14, 30);
    const formatoAntigo = d.toISOString().slice(0, 16);
    const desvioHoras = -d.getTimezoneOffset() / 60;

    // Só há divergência fora do UTC; em UTC o bug era invisível, e é por isso que passou.
    if (desvioHoras !== 0) expect(formatoAntigo).not.toBe(paraDatetimeLocal(d));
    else expect(formatoAntigo).toBe(paraDatetimeLocal(d));
  });
});

describe('idadeDoPreco', () => {
  const minutosAtras = (m: number) => new Date(Date.now() - m * 60_000).toISOString();

  it('sem cotação é tratado como obsoleto', () => {
    expect(idadeDoPreco(null).obsoleto).toBe(true);
    expect(idadeDoPreco(undefined).texto).toBe('sem cotação');
  });

  it('preço recente não é obsoleto', () => {
    const r = idadeDoPreco(minutosAtras(5));
    expect(r.obsoleto).toBe(false);
    expect(r.texto).toBe('há 5 min');
  });

  it('acima do limiar vira obsoleto', () => {
    // O job roda a cada 15 min: 45 min sem atualizar significa que ele parou.
    expect(idadeDoPreco(minutosAtras(45)).obsoleto).toBe(true);
  });

  it('a fronteira do limiar é obsoleta', () => {
    expect(idadeDoPreco(minutosAtras(PRECO_OBSOLETO_MINUTOS)).obsoleto).toBe(true);
    expect(idadeDoPreco(minutosAtras(PRECO_OBSOLETO_MINUTOS - 1)).obsoleto).toBe(false);
  });

  it('escala para horas e dias', () => {
    expect(idadeDoPreco(minutosAtras(120)).texto).toBe('há 2 h');
    expect(idadeDoPreco(minutosAtras(60 * 24 * 3)).texto).toBe('há 3 d');
  });
});
