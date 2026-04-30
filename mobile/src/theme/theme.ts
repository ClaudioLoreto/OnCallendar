/**
 * OnCallendar Design System
 * Espone tema chiaro e scuro coerenti.
 */

const lightPalette = {
  primary:    '#355872',
  secondary:  '#7AAACE',
  accent:     '#9CD5FF',
  background: '#F7F8F0',

  white:        '#FFFFFF',
  black:        '#000000',
  textPrimary:  '#355872',
  textSecondary:'#5A7689',
  textMuted:    '#8FA5B5',
  surface:      '#FFFFFF',
  surfaceAlt:   '#EFF2EA',
  border:       '#D9DFD3',
  success:      '#3FA66B',
  warning:      '#E2A23B',
  danger:       '#C0413B',

  overlay:      'rgba(53, 88, 114, 0.45)',
} as const;

const darkPalette = {
  primary:    '#9CD5FF',
  secondary:  '#7AAACE',
  accent:     '#355872',
  background: '#0E1822',

  white:        '#FFFFFF',
  black:        '#000000',
  textPrimary:  '#EAF3FA',
  textSecondary:'#A8C0D2',
  textMuted:    '#6E8597',
  surface:      '#162534',
  surfaceAlt:   '#1E3142',
  border:       '#27405A',
  success:      '#3FA66B',
  warning:      '#E2A23B',
  danger:       '#E26A65',

  overlay:      'rgba(0, 0, 0, 0.55)',
} as const;

export type ColorScheme = 'light' | 'dark';
export type Palette = typeof lightPalette;

export const palettes: Record<ColorScheme, Palette> = {
  light: lightPalette,
  dark: darkPalette,
};

export const spacing = { xs: 4, s: 8, m: 12, l: 16, xl: 24, xxl: 32 } as const;
export const radius  = { s: 6, m: 10, l: 16, xl: 24, pill: 999 } as const;

export const buildTheme = (scheme: ColorScheme) => {
  const colors = palettes[scheme];
  return {
    scheme,
    colors,
    spacing,
    radius,
    shadows: {
      card: scheme === 'dark'
        ? { shadowColor: '#000000', shadowOffset: { width: 0, height: 4 }, shadowOpacity: 0.4,  shadowRadius: 12, elevation: 3 }
        : { shadowColor: '#355872', shadowOffset: { width: 0, height: 4 }, shadowOpacity: 0.08, shadowRadius: 12, elevation: 3 },
    },
    typography: {
      h1: { fontSize: 28, fontWeight: '700' as const, color: colors.textPrimary },
      h2: { fontSize: 22, fontWeight: '700' as const, color: colors.textPrimary },
      h3: { fontSize: 18, fontWeight: '600' as const, color: colors.textPrimary },
      body: { fontSize: 16, fontWeight: '400' as const, color: colors.textPrimary },
      caption: { fontSize: 13, fontWeight: '400' as const, color: colors.textSecondary },
      button: { fontSize: 16, fontWeight: '600' as const, color: colors.white },
    },
  };
};

export type AppTheme = ReturnType<typeof buildTheme>;

// Tema di default (chiaro) per chi importa staticamente.
export const theme = buildTheme('light');
export default theme;
