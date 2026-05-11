import React, { useEffect, useMemo, useState } from 'react';
import { FlatList, RefreshControl, SafeAreaView, Text, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Button, ConfirmModal, EmptyState, Field, PasswordField, Sheet } from '../components/ui';
import { useTheme } from '../theme/ThemeContext';
import { useNotifications } from '../auth/NotificationsContext';
import { AuthApi, MeDto, UsersApi } from '../api/endpoints';
import { buildCallbackUrl } from '../api/callbackUrl';

type Props = { navigation: any };

/**
 * Restituisce il "tono" della notifica usando la palette ufficiale dell'app.
 * Tutte le notifiche di tipo info → primary, le positive → success, le attese → warning,
 * le bloccanti / errori → danger. Niente colori arbitrari.
 */
type Tone = 'primary' | 'success' | 'warning' | 'danger';

function notifTone(type: string): { tone: Tone; icon: string; label: string } {
  switch (type) {
    // SWAP — apertura / info
    case 'SwapRequested':
    case 'SwapIncoming':
      return { tone: 'primary', icon: 'swap-horizontal-outline', label: 'Nuova richiesta' };
    case 'CounterOfferReceived':
    case 'SwapCounter':
      return { tone: 'primary', icon: 'repeat-outline', label: 'Controproposta ricevuta' };
    // SWAP — esito positivo
    case 'SwapAccepted':
    case 'CounterOfferAccepted':
      return { tone: 'success', icon: 'checkmark-circle-outline', label: 'Richiesta accettata' };
    // SWAP — annullamenti / esiti neutri
    case 'SwapCancelled':
      return { tone: 'warning', icon: 'ban-outline', label: 'Richiesta annullata' };
    case 'SwapAutoCancelled':
    case 'SwapAutoCancel':
      return { tone: 'warning', icon: 'time-outline', label: 'Turno gi\u00e0 preso' };
    // SWAP — esito negativo
    case 'SwapRejected':
    case 'CounterOfferRejected':
    case 'SwapCounterRejected':
      return { tone: 'danger', icon: 'close-circle-outline', label: 'Richiesta rifiutata' };
    // SHIFT
    case 'ShiftAssigned':
      return { tone: 'primary', icon: 'calendar-outline', label: 'Nuovo turno' };
    case 'ShiftReassigned':
      return { tone: 'warning', icon: 'sync-outline', label: 'Turno modificato' };
    case 'ShiftRemoved':
      return { tone: 'danger', icon: 'trash-outline', label: 'Turno rimosso' };
    case 'ShiftPostedToBoard':
      return { tone: 'primary', icon: 'megaphone-outline', label: 'Turno in bacheca' };
    case 'ExternalDoctorAssigned':
      return { tone: 'warning', icon: 'person-add-outline', label: 'Affidato a esterno' };
    // REMINDER
    case 'ReminderShiftTomorrow':
      return { tone: 'warning', icon: 'alarm-outline', label: 'Promemoria turno' };
    case 'ReminderOnCallToday':
      return { tone: 'primary', icon: 'pulse-outline', label: 'Reperibilit\u00e0 oggi' };
    // SISTEMA
    case 'SystemAnnouncement':
      return { tone: 'primary', icon: 'megaphone-outline', label: 'Avviso' };
    default:
      return { tone: 'primary', icon: 'notifications-outline', label: type };
  }
}

const timeAgo = (iso: string): string => {
  const normalised = /[Z+\-]\d{2}:?\d{2}$|Z$/.test(iso) ? iso : iso + 'Z';
  const diff = Date.now() - new Date(normalised).getTime();
  const mins = Math.floor(diff / 60_000);
  if (mins < 1) return 'Ora';
  if (mins < 60) return `${mins} min fa`;
  const h = Math.floor(mins / 60);
  if (h < 24) return `${h} ore fa`;
  const d = Math.floor(h / 24);
  return `${d} giorni fa`;
};

export default function NotificationsScreen({ navigation }: Props) {
  const { theme } = useTheme();
  const { notifications, loading, refresh, markAllRead } = useNotifications();
  const [me, setMe] = useState<MeDto | null>(null);

  // Sheet per gestire pending email (cambio o reinvio)
  const [pendingSheetOpen, setPendingSheetOpen] = useState(false);
  const [newEmail, setNewEmail] = useState('');
  const [busy, setBusy] = useState(false);

  // Sheet per password expired/required
  const [pwdSheetOpen, setPwdSheetOpen] = useState(false);
  const [oldPwd, setOldPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [newPwd2, setNewPwd2] = useState('');

  const [popup, setPopup] = useState<{ title: string; message?: string; tone?: Tone | 'default'; icon?: any } | null>(null);

  const loadMe = () => { UsersApi.me().then(setMe).catch(() => {}); };

  useEffect(() => {
    refresh();
    loadMe();
    const t = setTimeout(() => { markAllRead(); }, 1500);
    return () => clearTimeout(t);
  }, []);

  // ---- Account alerts (palette unificata) ----
  // Mutual exclusion: se c'\u00e8 pendingEmail NON mostro l'alert "email di default".
  type AccountAlert = {
    id: string;
    icon: string;
    tone: Tone;
    title: string;
    message: string;
    onPress?: () => void;
  };
  const accountAlerts: AccountAlert[] = useMemo(() => {
    const list: AccountAlert[] = [];
    if (!me) return list;

    if (me.pendingEmail) {
      list.push({
        id: 'pending-email',
        icon: 'mail-outline',
        tone: 'warning',
        title: 'Email in attesa di conferma',
        message: `Apri ${me.pendingEmail} e clicca il link che ti abbiamo inviato. Tocca qui per cambiare indirizzo o rispedire l'email.`,
        onPress: () => { setNewEmail(me.pendingEmail ?? ''); setPendingSheetOpen(true); },
      });
    } else if (me.isDefaultEmail) {
      list.push({
        id: 'default-email',
        icon: 'mail-unread-outline',
        tone: 'danger',
        title: 'Email di default',
        message: `Stai usando ${me.email}. Imposta la tua email reale per ricevere notifiche e poter recuperare la password.`,
        onPress: () => { setNewEmail(''); setPendingSheetOpen(true); },
      });
    }

    if (me.passwordChangeRequired) {
      list.push({
        id: 'pwd-required',
        icon: 'shield-half-outline',
        tone: 'danger',
        title: 'Password mai cambiata',
        message: 'Per sicurezza imposta una tua password personale. Tocca qui per farlo subito.',
        onPress: () => setPwdSheetOpen(true),
      });
    } else if (me.passwordExpired) {
      list.push({
        id: 'pwd-expired',
        icon: 'time-outline',
        tone: 'warning',
        title: 'Password scaduta',
        message: 'Sono passati pi\u00f9 di 12 mesi dall\'ultimo cambio. Tocca qui per cambiarla.',
        onPress: () => setPwdSheetOpen(true),
      });
    }
    return list;
  }, [me]);

  const onRefresh = () => { refresh(); loadMe(); };

  const tinted = (tone: Tone) => {
    const c = theme.colors[tone] ?? theme.colors.primary;
    return { color: c, bg: c + '14', border: c + '55' };
  };

  // --- Submit cambio/conferma email ---
  const submitEmail = async () => {
    const target = newEmail.trim();
    if (!target) return;
    setBusy(true);
    try {
      const r = await UsersApi.requestEmailChange(target, buildCallbackUrl('confirm-email'));
      setPendingSheetOpen(false);
      loadMe();
      setPopup({
        title: 'Email inviata',
        message: `Ti abbiamo inviato un link a ${r.pendingEmail}. Aprila e clicca il pulsante per attivare il nuovo indirizzo.`,
        tone: 'success',
        icon: 'mail-outline',
      });
    } catch (e: any) {
      setPopup({
        title: 'Errore',
        message: e?.response?.data?.error ?? 'Impossibile inviare la richiesta.',
        tone: 'danger',
        icon: 'alert-circle-outline',
      });
    } finally {
      setBusy(false);
    }
  };

  // --- Submit cambio password ---
  const submitPwd = async () => {
    if (!oldPwd || !newPwd || newPwd !== newPwd2 || newPwd.length < 8) {
      setPopup({ title: 'Dati non validi', message: 'Verifica i campi: password attuale richiesta, nuova min. 8 caratteri e conferma uguale.', tone: 'warning', icon: 'alert-circle-outline' });
      return;
    }
    setBusy(true);
    try {
      await UsersApi.changePassword(oldPwd, newPwd);
      setPwdSheetOpen(false);
      setOldPwd(''); setNewPwd(''); setNewPwd2('');
      loadMe();
      setPopup({ title: 'Password aggiornata', tone: 'success', icon: 'shield-checkmark-outline' });
    } catch (e: any) {
      setPopup({ title: 'Errore', message: e?.response?.data?.errors?.join(' ') ?? e?.response?.data?.error ?? 'Cambio non riuscito.', tone: 'danger', icon: 'alert-circle-outline' });
    } finally {
      setBusy(false);
    }
  };

  const headerComponent = accountAlerts.length === 0 ? null : (
    <View style={{ marginBottom: theme.spacing.m }}>
      {accountAlerts.map(a => {
        const tt = tinted(a.tone);
        return (
          <TouchableOpacity
            key={a.id}
            activeOpacity={0.85}
            onPress={a.onPress}
            disabled={!a.onPress}
            style={{
              flexDirection: 'row', alignItems: 'flex-start', gap: 12,
              backgroundColor: tt.bg,
              borderLeftWidth: 4, borderLeftColor: tt.color,
              borderRadius: theme.radius.l,
              padding: theme.spacing.m,
              marginBottom: theme.spacing.s,
            }}
          >
            <View style={{
              width: 38, height: 38, borderRadius: 19,
              backgroundColor: tt.color + '22',
              alignItems: 'center', justifyContent: 'center',
            }}>
              <Ionicons name={a.icon as any} size={20} color={tt.color} />
            </View>
            <View style={{ flex: 1 }}>
              <Text style={{ color: tt.color, fontWeight: '700', marginBottom: 2 }}>{a.title}</Text>
              <Text style={{ color: theme.colors.textPrimary, lineHeight: 19 }}>{a.message}</Text>
            </View>
          </TouchableOpacity>
        );
      })}
    </View>
  );

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <FlatList
        contentContainerStyle={{ padding: theme.spacing.l, paddingBottom: 80 }}
        data={notifications}
        keyExtractor={n => n.id}
        refreshControl={<RefreshControl refreshing={loading} onRefresh={onRefresh} />}
        ListHeaderComponent={headerComponent}
        ListEmptyComponent={!loading && accountAlerts.length === 0 ? (
          <EmptyState icon="notifications-outline" title="Nessuna notifica" />
        ) : null}
        renderItem={({ item }) => {
          const cfg = notifTone(item.type);
          const tt = tinted(cfg.tone);
          const cat = (item.category || '').toLowerCase();
          const isSwap = (cat === 'swap' || item.type.startsWith('Swap') || item.type.startsWith('CounterOffer')) && !!item.relatedEntityId;
          const isShift = cat === 'shift' || cat === 'reminder';
          const onPress = () => {
            if (isSwap) {
              navigation.navigate('Main', { screen: 'Swaps', params: { openSwapId: item.relatedEntityId } });
            } else if (isShift) {
              navigation.navigate('Main', { screen: 'Calendar' });
            }
          };
          const tappable = isSwap || isShift;
          return (
            <TouchableOpacity
              activeOpacity={tappable ? 0.7 : 1}
              onPress={onPress}
              disabled={!tappable}
              style={{
                flexDirection: 'row', alignItems: 'flex-start', gap: 12,
                backgroundColor: item.isRead ? theme.colors.surface : tt.bg,
                borderLeftWidth: item.isRead ? 0 : 4,
                borderLeftColor: tt.color,
                borderRadius: theme.radius.l,
                padding: theme.spacing.m,
                marginBottom: theme.spacing.s,
                ...theme.shadows.card,
              }}>
              <View style={{
                width: 38, height: 38, borderRadius: 19,
                backgroundColor: tt.color + '22',
                alignItems: 'center', justifyContent: 'center',
              }}>
                <Ionicons name={cfg.icon as any} size={20} color={tt.color} />
              </View>
              <View style={{ flex: 1 }}>
                <Text style={[theme.typography.caption, { color: tt.color, fontWeight: '700', marginBottom: 2 }]}>
                  {item.title ?? cfg.label}
                </Text>
                <Text style={[theme.typography.body, { lineHeight: 20 }]}>{item.message}</Text>
                <Text style={[theme.typography.caption, { marginTop: 4 }]}>{timeAgo(item.createdAtUtc)}</Text>
              </View>
              {!item.isRead ? (
                <View style={{
                  width: 8, height: 8, borderRadius: 4,
                  backgroundColor: tt.color, marginTop: 5,
                }} />
              ) : null}
            </TouchableOpacity>
          );
        }}
      />

      {/* Sheet cambio email / reinvio conferma */}
      <Sheet visible={pendingSheetOpen} onClose={() => setPendingSheetOpen(false)} title={me?.pendingEmail ? 'Conferma o cambia email' : 'Imposta la tua email'}>
        <Text style={[theme.typography.body, { marginBottom: theme.spacing.m }]}>
          {me?.pendingEmail
            ? `Hai una conferma in sospeso per ${me.pendingEmail}. Puoi rispedire il link allo stesso indirizzo o cambiarlo.`
            : 'Inserisci la tua email reale. Ti invieremo un link di conferma.'}
        </Text>
        <Field
          label="Email"
          value={newEmail}
          onChangeText={setNewEmail}
          keyboardType="email-address"
          autoCapitalize="none"
          autoCorrect={false}
        />
        <Button title={me?.pendingEmail && newEmail.trim().toLowerCase() === (me?.pendingEmail ?? '').toLowerCase() ? 'Rinvia link' : 'Invia link di conferma'} icon="send-outline" onPress={submitEmail} loading={busy} />
        <View style={{ height: theme.spacing.s }} />
        <Button title="Annulla" variant="subtle" onPress={() => setPendingSheetOpen(false)} />
      </Sheet>

      {/* Sheet cambio password */}
      <Sheet visible={pwdSheetOpen} onClose={() => setPwdSheetOpen(false)} title="Cambia password">
        <Text style={[theme.typography.body, { marginBottom: theme.spacing.m }]}>
          La nuova password deve avere almeno 8 caratteri, una maiuscola, una minuscola, un numero e un simbolo.
        </Text>
        <PasswordField label="Password attuale" value={oldPwd} onChangeText={setOldPwd} />
        <PasswordField label="Nuova password" value={newPwd} onChangeText={setNewPwd} />
        <PasswordField label="Conferma nuova password" value={newPwd2} onChangeText={setNewPwd2} />
        <Button title="Aggiorna password" icon="shield-checkmark-outline" onPress={submitPwd} loading={busy} />
        <View style={{ height: theme.spacing.s }} />
        <Button title="Annulla" variant="subtle" onPress={() => setPwdSheetOpen(false)} />
      </Sheet>

      <ConfirmModal
        visible={!!popup}
        title={popup?.title ?? ''}
        message={popup?.message}
        tone={popup?.tone as any}
        icon={popup?.icon}
        onClose={() => setPopup(null)}
      />
    </SafeAreaView>
  );
}
