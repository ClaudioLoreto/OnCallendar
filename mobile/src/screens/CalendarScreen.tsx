import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  FlatList, RefreshControl, SafeAreaView, ScrollView, Text, TouchableOpacity, View,
} from 'react-native';
import { Avatar, Badge, Button, Card, EmptyState, Field, Icon, Sheet } from '../components/ui';
import AppHeader from '../components/AppHeader';
import {
  CalendarApi, DayDto, MedicoDto, ShiftDto, ShiftStatus, ShiftsApi, SwapsApi, UsersApi,
  shiftCodeIcon, shiftCodeShort, shiftCodeTone,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';
import { useAuth } from '../auth/AuthContext';

type Props = { navigation: { navigate: (route: string) => void } };

export default function CalendarScreen({ navigation }: Props) {
  const { theme } = useTheme();
  const { t, locale } = useI18n();
  const { user } = useAuth();

  const [days, setDays] = useState<DayDto[]>([]);
  const [medici, setMedici] = useState<MedicoDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Sheet azioni sul mio turno
  const [actionShift, setActionShift] = useState<ShiftDto | null>(null);
  const [actionBusy, setActionBusy] = useState(false);

  // Sheet "cedi a un collega"
  const [giveShift, setGiveShift] = useState<ShiftDto | null>(null);
  const [giveMessage, setGiveMessage] = useState('');
  const [giveBusy, setGiveBusy] = useState(false);
  const [giveError, setGiveError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const to = new Date(today); to.setDate(to.getDate() + 14);
    const [d, m] = await Promise.all([
      CalendarApi.list(today, to),
      UsersApi.medici(),
    ]);
    setDays(d);
    setMedici(m);
  }, []);

  useEffect(() => {
    (async () => {
      try { await load(); }
      catch (e: any) { setError(e?.response?.data?.error ?? e?.message ?? 'Errore'); }
      finally { setLoading(false); }
    })();
  }, [load]);

  const onRefresh = async () => {
    setRefreshing(true);
    try { await load(); }
    catch (e: any) { setError(e?.response?.data?.error ?? e?.message ?? 'Errore'); }
    finally { setRefreshing(false); }
  };

  const isTodayStr = (ymd: string) => {
    const t = new Date();
    const today = `${t.getFullYear()}-${String(t.getMonth() + 1).padStart(2, '0')}-${String(t.getDate()).padStart(2, '0')}`;
    return ymd === today;
  };
  const isTomorrowStr = (ymd: string) => {
    const t = new Date(); t.setDate(t.getDate() + 1);
    const tom = `${t.getFullYear()}-${String(t.getMonth() + 1).padStart(2, '0')}-${String(t.getDate()).padStart(2, '0')}`;
    return ymd === tom;
  };

  const dayHeader = (ymd: string) => {
    const d = new Date(`${ymd}T00:00:00`);
    const short = d.toLocaleDateString(locale === 'it' ? 'it-IT' : 'en-GB', { day: 'numeric', month: 'long' });
    if (isTodayStr(ymd)) return `${t('calendar.today')} · ${short}`;
    if (isTomorrowStr(ymd)) return `${t('calendar.tomorrow')} · ${short}`;
    const wd = d.toLocaleDateString(locale === 'it' ? 'it-IT' : 'en-GB', { weekday: 'long' });
    return `${wd} · ${short}`;
  };

  const colleagues = medici.filter(m => m.id !== user?.userId);

  // ---- Azioni sul mio turno ----
  const openActions = (s: ShiftDto) => {
    if (!s.isMineTurno || s.isPast) return;
    setActionShift(s);
  };
  const togglePublish = async () => {
    if (!actionShift) return;
    setActionBusy(true);
    try {
      if (actionShift.status === ShiftStatus.OnBoard) {
        await ShiftsApi.unpublish(actionShift.id);
      } else {
        await ShiftsApi.publish(actionShift.id);
      }
      setActionShift(null);
      await load();
    } catch (e: any) {
      // mostralo nel banner globale
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setActionBusy(false);
    }
  };
  const startGiveaway = () => {
    if (!actionShift) return;
    setGiveShift(actionShift);
    setActionShift(null);
    setGiveMessage('');
    setGiveError(null);
  };
  const submitGiveaway = async (toMedicoId: string) => {
    if (!giveShift) return;
    setGiveBusy(true); setGiveError(null);
    try {
      await SwapsApi.giveaway(giveShift.id, toMedicoId, giveMessage || undefined);
      setGiveShift(null);
      await load();
    } catch (e: any) {
      const code = e?.response?.status;
      const msg = e?.response?.data?.error ?? e?.message ?? 'Errore';
      setGiveError(code === 409 ? 'Hai già una richiesta in sospeso per questo turno.' : msg);
    } finally {
      setGiveBusy(false);
    }
  };

  // ---- Render ----
  const renderShift = (s: ShiftDto) => {
    const isMine = s.isMineTurno || s.isMineReperibile;
    const onBoard = s.status === ShiftStatus.OnBoard;
    const dim = s.isPast && !isMine;

    return (
      <TouchableOpacity
        key={s.id}
        activeOpacity={s.isMineTurno && !s.isPast ? 0.7 : 1}
        onPress={() => openActions(s)}
        style={{
          borderTopWidth: 1,
          borderTopColor: theme.colors.border,
          paddingVertical: theme.spacing.m,
          opacity: dim ? 0.55 : 1,
        }}
      >
        <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10, flex: 1 }}>
            <Icon name={shiftCodeIcon(s.code) as any} size={22} color={theme.colors.primary} />
            <View style={{ flex: 1 }}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                <Badge label={s.code} tone={shiftCodeTone(s.code) as any} />
                <Text style={[theme.typography.body, { fontWeight: '700' }]}>{shiftCodeShort(s.code)}</Text>
              </View>
              <Text style={theme.typography.caption}>{s.startLocal} – {s.endLocal}</Text>
            </View>
          </View>
          {onBoard ? <Badge label="In bacheca" tone="warning" /> : null}
        </View>

        {/* Medico di turno */}
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: theme.spacing.s }}>
          {s.medicoTurno ? (
            <>
              <Avatar fullName={s.medicoTurno.fullName} url={s.medicoTurno.avatarUrl} size={26} />
              <Text style={[theme.typography.body, { flex: 1, fontWeight: s.isMineTurno ? '700' : '400' }]}>
                {s.medicoTurno.fullName}
                {s.isMineTurno ? ' (tu)' : ''}
              </Text>
            </>
          ) : (
            <Text style={[theme.typography.caption, { fontStyle: 'italic' }]}>Nessun medico assegnato</Text>
          )}
          <Text style={theme.typography.caption}>turno</Text>
        </View>

        {/* Medico reperibile */}
        {s.medicoReperibile ? (
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 6 }}>
            <Avatar fullName={s.medicoReperibile.fullName} url={s.medicoReperibile.avatarUrl} size={20} />
            <Text style={[theme.typography.caption, { flex: 1, fontWeight: s.isMineReperibile ? '700' : '400' }]}>
              {s.medicoReperibile.fullName}{s.isMineReperibile ? ' (tu)' : ''}
            </Text>
            <Text style={theme.typography.caption}>reperibile</Text>
          </View>
        ) : null}

        {s.isMineTurno && !s.isPast ? (
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 6 }}>
            <Icon name="ellipsis-horizontal" size={14} color={theme.colors.textMuted} />
            <Text style={theme.typography.caption}>Tocca per gestire</Text>
          </View>
        ) : null}
      </TouchableOpacity>
    );
  };

  const renderDay = ({ item }: { item: DayDto }) => {
    const hasMine = item.shifts.some(s => s.isMineTurno || s.isMineReperibile);
    const today = isTodayStr(item.date);
    return (
      <Card>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
          <Text style={[theme.typography.h3, { textTransform: 'capitalize' }]}>{dayHeader(item.date)}</Text>
          {today ? <Badge label="Oggi" tone="info" /> : hasMine ? <Badge label="Sei in turno" tone="success" /> : null}
        </View>
        {item.shifts.map(renderShift)}
      </Card>
    );
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <AppHeader title={t('calendar.title')} onAvatarPress={() => navigation.navigate('Profile')} />
      {loading ? (
        <EmptyState title={t('common.loading')} />
      ) : (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: theme.spacing.s }}
          data={days}
          keyExtractor={d => d.date}
          renderItem={renderDay}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListHeaderComponent={
            error ? (
              <View style={{ backgroundColor: '#FBE3E1', padding: theme.spacing.m, borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
                <Text style={{ color: theme.colors.danger, fontWeight: '600' }}>{error}</Text>
              </View>
            ) : null
          }
          ListEmptyComponent={<EmptyState icon="calendar-outline" title="Nessun turno" subtitle="Aggiorna trascinando giù." />}
        />
      )}

      {/* Sheet azioni sul mio turno */}
      <Sheet visible={!!actionShift} onClose={() => setActionShift(null)} title="Gestisci turno">
        {actionShift ? (
          <ScrollView>
            <View style={{
              padding: theme.spacing.m, borderRadius: theme.radius.m,
              backgroundColor: theme.colors.accent, marginBottom: theme.spacing.m,
              flexDirection: 'row', alignItems: 'center', gap: 12,
            }}>
              <Icon name={shiftCodeIcon(actionShift.code) as any} size={28} color={theme.colors.primary} />
              <View style={{ flex: 1 }}>
                <Text style={[theme.typography.body, { fontWeight: '700' }]}>
                  {dayHeader(actionShift.date)}
                </Text>
                <Text style={theme.typography.caption}>
                  {actionShift.codeLabel} · {actionShift.startLocal} – {actionShift.endLocal}
                </Text>
              </View>
            </View>

            <View style={{ gap: theme.spacing.s }}>
              <Button
                title={actionShift.status === ShiftStatus.OnBoard ? 'Ritira dalla bacheca' : 'Pubblica in bacheca'}
                icon={actionShift.status === ShiftStatus.OnBoard ? 'arrow-undo-outline' : 'megaphone-outline'}
                variant={actionShift.status === ShiftStatus.OnBoard ? 'subtle' : 'secondary'}
                loading={actionBusy}
                onPress={togglePublish}
              />
              <Button
                title="Cedi a un collega"
                icon="person-add-outline"
                onPress={startGiveaway}
              />
              <Button
                title="Annulla"
                icon="close"
                variant="ghost"
                onPress={() => setActionShift(null)}
              />
            </View>
          </ScrollView>
        ) : null}
      </Sheet>

      {/* Sheet giveaway */}
      <Sheet visible={!!giveShift} onClose={() => setGiveShift(null)} title="Cedi questo turno">
        {giveShift ? (
          <ScrollView>
            <Text style={[theme.typography.caption, { marginBottom: theme.spacing.m }]}>
              {dayHeader(giveShift.date)} · {giveShift.codeLabel} · {giveShift.startLocal} – {giveShift.endLocal}
            </Text>
            <Field label="Messaggio (opzionale)" value={giveMessage} onChangeText={setGiveMessage} multiline />
            <Text style={[theme.typography.body, { fontWeight: '600', marginBottom: theme.spacing.s }]}>
              Scegli un collega
            </Text>
            {colleagues.length === 0 ? (
              <Text style={theme.typography.caption}>Nessun collega disponibile.</Text>
            ) : colleagues.map(m => (
              <TouchableOpacity
                key={m.id}
                onPress={() => submitGiveaway(m.id)}
                disabled={giveBusy}
                style={{
                  flexDirection: 'row', alignItems: 'center', gap: 12,
                  borderWidth: 1, borderColor: theme.colors.border,
                  borderRadius: theme.radius.m, padding: theme.spacing.m,
                  marginBottom: theme.spacing.s, backgroundColor: theme.colors.surface,
                  opacity: giveBusy ? 0.6 : 1,
                }}
              >
                <Avatar fullName={m.fullName} url={m.avatarUrl} size={32} />
                <Text style={[theme.typography.body, { flex: 1 }]}>{m.fullName}</Text>
                <Icon name="chevron-forward" size={18} color={theme.colors.textMuted} />
              </TouchableOpacity>
            ))}
            {giveError ? (
              <Text style={{ color: theme.colors.danger, marginTop: theme.spacing.s }}>{giveError}</Text>
            ) : null}
          </ScrollView>
        ) : null}
      </Sheet>
    </SafeAreaView>
  );
}
