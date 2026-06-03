import type { Config } from 'tailwindcss';
import colors from 'tailwindcss/colors';

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        surface: '#18181b',
        'surface-raised': '#1f1f23',
        border: '#2a2a30',
        brand: colors.indigo,
      },
      fontFamily: {
        mono: ['ui-monospace', 'Cascadia Code', 'Fira Code', 'monospace'],
      },
      boxShadow: {
        card: '0 1px 3px rgba(0,0,0,0.4), 0 1px 2px rgba(0,0,0,0.3)',
        drawer: '-4px 0 24px rgba(0,0,0,0.5)',
      },
    },
  },
  plugins: [],
} satisfies Config;
