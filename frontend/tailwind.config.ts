import type { Config } from 'tailwindcss';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        bg: '#0b0d12',
        panel: '#12151c',
        border: '#1f242e',
        text: '#e6e8eb',
        muted: '#8a93a4',
        ok: '#10b981',
        warn: '#f59e0b',
        alert: '#ef4444',
        accent: '#3b82f6',
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
      },
    },
  },
  plugins: [],
} satisfies Config;
