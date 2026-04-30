import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  FlatList, RefreshControl, SafeAreaView, ScrollView, Text, TouchableOpacity, View,
} from 'react-native';
import {
  Avatar, Badge, Button, Card, EmptyState, Field, Icon, SegmentedControl, Sheet,
} from '../components/ui';
import AppHeader from '../components/AppHeader';
import {
  CalendarApi, DayDto, MedicoDto, ShiftDto, SwapDto, SwapStatus, SwapsApi, UsersApi,
  formatDayLong, shiftCodeIcon, shiftCodeShort, shiftCodeTone, swapStatusLabel, swapStatusTone, swapTypeLabel,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';
import { useAuth } from '../auth/AuthContext';

type Tab = 'incoming' | 'outgoing';
type Props = { navigation: { navigate: (route: string) => void } };

export default function SwapsScreen({ navigation }: Props) {
  const { theme } = useTheme();
  const { t, locale } = useI18n();
  const { user } = useAuth();

  const [tab, setTab] = useState<Tab>('incoming');
  const [incoming, setIncoming] = useState<SwapDto[]>([]);
  const [outgoing, setOutgoing] = useState<SwapDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  // wizard nuova richiesta
  const [newOpen, setNewOpen] = useState(false);
  const [days, setDays] = useState<DayDto[]>([]);
  const [colleagues, setColleagues] = useState<MedicoDto[]>([]);
  const [pickedShift, setPickedShift] = useState<ShiftDto | null>(null);
  const [pickedColleague, setPickedColleague] = useState<MedicoDto | null>(null);
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [stepError, setStepError] = useState<string | null>(null);

  const loadLists = useCallback(async () => {
    setError(null);
    const [i, o] = await Promise.all([SwapsApi.incoming(), SwapsApi.outgoing()]);
    setIncoming(i);
    setOutgoing(o);
  }, []);

  useEffect(() => {
    (async () => {
      try { await loadLists(); }
      catch (e: any) { setError(e?.response?.data?.error ?? e?.message ?? 'Errore'); }
      finally { setLoading(false); }
    })();
  }, [loadLists]);

  const onRefresh = async () => {
    setRefreshing(true);
    try { await loadLists(); }
    catch (e: any) { setError(e?.response?.data?.error ?? e?.message ?? 'Errore'); }
    finally { setRefreshing(false); }
  };

  const decide = async (id: string, action: 'accept' | 'reject' | 'cancel') => {
    setBusy(id); setError(null);
    try {
      if (action === 'accept') await SwapsApi.accept(id);
      else if (action === 'reject') await SwapsApi.reject(id);
      else await SwapsApi.cancel(id);
      await loadLists();
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setBusy(null);
    }
  };

  // ---- nuova richiesta ----
  const openNew = async () => {
    setNewOpen(true);
    setPickedShift(null); setPickedColleague(null);
    setMessage(''); setStepError(null);
    try {
      const today = new Date(); today.setHours(0, 0, 0, 0);
      const to = new Date(today); to.setDate(to.getDate() + 30);
      const [d, m] = await Promise.all([CalendarApi.list(today, to), UsersApi.medici()]);
      setDays(d);
      setColleagues(m.filter(x => x.id !== user?.userId));
    } catch (e: any) {
      setStepError(e?.response?.data?.error ?? e?.message ?? 'Errore caricamento');
    }
  };

  // I miei turni futuri (di cui sono medico di turno)
  const myShifts = useMemo<ShiftDto[]>(() => {
    const out: ShiftDto[] = [];
    for (const d of days) for (const s of d.shifts) {
      if (s.isMineTurno && !s.isPast) out.push(s);
    }
    return out;
  }, [days]);

  const submitNew = async () => {
    if (!pickedShift || !pickedColleague) return;
    setSubmitting(true); setStepError(null);
    try {
      await SwapsApi.giveaway(pickedShift.id, pickedColleague.id, message || undefined);
      setNewOpen(false);
      await loadLists();
    } catch (e: any) {
      const code = e?.response?.status;
      const msg = e?.response?.data?.error ?? e?.message ?? 'Errore invio';
      setStepError(code === 409 ? 'Hai già una richiesta in sospeso per questo turno.' : msg);
    } finally {
      setSubmitting(false);
    }
  };

  const list = tab === 'incoming' ? incoming : outgoing;

  const briefLabel = (s: { date: string; code: string; startUtc: string; endUtc: string }) => {
    const d = new Date(`${s.date}T00:00:00`);
    const day = d.toLocaleDateString(locale === 'it' ? 'it-IT' : 'en-GB',
      { weekday: 'short', day: '2-digit', month: 'short' });
    const start = new Date(s.startUtc).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
    const end   = new Date(s.endUtc).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
    return `${day} · ${s.code} · ${start} → ${end}`;
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <AppHeader title={t('swaps.title')} onAvatarPress={() => navigation.navigate('Profile')} />

      <View style={{ paddingHorizontal: theme.spacing.l }}>
        <SegmentedControl<Tab>
          value={tab}
          onChange={setTab}
          options={[
            { label: `${t('swaps.tab.incoming')} (${incoming.length})`, value: 'incoming' },
            { label: `${t('swaps.tab.outgoing')} (${outgoing.length})`, value: 'outgoing' },
          ]}
        />
      </View>

      {loading ? (
        <EmptyState title={t('common.loading')} />
      ) : (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: 0, paddingBottom: 120 }}
          data={list}
          keyExtractor={s => s.id}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListEmptyComponent={<EmptyState icon="swap-horizontal-outline" title={t('swaps.empty')} />}
          ListHeaderComponent={
            error ? (
              <View style={{ backgroundColor: '#FBE3E1', padding: theme.spacing.m, borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
                <Text style={{ color: theme.colors.danger, fontWeight: '600' }}>{error}</Text>
              </View>
            ) : null
          }
          renderItem={({ item }) => {
            const isIncoming = tab === 'incoming';
            return (
              <Card>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: theme.spacing.s }}>
                  <Text style={theme.typography.h3}>{swapTypeLabel(item.type)}</Text>
                  <Badge label={swapStatusLabel(item.status)} tone={swapStatusTone(item.status) as any} />
                </View>
                <Text style={theme.typography.body}>
                  {isIncoming ? `Da ${item.initiatorName}` : `A ${item.counterpartName ?? '—'}`}
                </Text>
                <Text style={theme.typography.caption}>Turno: {briefLabel(item.initiatorShift)}</Text>
                {item.counterpartShift ? (
                  <Text style={theme.typography.caption}>
                    Contropartita: {briefLabel(item.counterpartShift)}
                  </Text>
                ) : null}
                {item.message ? (
                  <Text style={[theme.typography.caption, { fontStyle: 'italic', marginTop: 4 }]}>
                    «{item.message}»
                  </Text>
                ) : null}
                {item.resolutionReason ? (
                  <Text style={[theme.typography.caption, { color: theme.colors.danger, marginTop: 4 }]}>
                    {item.resolutionReason}
                  </Text>
                ) : null}

                {item.status === SwapStatus.Pending ? (
                  <View style={{ marginTop: theme.spacing.m, flexDirection: 'row', gap: theme.spacing.s }}>
                    {isIncoming ? (
                      <>
                        <View style={{ flex: 1 }}>
                          <Button title={t('swaps.accept')} icon="checkmark"
                            loading={busy === item.id}
                            onPress={() => decide(item.id, 'accept')} />
                        </View>
                        <View style={{ flex: 1 }}>
                          <Button title={t('swaps.reject')} variant="danger" icon="close"
                            loading={busy === item.id}
                            onPress={() => decide(item.id, 'reject')} />
                        </View>
                      </>
                    ) : (
                      <View style={{ flex: 1 }}>
                        <Button title={t('swaps.cancel')} variant="subtle" icon="trash-outline"
                          loading={busy === item.id}
                          onPress={() => decide(item.id, 'cancel')} />
                      </View>
                    )}
                  </View>
                ) : null}
              </Card>
            );
          }}
        />
      )}

      {/* Floating Nuova richiesta */}
      <View style={{ position: 'absolute', left: theme.spacing.l, right: theme.spacing.l, bottom: theme.spacing.l }}>
        <Button title={t('swaps.new')} icon="add" onPress={openNew} />
      </View>

      {/* Sheet wizard nuova richiesta (giveaway) */}
      <Sheet visible={newOpen} onClose={() => setNewOpen(false)} title={t('swaps.new')}>
        <ScrollView>
          <Text style={[theme.typography.body, { fontWeight: '700', marginBottom: theme.spacing.s }]}>
            <Icon name="calendar-outline" size={16} color={theme.colors.textPrimary} />  1. Quale turno cedi?
          </Text>
          {myShifts.length === 0 ? (
            <View style={{ padding: theme.spacing.m, backgroundColor: theme.colors.surfaceAlt, borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
              <Text style={theme.typography.caption}>Non hai turni futuri da cedere nei prossimi 30 giorni.</Text>
            </View>
          ) : (
            <View style={{ gap: 8, marginBottom: theme.spacing.m }}>
              {myShifts.map(s => {
                const active = pickedShift?.id === s.id;
                return (
                  <TouchableOpacity
                    key={s.id}
                    onPress={() => setPickedShift(s)}
                    style={{
                      flexDirection: 'row', alignItems: 'center', gap: 10,
                      borderWidth: 1,
                      borderColor: active ? theme.colors.primary : theme.colors.border,
                      backgroundColor: active ? theme.colors.accent : theme.colors.surface,
                      borderRadius: theme.radius.m, paddingHorizontal: 12, paddingVertical: 10,
                    }}
                  >
                    <Icon name={shiftCodeIcon(s.code) as any} size={18} color={theme.colors.primary} />
                    <Badge label={s.code} tone={shiftCodeTone(s.code) as any} />
                    <View style={{ flex: 1 }}>
                      <Text style={{ color: active ? theme.colors.primary : theme.colors.textPrimary, fontWeight: '600', textTransform: 'capitalize' }}>
                        {formatDayLong(s.date, locale === 'it' ? 'it-IT' : 'en-GB')}
                      </Text>
                      <Text style={theme.typography.caption}>
                        {shiftCodeShort(s.code)} · {s.startLocal} – {s.endLocal}
                      </Text>
                    </View>
                  </TouchableOpacity>
                );
              })}
            </View>
          )}

          {pickedShift ? (
            <>
              <Text style={[theme.typography.body, { fontWeight: '700', marginBottom: theme.spacing.s }]}>
                <Icon name="people-outline" size={16} color={theme.colors.textPrimary} />  2. A chi cedi?
              </Text>
              {colleagues.map(m => {
                const active = pickedColleague?.id === m.id;
                return (
                  <TouchableOpacity
                    key={m.id}
                    onPress={() => setPickedColleague(m)}
                    style={{
                      flexDirection: 'row', alignItems: 'center', gap: 12,
                      borderWidth: 1,
                      borderColor: active ? theme.colors.primary : theme.colors.border,
                      backgroundColor: active ? theme.colors.accent : theme.colors.surface,
                      borderRadius: theme.radius.m, padding: theme.spacing.m,
                      marginBottom: theme.spacing.s,
                    }}
                  >
                    <Avatar fullName={m.fullName} url={m.avatarUrl} size={32} />
                    <Text style={[theme.typography.body, { flex: 1 }]}>{m.fullName}</Text>
                    {active ? <Icon name="checkmark-circle" size={22} color={theme.colors.primary} /> : null}
                  </TouchableOpacity>
                );
              })}

              <Field label="Messaggio (opzionale)" value={message} onChangeText={setMessage} multiline />
            </>
          ) : null}

          {stepError ? (
            <Text style={{ color: theme.colors.danger, marginBottom: theme.spacing.s }}>{stepError}</Text>
          ) : null}

          <View style={{ flexDirection: 'row', gap: theme.spacing.s, marginTop: theme.spacing.m }}>
            <View style={{ flex: 1 }}>
              <Button title={t('common.cancel')} variant="subtle" onPress={() => setNewOpen(false)} />
            </View>
            <View style={{ flex: 1 }}>
              <Button
                title="Invia"
                icon="send"
                loading={submitting}
                onPress={submitNew}
              />
            </View>
          </View>
        </ScrollView>
      </Sheet>
    </SafeAreaView>
  );
}
