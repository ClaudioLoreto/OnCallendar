import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  FlatList, RefreshControl, SafeAreaView, ScrollView, Text, TouchableOpacity, View,
} from 'react-native';
import {
  Avatar, Badge, Button, Card, EmptyState, Field, Icon, SegmentedControl, Sheet,
} from '../components/ui';
import AppHeader from '../components/AppHeader';
import {
  CalendarApi, DayDto, MedicoDto, SlotDto, SwapDto, SwapsApi, UsersApi, formatRange,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';
import { useAuth } from '../auth/AuthContext';

type Tab = 'incoming' | 'outgoing';
type Props = { navigation: { navigate: (route: string) => void } };

const ymd = (d: Date) => d.toISOString().slice(0, 10);
const parseYmd = (s: string) => { const [y, m, d] = s.split('-').map(Number); return new Date(y, (m ?? 1) - 1, d ?? 1); };

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
  const [pickedDate, setPickedDate] = useState<string | null>(null);
  const [pickedShift, setPickedShift] = useState<SlotDto | null>(null);
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
    (async () => { try { await loadLists(); } finally { setLoading(false); } })();
  }, [loadLists]);

  const onRefresh = async () => {
    setRefreshing(true);
    try { await loadLists(); } finally { setRefreshing(false); }
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
    setPickedDate(null); setPickedShift(null); setPickedColleague(null);
    setMessage(''); setStepError(null);
    try {
      const today = new Date(); today.setHours(0, 0, 0, 0);
      const to = new Date(today); to.setDate(to.getDate() + 14);
      const [d, m] = await Promise.all([CalendarApi.list(today, to), UsersApi.medici()]);
      setDays(d);
      setColleagues(m.filter(x => x.id !== user?.userId));
    } catch (e: any) {
      setStepError(e?.response?.data?.error ?? e?.message ?? 'Errore caricamento');
    }
  };

  // Date in cui ho un turno futuro
  const myDates = useMemo(() => {
    const set = new Set<string>();
    const now = new Date();
    for (const d of days) {
      for (const s of d.slots) {
        if (s.isMine && new Date(s.startUtc) > now) set.add(d.dateUtc.slice(0, 10));
      }
    }
    return Array.from(set).sort();
  }, [days]);

  const myShiftsOnDate = useMemo(() => {
    if (!pickedDate) return [];
    const day = days.find(d => d.dateUtc.slice(0, 10) === pickedDate);
    if (!day) return [];
    return day.slots.filter(s => s.isMine && new Date(s.startUtc) > new Date());
  }, [pickedDate, days]);

  const colleaguesOnPickedShift = useMemo(() => {
    // Tutti i colleghi tranne me; il backend valida poi se il destinatario è disponibile
    return colleagues;
  }, [colleagues]);

  const submitNew = async () => {
    if (!pickedShift || !pickedColleague) return;
    setSubmitting(true); setStepError(null);
    try {
      await SwapsApi.giveaway(pickedShift.shiftId, pickedColleague.id, message || undefined);
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

  const dateLabel = (s: string) => parseYmd(s).toLocaleDateString(locale === 'it' ? 'it-IT' : 'en-GB', {
    weekday: 'long', day: '2-digit', month: 'long',
  });
  const slotLabel = (s: SlotDto) => {
    const h = new Date(s.startUtc).getHours();
    return h < 12 ? `${t('calendar.morning')}` : `${t('calendar.night')}`;
  };
  const slotIcon = (s: SlotDto) => new Date(s.startUtc).getHours() < 12 ? 'sunny-outline' : 'moon-outline';

  const list = tab === 'incoming' ? incoming : outgoing;

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
            const tone =
              item.status === 'AutoApproved' ? 'success' :
              item.status === 'Rejected' ? 'danger' :
              item.status === 'Cancelled' ? 'neutral' :
              item.status === 'BlockedByRules' ? 'danger' : 'warning';
            const isIncoming = tab === 'incoming';
            return (
              <Card>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: theme.spacing.s }}>
                  <Text style={theme.typography.h3}>
                    {item.type === 'Giveaway' ? 'Cessione' : item.type === 'Swap' ? 'Scambio' : 'Pick bacheca'}
                  </Text>
                  <Badge label={item.status} tone={tone as any} />
                </View>
                <Text style={theme.typography.body}>
                  {isIncoming ? `Da ${item.initiatorName}` : `A ${item.counterpartName ?? '—'}`}
                </Text>
                <Text style={theme.typography.caption}>
                  Turno: {formatRange(item.initiatorShiftStart, item.initiatorShiftEnd)}
                </Text>
                {item.message ? (
                  <Text style={[theme.typography.caption, { fontStyle: 'italic', marginTop: 4 }]}>
                    «{item.message}»
                  </Text>
                ) : null}

                {item.status === 'Pending' ? (
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

      {/* Sheet wizard nuova richiesta */}
      <Sheet visible={newOpen} onClose={() => setNewOpen(false)} title={t('swaps.new')}>
        <ScrollView>
          {/* Step 1: data */}
          <Text style={[theme.typography.body, { fontWeight: '700', marginBottom: theme.spacing.s }]}>
            <Icon name="calendar-outline" size={16} color={theme.colors.textPrimary} />  1. Quale giorno?
          </Text>
          {myDates.length === 0 ? (
            <View style={{ padding: theme.spacing.m, backgroundColor: theme.colors.surfaceAlt, borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
              <Text style={theme.typography.caption}>Non hai turni futuri da scambiare.</Text>
            </View>
          ) : (
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginBottom: theme.spacing.m }}>
              {myDates.map(d => {
                const active = pickedDate === d;
                return (
                  <TouchableOpacity
                    key={d}
                    onPress={() => { setPickedDate(d); setPickedShift(null); }}
                    style={{
                      borderWidth: 1,
                      borderColor: active ? theme.colors.primary : theme.colors.border,
                      backgroundColor: active ? theme.colors.accent : theme.colors.surface,
                      borderRadius: theme.radius.m, paddingHorizontal: 12, paddingVertical: 8,
                    }}
                  >
                    <Text style={{ color: active ? theme.colors.primary : theme.colors.textPrimary, fontWeight: active ? '700' : '500' }}>
                      {dateLabel(d)}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>
          )}

          {/* Step 2: turno del giorno */}
          {pickedDate ? (
            <>
              <Text style={[theme.typography.body, { fontWeight: '700', marginBottom: theme.spacing.s }]}>
                <Icon name="time-outline" size={16} color={theme.colors.textPrimary} />  2. Quale turno?
              </Text>
              {myShiftsOnDate.length === 0 ? (
                <View style={{ padding: theme.spacing.m, backgroundColor: '#FBE3E1', borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
                  <Text style={{ color: theme.colors.danger }}>Impossibile chiedere swap: turno libero in questo giorno.</Text>
                </View>
              ) : (
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginBottom: theme.spacing.m }}>
                  {myShiftsOnDate.map(s => {
                    const active = pickedShift?.shiftId === s.shiftId;
                    return (
                      <TouchableOpacity
                        key={s.shiftId}
                        onPress={() => setPickedShift(s)}
                        style={{
                          flexDirection: 'row', alignItems: 'center', gap: 6,
                          borderWidth: 1,
                          borderColor: active ? theme.colors.primary : theme.colors.border,
                          backgroundColor: active ? theme.colors.accent : theme.colors.surface,
                          borderRadius: theme.radius.m, paddingHorizontal: 12, paddingVertical: 8,
                        }}
                      >
                        <Icon name={slotIcon(s) as any} size={16} color={theme.colors.primary} />
                        <Text style={{ color: active ? theme.colors.primary : theme.colors.textPrimary, fontWeight: '600' }}>
                          {slotLabel(s)}
                        </Text>
                      </TouchableOpacity>
                    );
                  })}
                </View>
              )}
            </>
          ) : null}

          {/* Step 3: collega */}
          {pickedShift ? (
            <>
              <Text style={[theme.typography.body, { fontWeight: '700', marginBottom: theme.spacing.s }]}>
                <Icon name="people-outline" size={16} color={theme.colors.textPrimary} />  3. A chi cedi?
              </Text>
              {colleaguesOnPickedShift.map(m => {
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
                title={t('common.confirm')}
                icon="paper-plane-outline"
                onPress={submitNew}
                loading={submitting}
                disabled={!pickedShift || !pickedColleague}
              />
            </View>
          </View>
        </ScrollView>
      </Sheet>
    </SafeAreaView>
  );
}
