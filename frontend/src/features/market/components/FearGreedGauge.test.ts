import { describe, expect, it } from 'vitest';
import { anguloDoPonteiro } from './FearGreedGauge';

/**
 * O arco desenhado é o semicírculo SUPERIOR (`M10,100 A90,90 0 0,1 190,100`), com centro em
 * (100,100) e raio 90. No SVG o eixo y cresce para BAIXO, então varrer esse arco da esquerda
 * para a direita significa ir de 180° a 360°, passando por 270° (que aponta para cima).
 *
 * A fórmula anterior varria de −90° a +90° — a metade DIREITA do círculo.
 */
describe('anguloDoPonteiro', () => {
  const ponta = (value: number) => {
    const a = (anguloDoPonteiro(value) * Math.PI) / 180;
    return { x: 100 + 70 * Math.cos(a), y: 100 + 70 * Math.sin(a) };
  };

  it('índice 0 (medo extremo) aponta para a esquerda', () => {
    const p = ponta(0);
    expect(p.x).toBeCloseTo(30, 5);
    expect(p.y).toBeCloseTo(100, 5);
  });

  it('índice 50 (neutro) aponta para CIMA, não para a ganância', () => {
    const p = ponta(50);
    expect(p.x).toBeCloseTo(100, 5);
    expect(p.y).toBeCloseTo(30, 5);   // acima do centro
  });

  it('índice 100 (ganância extrema) aponta para a direita', () => {
    const p = ponta(100);
    expect(p.x).toBeCloseTo(170, 5);
    expect(p.y).toBeCloseTo(100, 5);
  });

  it('a ponta nunca sai da metade superior da viewBox', () => {
    // A viewBox tem 110 de altura; com a fórmula antiga, o índice 100 mandava a agulha
    // para y = 170 — fora do desenho.
    for (let v = 0; v <= 100; v++) {
      const p = ponta(v);
      expect(p.y).toBeLessThanOrEqual(100.0001);
      expect(p.y).toBeGreaterThanOrEqual(0);
      expect(p.x).toBeGreaterThanOrEqual(0);
      expect(p.x).toBeLessThanOrEqual(200);
    }
  });

  it('o ângulo cresce de forma monótona com o índice', () => {
    for (let v = 1; v <= 100; v++) {
      expect(anguloDoPonteiro(v)).toBeGreaterThan(anguloDoPonteiro(v - 1));
    }
  });

  it('valores fora da faixa são presos aos extremos', () => {
    // O índice vem de fonte externa; um valor absurdo não pode jogar a agulha para fora.
    expect(anguloDoPonteiro(-30)).toBe(anguloDoPonteiro(0));
    expect(anguloDoPonteiro(999)).toBe(anguloDoPonteiro(100));
  });

  it('a fórmula antiga colocava o neutro na ganância — prova do bug', () => {
    const antiga = (value: number) => -90 + (value / 100) * 180;
    const a = (antiga(50) * Math.PI) / 180;
    const x = 100 + 70 * Math.cos(a);

    expect(x).toBeCloseTo(170, 5);          // apontava para a direita, extremo de ganância
    expect(x).not.toBeCloseTo(ponta(50).x, 1);
  });
});
