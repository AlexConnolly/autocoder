import type { Config } from 'tailwindcss';
import colors from 'tailwindcss/colors';

export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        surface: 'rgb(var(--color-surface) / <alpha-value>)',
        'surface-raised': 'rgb(var(--color-surface-raised) / <alpha-value>)',
        border: 'rgb(var(--color-border) / <alpha-value>)',
        brand: colors.indigo,
      },
      fontFamily: {
        mono: ['ui-monospace', 'Cascadia Code', 'Fira Code', 'monospace'],
      },
      boxShadow: {
        card: '0 1px 3px var(--color-shadow), 0 1px 2px var(--color-shadow)',
        drawer: '-4px 0 24px var(--color-shadow-drawer)',
      },
    },
  },
  plugins: [],
} satisfies Config;
