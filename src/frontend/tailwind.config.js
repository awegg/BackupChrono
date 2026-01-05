/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        sidebar: {
          DEFAULT: 'hsl(var(--sidebar))',
          foreground: 'hsl(var(--sidebar-foreground))',
          primary: 'hsl(var(--sidebar-primary))',
          accent: 'hsl(var(--sidebar-accent))',
          'accent-foreground': 'hsl(var(--sidebar-accent-foreground))',
          border: 'hsl(var(--sidebar-border))',
        },
        card: {
          DEFAULT: 'hsl(var(--card))',
          foreground: 'hsl(var(--card-foreground))',
        },
        border: 'hsl(var(--border))',
        muted: {
          DEFAULT: 'hsl(var(--muted))',
          foreground: 'hsl(var(--muted-foreground))',
        },
        primary: {
          DEFAULT: 'hsl(var(--primary))',
          foreground: 'hsl(var(--primary-foreground))',
        },
        'status-success': 'hsl(var(--status-success))',
        'status-success-bg': 'hsl(var(--status-success-bg))',
        'status-success-fg': 'hsl(var(--status-success-fg))',
        'status-warning': 'hsl(var(--status-warning))',
        'status-warning-bg': 'hsl(var(--status-warning-bg))',
        'status-warning-fg': 'hsl(var(--status-warning-fg))',
        'status-error': 'hsl(var(--status-error))',
        'status-error-bg': 'hsl(var(--status-error-bg))',
        'status-error-fg': 'hsl(var(--status-error-fg))',
      },
    },
  },
  plugins: [],
}
