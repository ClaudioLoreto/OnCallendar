import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  FlatList, RefreshControl, SafeAreaView, ScrollView, Text, TouchableOpacity, View,
} from 'react-native';
import { Avatar, Badge, Button, Card, EmptyState, Field, Icon, Sheet } from '../components/ui';
import AppHeader from '../components/AppHeader';
import { CalendarApi, DayDto, MedicoDto, SlotDto, SwapsApi, UsersApi } from '../api/endpoints';
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

  const [joinSlot, setJoinSlot] = useState<SlotDto | null>(null);
  const [joinSubmitting, setJoinSubmitting] = useState(false);
  const [joinError, setJoinError] = useState<string | null>(null);

  const [swapSlot, setSwapSlot] = useState<SlotDto | null>(null);
  const [swapMessage, setSwapMessage] = useState('');
  const [swapSubmitting, setSwapSubmitting] = useState(false);
  const [swapError, setSwapError] = useState<string | null>(null);

  const load = useCallback(async () => {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const to = new Date(today);
    to.setDate(to.getDate() + 14);
    const [d, m] = await Promise.all([
      CalendarApi.list(today, to),
      UsersApi.medici(),
    ]);
    setDays(d);
    setMedici(m);
  }, []);

  useEffect(() => {
    (async () => { try { await load(); } finally { setLoading(false); } })();
  }, [load]);

  const onRefresh = async () => {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  };

  const isToday = (iso: string) => {
    const d = new Date(iso); const t = new Date();
    return d.getFullYear() === t.getFullYear() && d.getMonth() === t.getMonth() && d.getDate() === t.getDate();
  };
  const isTomorrow = (iso: string) => {
    const d = new Date(iso); const t = new Date(); t.setDate(t.getDate() + 1);
    return d.getFullYear() === t.getFullYear() && d.getMonth() === t.getMonth() && d.getDate() === t.getDate();
  };

  const dayHeader = (iso: string) => {
    const d = new Date(iso);
    const short = d.toLocaleDateString(locale === 'it' ? 'it-IT' : 'en-GB', { day: 'numeric', month: 'long' });
    if (isToday(iso)) return `${t('calendar.today')} · ${short}`;
    if (isTomorrow(iso)) return `${t('calendar.tomorrow')} · ${short}`;
    const wd = d.toLocaleDateString(locale === 'it' ? 'it-IT' : 'en-GB', { weekday: 'long' });
    return `${wd} · ${short}`;
  };

  const slotShortLabel = (slot: SlotDto) => {
    const start = new Date(slot.startUtc).getHours();
    return start < 12 ? t('calendar.morning') : t('calendar.night');
  };
  const slotIcon = (slot: SlotDto) => {
    const start = new Date(slot.startUtc).getHours();
    return start < 12 ? 'sunny-outline' : 'moon-outline';
  };
  const slotHours = (slot: SlotDto) => {
    const s = new Date(slot.startUtc); const e = new Date(slot.endUtc);
    const fmt = (d: Date) => d.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
    return `${fmt(s)} – ${fmt(e)}`;
  };

  const slotInPast = (slot: SlotDto) => new Date(slot.startUtc) <= new Date();

  // ---- join flow ----
  const openJoin = (slot: SlotDto) => {
    setJoinSlot(slot); setJoinError(null);
  };
  const confirmJoin = async () => {
    if (!joinSlot) return;
    setJoinSubmitting(true); setJoinError(null);
    try {
      await CalendarApi.join([joinSlot.shiftId]);
      setJoinSlot(null);
      await load();
    } catch (e: any) {
      setJoinError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setJoinSubmitting(false);
    }
  };

  // ---- swap flow ----
  const openSwap = (slot: SlotDto) => {
    setSwapSlot(slot); setSwapMessage(''); setSwapError(null);
  };
  const submitSwap = async (toMedicoId: string) => {
    if (!swapSlot) return;
    setSwapSubmitting(true); setSwapError(null);
    try {
      await SwapsApi.giveaway(swapSlot.shiftId, toMedicoId, swapMessage || undefined);
      setSwapSlot(null);
      await load();
    } catch (e: any) {
      const code = e?.response?.status;
      const msg = e?.response?.data?.error ?? e?.message ?? 'Errore';
      setSwapError(code === 409 ? 'Hai già una richiesta in sospeso per questo turno.' : msg);
    } finally {
      setSwapSubmitting(false);
    }
  };

  const renderSlot = (slot: SlotDto, isLast: boolean) => {
    const past = slotInPast(slot);
    const canJoin = !slot.isMine && slot.hasFreeSpot && !past;
    const canSwap = slot.isMine && !past;

    return (
      <View
        key={slot.shiftId}
        style={{
          borderTopWidth: 1,
          borderTopColor: theme.colors.border,
          paddingVertical: theme.spacing.m,
          opacity: past && !slot.isMine ? 0.55 : 1,
        }}
      >
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
            <Icon name={slotIcon(slot) as any} size={22} color={theme.colors.primary} />
            <View>
              <Text style={[theme.typography.body, { fontWeight: '700' }]}>{slotShortLabel(slot)}</Text>
              <Text style={theme.typography.caption}>{slotHours(slot)}</Text>
            </View>
          </View>

          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
            {slot.assignees.length === 0 ? (
              <Text style={[theme.typography.caption, { fontStyle: 'italic' }]}>{t('calendar.empty')}</Text>
            ) : (
              <View style={{ flexDirection: 'row' }}>
                {slot.assignees.map((a, i) => (
                  <View key={a.medicoId} style={{ marginLeft: i === 0 ? 0 : -8 }}>
                    <Avatar fullName={a.fullName} url={a.avatarUrl} size={28} />
                  </View>
                ))}
              </View>
            )}
            <Text style={[theme.typography.caption, { marginLeft: 4 }]}>
              {slot.assignees.length}/{slot.capacity}
            </Text>

            {canJoin ? (
              <Button icon="checkmark" onPress={() => openJoin(slot)} compact full={false} />
            ) : null}
            {canSwap ? (
              <Button icon="swap-horizontal" variant="ghost" onPress={() => openSwap(slot)} compact full={false} />
            ) : null}
          </View>
        </View>
      </View>
    );
  };

  const renderDay = ({ item }: { item: DayDto }) => {
    const allFull = item.slots.every(s => !s.hasFreeSpot && !s.isMine);
    const hasMine = item.slots.some(s => s.isMine);
    const today = isToday(item.dateUtc);

    return (
      <Card>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
          <Text style={[theme.typography.h3, { textTransform: 'capitalize' }]}>{dayHeader(item.dateUtc)}</Text>
          {today ? <Badge label="Oggi" tone="info" /> : hasMine ? <Badge label="Sei in turno" tone="success" /> : allFull ? <Badge label={t('calendar.full')} tone="neutral" /> : null}
        </View>
        {item.slots.map((slot, idx) => renderSlot(slot, idx === item.slots.length - 1))}
      </Card>
    );
  };

  const colleagues = medici.filter(m => m.id !== user?.userId);

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <AppHeader title={t('calendar.title')} onAvatarPress={() => navigation.navigate('Profile')} />
      {loading ? (
        <EmptyState title={t('common.loading')} />
      ) : (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: theme.spacing.s }}
          data={days}
          keyExtractor={d => d.dateUtc}
          renderItem={renderDay}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListEmptyComponent={<EmptyState icon="calendar-outline" title="Nessun turno" subtitle="Aggiorna trascinando giù." />}
        />
      )}

      {/* Sheet JOIN — singolo slot */}
      <Sheet visible={!!joinSlot} onClose={() => setJoinSlot(null)} title={t('calendar.modal.title')}>
        {joinSlot ? (
          <ScrollView>
            <View style={{
              flexDirection: 'row', alignItems: 'center', gap: 12,
              padding: theme.spacing.m, borderRadius: theme.radius.m,
              backgroundColor: theme.colors.accent, marginBottom: theme.spacing.m,
            }}>
              <Icon name={slotIcon(joinSlot) as any} size={28} color={theme.colors.primary} />
              <View>
                <Text style={[theme.typography.body, { fontWeight: '700' }]}>
                  {slotShortLabel(joinSlot)} — {dayHeader(joinSlot.startUtc)}
                </Text>
                <Text style={theme.typography.caption}>{slotHours(joinSlot)}</Text>
              </View>
            </View>
            <View style={{
              flexDirection: 'row', gap: 8, padding: theme.spacing.s,
              borderRadius: theme.radius.s, backgroundColor: theme.colors.surfaceAlt, marginBottom: theme.spacing.m,
            }}>
              <Icon name="lock-closed-outline" size={16} color={theme.colors.textSecondary} />
              <Text style={[theme.typography.caption, { flex: 1 }]}>{t('calendar.locked')}</Text>
            </View>
            {joinError ? (
              <Text style={{ color: theme.colors.danger, marginBottom: theme.spacing.s }}>{joinError}</Text>
            ) : null}
            <View style={{ flexDirection: 'row', gap: theme.spacing.s }}>
              <View style={{ flex: 1 }}>
                <Button title={t('calendar.modal.cancel')} variant="subtle" onPress={() => setJoinSlot(null)} />
              </View>
              <View style={{ flex: 1 }}>
                <Button title={t('calendar.modal.confirm')} icon="checkmark" onPress={confirmJoin} loading={joinSubmitting} />
              </View>
            </View>
          </ScrollView>
        ) : null}
      </Sheet>

      {/* Sheet SWAP (giveaway) */}
      <Sheet visible={!!swapSlot} onClose={() => setSwapSlot(null)} title="Cedi questo turno">
        {swapSlot ? (
          <ScrollView>
            <Text style={[theme.typography.caption, { marginBottom: theme.spacing.m }]}>
              {slotShortLabel(swapSlot)} · {slotHours(swapSlot)}
            </Text>
            <Field label="Messaggio (opzionale)" value={swapMessage} onChangeText={setSwapMessage} multiline />
            <Text style={[theme.typography.body, { fontWeight: '600', marginBottom: theme.spacing.s }]}>
              Scegli un collega
            </Text>
            {colleagues.length === 0 ? (
              <Text style={theme.typography.caption}>Nessun collega disponibile.</Text>
            ) : colleagues.map(m => (
              <TouchableOpacity
                key={m.id}
                onPress={() => submitSwap(m.id)}
                disabled={swapSubmitting}
                style={{
                  flexDirection: 'row', alignItems: 'center', gap: 12,
                  borderWidth: 1, borderColor: theme.colors.border,
                  borderRadius: theme.radius.m, padding: theme.spacing.m,
                  marginBottom: theme.spacing.s, backgroundColor: theme.colors.surface,
                }}
              >
                <Avatar fullName={m.fullName} url={m.avatarUrl} size={32} />
                <Text style={[theme.typography.body, { flex: 1 }]}>{m.fullName}</Text>
                <Icon name="chevron-forward" size={18} color={theme.colors.textMuted} />
              </TouchableOpacity>
            ))}
            {swapError ? (
              <Text style={{ color: theme.colors.danger, marginTop: theme.spacing.s }}>{swapError}</Text>
            ) : null}
          </ScrollView>
        ) : null}
      </Sheet>
    </SafeAreaView>
  );
}
