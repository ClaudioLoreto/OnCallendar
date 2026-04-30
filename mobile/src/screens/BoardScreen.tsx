import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { FlatList, RefreshControl, SafeAreaView, Text, View } from 'react-native';
import { Avatar, Badge, Button, Card, EmptyState, Icon } from '../components/ui';
import AppHeader from '../components/AppHeader';
import {
  BoardApi, BoardItemDto, CalendarApi, DayDto, SlotDto, SwapsApi,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';
import { useAuth } from '../auth/AuthContext';

type Row =
  | { kind: 'board'; data: BoardItemDto }
  | { kind: 'free'; slot: SlotDto };

type Props = { navigation: { navigate: (route: string) => void } };

export default function BoardScreen({ navigation }: Props) {
  const { theme } = useTheme();
  const { t, locale } = useI18n();
  const { user } = useAuth();
  const [board, setBoard] = useState<BoardItemDto[]>([]);
  const [days, setDays] = useState<DayDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [pickingId, setPickingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const to = new Date(today); to.setDate(to.getDate() + 7);
    try {
      const [b, d] = await Promise.all([BoardApi.list(), CalendarApi.list(today, to)]);
      setBoard(b);
      setDays(d);
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    }
  }, []);

  useEffect(() => {
    (async () => { try { await load(); } finally { setLoading(false); } })();
  }, [load]);

  const onRefresh = async () => {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  };

  // Costruisce la lista: prima i giveaway in bacheca, poi i prossimi 5 slot con posti liberi.
  const rows = useMemo<Row[]>(() => {
    const out: Row[] = board.map(b => ({ kind: 'board', data: b }));
    const now = new Date();
    const free: SlotDto[] = [];
    for (const d of days) {
      for (const s of d.slots) {
        if (s.isMine) continue;
        if (!s.hasFreeSpot) continue;
        if (new Date(s.startUtc) <= now) continue;
        if (board.some(b => b.shiftId === s.shiftId)) continue;
        free.push(s);
      }
    }
    free.sort((a, b) => a.startUtc.localeCompare(b.startUtc));
    for (const s of free.slice(0, 5)) out.push({ kind: 'free', slot: s });
    return out;
  }, [board, days]);

  const onPick = async (shiftId: string) => {
    setPickingId(shiftId); setError(null);
    try {
      await SwapsApi.pick(shiftId);
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setPickingId(null);
    }
  };

  const dayShort = (iso: string) => {
    const d = new Date(iso);
    return d.toLocaleDateString(locale === 'it' ? 'it-IT' : 'en-GB', {
      weekday: 'short', day: '2-digit', month: 'short',
    });
  };
  const hours = (s: string, e: string) => {
    const fmt = (d: Date) => d.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
    return `${fmt(new Date(s))} – ${fmt(new Date(e))}`;
  };
  const slotIcon = (iso: string) => new Date(iso).getHours() < 12 ? 'sunny-outline' : 'moon-outline';

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <AppHeader title={t('board.title')} onAvatarPress={() => navigation.navigate('Profile')} />
      {loading ? (
        <EmptyState title={t('common.loading')} />
      ) : (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: theme.spacing.s }}
          data={rows}
          keyExtractor={r => r.kind + ':' + (r.kind === 'board' ? r.data.shiftId : r.slot.shiftId)}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListEmptyComponent={<EmptyState icon="albums-outline" title={t('board.empty')} subtitle="Trascina giù per aggiornare." />}
          ListHeaderComponent={
            error ? (
              <View style={{ backgroundColor: '#FBE3E1', padding: theme.spacing.m, borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
                <Text style={{ color: theme.colors.danger, fontWeight: '600' }}>{error}</Text>
              </View>
            ) : null
          }
          renderItem={({ item }) => {
            if (item.kind === 'board') {
              const b = item.data;
              return (
                <Card>
                  <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: theme.spacing.s }}>
                    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <Icon name={slotIcon(b.startUtc) as any} size={22} color={theme.colors.primary} />
                      <Text style={theme.typography.h3}>{dayShort(b.startUtc)}</Text>
                    </View>
                    <Badge label="In bacheca" tone="warning" />
                  </View>
                  <Text style={theme.typography.caption}>{hours(b.startUtc, b.endUtc)}</Text>
                  <Text style={[theme.typography.body, { marginTop: 4 }]}>
                    Offerto da <Text style={{ fontWeight: '700' }}>{b.offeredByMedicoName}</Text>
                  </Text>
                  {b.notes ? <Text style={[theme.typography.caption, { fontStyle: 'italic', marginTop: 4 }]}>«{b.notes}»</Text> : null}
                  <View style={{ marginTop: theme.spacing.m }}>
                    <Button
                      title="Prendo io"
                      icon="hand-right-outline"
                      loading={pickingId === b.shiftId}
                      onPress={() => onPick(b.shiftId)}
                    />
                  </View>
                </Card>
              );
            }
            const s = item.slot;
            return (
              <Card>
                <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: theme.spacing.s }}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                    <Icon name={slotIcon(s.startUtc) as any} size={22} color={theme.colors.primary} />
                    <Text style={theme.typography.h3}>{dayShort(s.startUtc)}</Text>
                  </View>
                  <Badge label="Posto libero" tone="info" />
                </View>
                <Text style={theme.typography.caption}>{hours(s.startUtc, s.endUtc)}</Text>
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: theme.spacing.s }}>
                  {s.assignees.map((a, i) => (
                    <View key={a.medicoId} style={{ marginLeft: i === 0 ? 0 : -6 }}>
                      <Avatar fullName={a.fullName} url={a.avatarUrl} size={24} />
                    </View>
                  ))}
                  <Text style={[theme.typography.caption, { marginLeft: 4 }]}>
                    {s.assignees.length}/{s.capacity}
                  </Text>
                </View>
                <View style={{ marginTop: theme.spacing.m }}>
                  <Button title="Prenota" icon="checkmark" onPress={() => navigation.navigate('Calendar')} variant="secondary" />
                </View>
              </Card>
            );
          }}
        />
      )}
    </SafeAreaView>
  );
}
