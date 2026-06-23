/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // TripNest brand — trust-first teal/green with a warm gold accent.
        brand: {
          50: '#ecfdf5',
          100: '#d1fae5',
          200: '#a7f3d0',
          300: '#6ee7b7',
          400: '#34d399',
          500: '#10b981',
          600: '#0f766e', // primary
          700: '#115e59',
          800: '#134e4a',
          900: '#0c3b38',
        },
        gold: {
          300: '#fcd34d',
          400: '#f5c542',
          500: '#f59e0b',
          600: '#d4af37',
          700: '#b8901f',
        },
        ink: '#111827',
        muted: '#6b7280',
        line: '#e5e7eb',
        surface: '#f9fafb',
        success: '#16a34a',
        warning: '#d97706',
        danger: '#dc2626',
      },
      fontFamily: {
        sans: ['Plus Jakarta Sans', 'Inter', 'system-ui', '-apple-system', 'sans-serif'],
      },
      borderRadius: {
        xl: '14px',
        '2xl': '18px',
      },
      boxShadow: {
        card: '0 1px 3px rgba(0,0,0,.08), 0 1px 2px rgba(0,0,0,.04)',
        soft: '0 6px 20px rgba(0,0,0,.08)',
        lg: '0 12px 40px -10px rgba(0,0,0,.22)',
        glow: '0 8px 24px -10px rgba(15,118,110,.45)',
      },
      keyframes: {
        shimmer: { '100%': { transform: 'translateX(100%)' } },
        'fade-in': { from: { opacity: '0', transform: 'translateY(6px)' }, to: { opacity: '1', transform: 'translateY(0)' } },
        'scale-in': { from: { opacity: '0', transform: 'scale(.96)' }, to: { opacity: '1', transform: 'scale(1)' } },
      },
      animation: {
        shimmer: 'shimmer 1.5s infinite',
        'fade-in': 'fade-in .3s ease-out both',
        'scale-in': 'scale-in .2s ease-out both',
      },
    },
  },
  plugins: [],
};
