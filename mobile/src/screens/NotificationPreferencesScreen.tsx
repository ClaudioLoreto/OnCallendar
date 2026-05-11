import React, { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Platform, ScrollView, Switch, Text, View } from 'react-native';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';
import {
  NotificationChannel,
  NotificationPreferenceDto,
  NotificationsApi,
} from '../api/endpoints';

/**
 * Pannello "Preferenze notifiche".
 *
 * Solo il canale Email è modificabile dall'utente:
 *   • In-app (campanella): SEMPRE attivo, non disattivabile.
 *   • Push:                gestito dalle impostazioni del telefono (iOS/Android).
 * Mostriamo solo la lista dei tipi di evento con un toggle Email per ognuno.
 */
export default function NotificationPreferencesScreen() {
  const { theme } = useTheme();
  const { t, locale } = useI18n();
  const [prefs, setPrefs] = useState<NotificationPreferenceDto[] | null>(null);
  const [busyKey, setBusyKey] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;
    NotificationsApi.getPreferences()
      .then(p => { if (mounted) setPrefs(p); })
      .catch(() => { if (mounted) setPrefs([]); });
    return () => { mounted = false; };
  }, []);

  // Mappa tipo → flag email (default: true se non c'è override esplicito)
  const emailByType = useMemo(() => {
    const map = new Map<string, boolean>();
    for (const type of EMAIL_NOTIFICATION_TYPES) map.set(type, true);
    if (prefs) {
      for (const p of prefs) {
        if (p.channel === 'Email') map.set(p.type, p.enabled);
      }
    }
    return map;
  }, [prefs]);

  const toggleEmail = async (type: string, enabled: boolean) => {
    const key = `${type}.Email`;
    setBusyKey(key);
    setPrefs(curr => {
      const list = curr ? [...curr] : [];
      const idx = list.findIndex(p => p.type === type && p.channel === 'Email');
      if (idx >= 0) list[idx] = { ...list[idx], enabled };
      else list.push({ type, channel: 'Email', enabled });
      return list;
    });
    try {
      await NotificationsApi.setPreference(type, 'Email', enabled);
    } catch {
      setPrefs(curr => curr?.map(p =>
        p.type === type && p.channel === 'Email' ? { ...p, enabled: !enabled } : p,
      ) ?? null);
    } finally {
      setBusyKey(null);
    }
  };

  if (!prefs) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: theme.colors.background }}>
        <ActivityIndicator color={theme.colors.primary} />
      </View>
    );
  }

  const it = locale.startsWith('it');

  return (
    <ScrollView
      style={{ flex: 1, backgroundColor: theme.colors.background }}
      contentContainerStyle={{ padding: theme.spacing.l, paddingBottom: 80 }}
    >
      <Text style={[theme.typography.body, { marginBottom: theme.spacing.m, color: theme.colors.textMuted }]}>
        {it
          ? 'Scegli per quali eventi vuoi ricevere una notifica via email.'
          : 'Choose which events you want to receive an email for.'}
      </Text>

      {/* Lista tipi evento con toggle email */}
      <Text style={[theme.typography.h3, { marginBottom: theme.spacing.s }]}>
        {it ? 'Notifiche via email' : 'Email notifications'}
      </Text>

      <View style={{
        backgroundColor: theme.colors.surface,
        borderRadius: theme.radius.l,
        padding: theme.spacing.m,
        ...theme.shadows.card,
      }}>
        {EMAIL_NOTIFICATION_TYPES.map((type, idx) => {
          const enabled = emailByType.get(type) ?? true;
          const key = `${type}.Email`;
          return (
            <View
              key={type}
              style={{
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'space-between',
                paddingVertical: 10,
                borderTopWidth: idx === 0 ? 0 : 1,
                borderTopColor: theme.colors.border,
              }}
            >
              <Text style={[theme.typography.body, { flex: 1, paddingRight: 12 }]}>
                {labelForType(type, locale)}
              </Text>
              <Switch
                value={enabled}
                onValueChange={v => toggleEmail(type, v)}
                disabled={busyKey === key}
                trackColor={{ false: theme.colors.border, true: theme.colors.primary }}
                thumbColor={Platform.OS === 'android'
                  ? (enabled ? theme.colors.surface : theme.colors.surface)
                  : undefined}
                ios_backgroundColor={theme.colors.border}
              />
            </View>
          );
        })}
      </View>
    </ScrollView>
  );
}

// Eventi per cui ha senso ricevere un'email (no reminder o annunci di sistema banali).
const EMAIL_NOTIFICATION_TYPES = [
  'SwapRequested',
  'SwapAccepted',
  'SwapRejected',
  'SwapCancelled',
  'SwapAutoCancelled',
  'CounterOfferReceived',
  'CounterOfferAccepted',
  'CounterOfferRejected',
  'ShiftAssigned',
  'ShiftReassigned',
  'ShiftRemoved',
  'ShiftPostedToBoard',
  'ExternalDoctorAssigned',
  'SystemAnnouncement',
];

const labelForType = (type: string, locale: string): string => {
  const it = locale.startsWith('it');
  const map: Record<string, [string, string]> = {
    SwapRequested:        ['Nuova richiesta di scambio/cessione',           'New swap/giveaway request'],
    SwapAccepted:         ['La mia richiesta è stata accettata',             'My request was accepted'],
    SwapRejected:         ['La mia richiesta è stata rifiutata',             'My request was rejected'],
    SwapCancelled:        ['Una richiesta diretta a me è stata annullata',   'A request to me was cancelled'],
    SwapAutoCancelled:    ['Una mia richiesta è stata auto-cancellata',      'My request was auto-cancelled'],
    CounterOfferReceived: ['Ho ricevuto una controproposta',                 'I received a counter-offer'],
    CounterOfferAccepted: ['La mia controproposta è stata accettata',        'My counter-offer was accepted'],
    CounterOfferRejected: ['La mia controproposta è stata rifiutata',        'My counter-offer was rejected'],
    ShiftAssigned:        ['Mi è stato assegnato un nuovo turno',            'A new shift was assigned to me'],
    ShiftReassigned:      ['Un mio turno è stato modificato',                'One of my shifts was changed'],
    ShiftRemoved:         ['Un mio turno è stato cancellato',                'One of my shifts was removed'],
    ShiftPostedToBoard:   ['Un turno è stato pubblicato in bacheca',         'A shift was posted to the board'],
    ExternalDoctorAssigned: ['Un mio turno è stato affidato a un esterno',   'My shift was given to an external doctor'],
    ReminderShiftTomorrow:  ['Promemoria: domani sei di turno',              'Reminder: you are on duty tomorrow'],
    ReminderOnCallToday:    ['Promemoria: oggi sei di reperibilità',         'Reminder: you are on call today'],
    SystemAnnouncement:     ['Avvisi di sistema',                             'System announcements'],
  };
  const pair = map[type];
  if (!pair) return type;
  return it ? pair[0] : pair[1];
};

