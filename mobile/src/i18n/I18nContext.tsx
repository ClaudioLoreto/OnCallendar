import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';

export type Locale = 'it' | 'en';

type Dict = Record<string, string>;
const dictionaries: Record<Locale, Dict> = {
  it: {
    'app.title': 'OnCallendar',
    'tabs.calendar': 'Calendario',
    'tabs.board': 'Bacheca',
    'tabs.swaps': 'Scambi',
    'profile.title': 'Profilo',
    'profile.section.personal': 'Anagrafica',
    'profile.section.settings': 'Impostazioni',
    'profile.section.history': 'Storico turni',
    'profile.firstName': 'Nome',
    'profile.lastName': 'Cognome',
    'profile.email': 'Email',
    'profile.phone': 'Telefono',
    'profile.avatarUrl': 'URL avatar',
    'profile.save': 'Salva',
    'profile.theme': 'Tema',
    'profile.theme.light': 'Chiaro',
    'profile.theme.dark': 'Scuro',
    'profile.theme.system': 'Sistema',
    'profile.language': 'Lingua',
    'profile.changePassword': 'Cambia password',
    'profile.logout': 'Esci',
    'profile.unverified.email': 'Email non verificata',
    'profile.unverified.phone': 'Telefono non verificato',
    'profile.verify': 'Verifica',
    'login.title': 'Accedi',
    'login.email': 'Email',
    'login.password': 'Password',
    'login.submit': 'Accedi',
    'login.forgot': 'Password dimenticata?',
    'login.noRegister': 'La registrazione è riservata all\'amministratore.',
    'calendar.title': 'Calendario',
    'calendar.today': 'Oggi',
    'calendar.tomorrow': 'Domani',
    'calendar.morning': 'Mattina',
    'calendar.night': 'Notte',
    'calendar.full': 'Slot al completo',
    'calendar.empty': 'Nessuno prenotato',
    'calendar.giveMyAvailability': 'Dai disponibilità',
    'calendar.alreadyBooked': 'Sei prenotato',
    'calendar.requestSwap': 'Chiedi scambio',
    'calendar.modal.title': 'Seleziona le fasce',
    'calendar.modal.confirm': 'Conferma',
    'calendar.modal.cancel': 'Annulla',
    'calendar.locked': 'Una volta confermata, la disponibilità si libera solo via scambio.',
    'board.title': 'Bacheca',
    'board.empty': 'Nessun turno disponibile in bacheca.',
    'swaps.title': 'Scambi',
    'swaps.tab.incoming': 'In arrivo',
    'swaps.tab.outgoing': 'Inviati',
    'swaps.empty': 'Nessuna richiesta',
    'swaps.accept': 'Accetta',
    'swaps.reject': 'Rifiuta',
    'swaps.cancel': 'Annulla',
    'swaps.new': 'Nuova richiesta',
    'common.cancel': 'Annulla',
    'common.confirm': 'Conferma',
    'common.error': 'Errore',
    'common.loading': 'Caricamento…',
    'common.notImplemented': 'Funzione non ancora attiva.',
  },
  en: {
    'app.title': 'OnCallendar',
    'tabs.calendar': 'Calendar',
    'tabs.board': 'Board',
    'tabs.swaps': 'Swaps',
    'profile.title': 'Profile',
    'profile.section.personal': 'Personal info',
    'profile.section.settings': 'Settings',
    'profile.section.history': 'Shift history',
    'profile.firstName': 'First name',
    'profile.lastName': 'Last name',
    'profile.email': 'Email',
    'profile.phone': 'Phone',
    'profile.avatarUrl': 'Avatar URL',
    'profile.save': 'Save',
    'profile.theme': 'Theme',
    'profile.theme.light': 'Light',
    'profile.theme.dark': 'Dark',
    'profile.theme.system': 'System',
    'profile.language': 'Language',
    'profile.changePassword': 'Change password',
    'profile.logout': 'Sign out',
    'profile.unverified.email': 'Email not verified',
    'profile.unverified.phone': 'Phone not verified',
    'profile.verify': 'Verify',
    'login.title': 'Sign in',
    'login.email': 'Email',
    'login.password': 'Password',
    'login.submit': 'Sign in',
    'login.forgot': 'Forgot password?',
    'login.noRegister': 'Registration is admin-only.',
    'calendar.title': 'Calendar',
    'calendar.today': 'Today',
    'calendar.tomorrow': 'Tomorrow',
    'calendar.morning': 'Morning',
    'calendar.night': 'Night',
    'calendar.full': 'Slot full',
    'calendar.empty': 'No bookings',
    'calendar.giveMyAvailability': 'Take this slot',
    'calendar.alreadyBooked': 'You are booked',
    'calendar.requestSwap': 'Request swap',
    'calendar.modal.title': 'Select time slots',
    'calendar.modal.confirm': 'Confirm',
    'calendar.modal.cancel': 'Cancel',
    'calendar.locked': 'Once confirmed, you can only release the slot via a swap.',
    'board.title': 'Board',
    'board.empty': 'No shifts on the board.',
    'swaps.title': 'Swaps',
    'swaps.tab.incoming': 'Incoming',
    'swaps.tab.outgoing': 'Sent',
    'swaps.empty': 'No requests',
    'swaps.accept': 'Accept',
    'swaps.reject': 'Reject',
    'swaps.cancel': 'Cancel',
    'swaps.new': 'New request',
    'common.cancel': 'Cancel',
    'common.confirm': 'Confirm',
    'common.error': 'Error',
    'common.loading': 'Loading…',
    'common.notImplemented': 'Feature not yet available.',
  },
};

type Ctx = {
  locale: Locale;
  setLocale: (l: Locale) => Promise<void>;
  t: (key: string) => string;
};

const I18nCtx = createContext<Ctx | null>(null);
const STORAGE_KEY = 'oncallendar.locale';

export const I18nProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [locale, setLoc] = useState<Locale>('it');

  useEffect(() => {
    (async () => {
      const raw = await AsyncStorage.getItem(STORAGE_KEY);
      if (raw === 'it' || raw === 'en') setLoc(raw);
    })();
  }, []);

  const setLocale = useCallback(async (l: Locale) => {
    setLoc(l);
    await AsyncStorage.setItem(STORAGE_KEY, l);
  }, []);

  const t = useCallback((key: string) => dictionaries[locale][key] ?? key, [locale]);

  const value = useMemo(() => ({ locale, setLocale, t }), [locale, setLocale, t]);
  return <I18nCtx.Provider value={value}>{children}</I18nCtx.Provider>;
};

export const useI18n = (): Ctx => {
  const ctx = useContext(I18nCtx);
  if (!ctx) throw new Error('useI18n must be inside I18nProvider');
  return ctx;
};
