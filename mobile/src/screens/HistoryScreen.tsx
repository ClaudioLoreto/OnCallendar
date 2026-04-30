import React, { useCallback, useEffect, useState } from 'react';
import { FlatList, RefreshControl, SafeAreaView, Text, View } from 'react-native';
import { Badge, Card, EmptyState, Icon, SegmentedControl } from '../components/ui';
import {
  CalendarApi, formatRange, SlotDto, SwapDto, SwapsApi,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';

type Tab = 'shifts' | 'swaps';

export default function HistoryScreen() {
  const { theme } = useTheme();
  const [tab, setTab] = useState<Tab>('shifts');
  const [pastSlots, setPastSlots] = useState<SlotDto[]>([]);
  const [allSwaps, setAllSwaps] = useState<SwapDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const from = new Date(today); from.setMonth(from.getMonth() - 6);
    const [days, outgoing, incoming] = await Promise.all([
      CalendarApi.list(from, today),
      SwapsApi.outgoing(),
      SwapsApi.incoming(),
    ]);
    const flat: SlotDto[] = [];
    for (const d of days) for (const s of d.slots) if (s.isMine) flat.push(s);
    setPastSlots(flat.sort((a, b) => b.startUtc.localeCompare(a.startUtc)));
    // Tutti gli swap (anche pending) iniziati o ricevuti, ordinati per data desc
    const merged = [...outgoing, ...incoming]
      .filter((v, i, arr) => arr.findIndex(x => x.id === v.id) === i)
      .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc));
    setAllSwaps(merged);
  }, []);

  useEffect(() => { (async () => { try { await load(); } finally { setLoading(false); } })(); }, [load]);

  const onRefresh = async () => {
    setRefreshing(true); try { await load(); } finally { setRefreshing(false); }
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <View style={{ padding: theme.spacing.l }}>
        <SegmentedControl<Tab>
          value={tab}
          onChange={setTab}
          options={[
            { label: `Turni (${pastSlots.length})`, value: 'shifts' },
            { label: `Scambi (${allSwaps.length})`, value: 'swaps' },
          ]}
        />
      </View>

      {loading ? (
        <EmptyState title="Caricamento…" />
      ) : tab === 'shifts' ? (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: 0 }}
          data={pastSlots}
          keyExtractor={s => s.shiftId}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListEmptyComponent={<EmptyState icon="archive-outline" title="Nessun turno passato" />}
          renderItem={({ item }) => (
            <Card>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                <Icon
                  name={new Date(item.startUtc).getHours() < 12 ? 'sunny-outline' : 'moon-outline'}
                  size={20} color={theme.colors.primary}
                />
                <Text style={theme.typography.body}>{formatRange(item.startUtc, item.endUtc)}</Text>
              </View>
            </Card>
          )}
        />
      ) : (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: 0 }}
          data={allSwaps}
          keyExtractor={s => s.id}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListEmptyComponent={<EmptyState icon="swap-horizontal-outline" title="Nessuno scambio" />}
          renderItem={({ item }) => {
            const tone =
              item.status === 'AutoApproved' ? 'success' :
              item.status === 'Rejected' ? 'danger' :
              item.status === 'BlockedByRules' ? 'danger' :
              item.status === 'Cancelled' ? 'neutral' : 'warning';
            return (
              <Card>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                  <Text style={theme.typography.h3}>{item.type}</Text>
                  <Badge label={item.status} tone={tone as any} />
                </View>
                <Text style={theme.typography.caption}>
                  {formatRange(item.initiatorShiftStart, item.initiatorShiftEnd)}
                </Text>
                <Text style={theme.typography.caption}>
                  {item.initiatorName} → {item.counterpartName ?? '—'}
                </Text>
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
              </Card>
            );
          }}
        />
      )}
    </SafeAreaView>
  );
}
