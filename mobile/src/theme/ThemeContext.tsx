import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { Appearance } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { AppTheme, ColorScheme, buildTheme } from './theme';

export type ThemePreference = 'light' | 'dark' | 'system';

type Ctx = {
  theme: AppTheme;
  scheme: ColorScheme;
  preference: ThemePreference;
  setPreference: (p: ThemePreference) => Promise<void>;
};

const ThemeCtx = createContext<Ctx | null>(null);
const STORAGE_KEY = 'oncallendar.themePref';

export const ThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [preference, setPref] = useState<ThemePreference>('system');
  const [systemScheme, setSystemScheme] = useState<ColorScheme>(
    (Appearance.getColorScheme() as ColorScheme) ?? 'light',
  );

  useEffect(() => {
    (async () => {
      const raw = await AsyncStorage.getItem(STORAGE_KEY);
      if (raw === 'light' || raw === 'dark' || raw === 'system') setPref(raw);
    })();
    const sub = Appearance.addChangeListener(({ colorScheme }) => {
      setSystemScheme((colorScheme as ColorScheme) ?? 'light');
    });
    return () => sub.remove();
  }, []);

  const setPreference = useCallback(async (p: ThemePreference) => {
    setPref(p);
    await AsyncStorage.setItem(STORAGE_KEY, p);
  }, []);

  const scheme: ColorScheme = preference === 'system' ? systemScheme : preference;
  const theme = useMemo(() => buildTheme(scheme), [scheme]);

  return (
    <ThemeCtx.Provider value={{ theme, scheme, preference, setPreference }}>
      {children}
    </ThemeCtx.Provider>
  );
};

export const useTheme = (): Ctx => {
  const ctx = useContext(ThemeCtx);
  if (!ctx) throw new Error('useTheme must be inside ThemeProvider');
  return ctx;
};
