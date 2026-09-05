/**
 * Ângulo do ponteiro, em graus, no sistema do SVG (y cresce para baixo).
 *
 * O arco desenhado é o semicírculo SUPERIOR (`M10,100 A90,90 0 0,1 190,100`), então o
 * ponteiro precisa varrer de 180° (esquerda) a 360° (direita), passando por 270° (cima).
 *
 * A fórmula anterior, `-90 + (value/100)*180`, varria de −90° a +90° — a metade DIREITA do
 * círculo. Com índice 50 ("Neutral") a agulha apontava para o extremo de ganância; com 100
 * apontava para baixo, fora da viewBox.
 */
export const anguloDoPonteiro = (value: number) => 180 + (Math.min(100, Math.max(0, value)) / 100) * 180;

export default function FearGreedGauge({ value, label }: { value: number; label: string }) {
  const angle = anguloDoPonteiro(value);
  const color = value < 25 ? '#ef4444' : value < 45 ? '#f59e0b' : value < 55 ? '#94a3b8' : value < 75 ? '#10b981' : '#22c55e';
  return (
    <div className="flex items-center gap-4">
      <svg viewBox="0 0 200 110" className="w-40">
        <path d="M10,100 A90,90 0 0,1 190,100" stroke="#1f242e" strokeWidth="14" fill="none" />
        <path d="M10,100 A90,90 0 0,1 190,100" stroke={color} strokeWidth="14" fill="none"
          strokeDasharray={`${(value / 100) * 283} 283`} strokeLinecap="round" />
        <line x1="100" y1="100" x2={100 + 70 * Math.cos((angle * Math.PI) / 180)}
          y2={100 + 70 * Math.sin((angle * Math.PI) / 180)} stroke="#e6e8eb" strokeWidth="2" />
        <circle cx="100" cy="100" r="4" fill="#e6e8eb" />
      </svg>
      <div>
        <p className="text-3xl font-mono font-semibold num">{value}</p>
        <p className="text-xs text-muted uppercase tracking-wide">{label}</p>
      </div>
    </div>
  );
}
