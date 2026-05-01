import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { FlatList, RefreshControl, SafeAreaView, Text, TouchableOpacity, View } from 'react-native';
import { Badge, Card, EmptyState, Icon, SegmentedControl } from '../components/ui';
import {
  CalendarApi, ShiftDto, SwapDto, SwapsApi,
  formatDayLong, shiftCodeIcon, shiftCodeShort, shiftCodeTone,
  swapStatusLabel, swapStatusTone, swapTypeLabel,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';

type Tab = 'shifts' | 'swaps';

export default function HistoryScreen() {
  const { theme } = useTheme();
  const { locale } = useI18n();
  const [tab, setTab] = useState<Tab>('shifts');
  const [pastShifts, setPastShifts] = useState<ShiftDto[]>([]);
  const [allSwaps, setAllSwaps] = useState<SwapDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [monthOffset, setMonthOffset] = useState(0); // 0 = mese corrente

  // ---- filtro mese derivato dall'offset ----
  const filterMonth = useMemo(() => {
    const d = new Date(); d.setDate(1); d.setHours(0, 0, 0, 0);
    d.setMonth(d.getMonth() + monthOffset);
    return d;
  }, [monthOffset]);

  const monthLabel = filterMonth.toLocaleDateString(
    locale === 'it' ? 'it-IT' : 'en-GB',
    { month: 'long', year: 'numeric' },
  );

  const filteredShifts = useMemo(() => {
    const y = filterMonth.getFullYear(); const m = filterMonth.getMonth();
    return pastShifts.filter(s => {
      const d = new Date(s.date);
      return d.getFullYear() === y && d.getMonth() === m;
    });
  }, [pastShifts, filterMonth]);

  const filteredSwaps = useMemo(() => {
    const y = filterMonth.getFullYear(); const m = filterMonth.getMonth();
    return allSwaps.filter(s => {
      const d = new Date(s.initiatorShift.date);
      return d.getFullYear() === y && d.getMonth() === m;
    });
  }, [allSwaps, filterMonth]);

  // ---- caricamento: 24 mesi di storia ----
  const load = useCallback(async () => {
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const from = new Date(today); from.setMonth(from.getMonth() - 24);
    const to = new Date(today); to.setMonth(to.getMonth() + 3); // include anche futuri
    const [days, history] = await Promise.all([
      CalendarApi.list(from, to),
      SwapsApi.history(),
    ]);
    const flat: ShiftDto[] = [];
    for (const d of days) for (const s of d.shifts) {
      if (s.isMineTurno || s.isMineReperibile) flat.push(s);
    }
    setPastShifts(flat.sort((a, b) => b.startUtc.localeCompare(a.startUtc)));
    setAllSwaps(history.sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)));
  }, []);

  useEffect(() => { (async () => { try { await load(); } finally { setLoading(false); } })(); }, [load]);

  const onRefresh = async () => {
    setRefreshing(true); try { await load(); } finally { setRefreshing(false); }
  };

  // ---- Selettore mese ----
  const MonthPicker = () => (
    <View style={{
      flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
      paddingHorizontal: theme.spacing.l, paddingBottom: theme.spacing.s,
    }}>
      <TouchableOpacity onPress={() => setMonthOffset(o => o - 1)} hitSlop={12}>
        <Icon name="chevron-back-outline" size={26} color={theme.colors.primary} />
      </TouchableOpacity>
      <Text style={[theme.typography.h3, { textTransform: 'capitalize', flex: 1, textAlign: 'center' }]}>
        {monthLabel}
      </Text>
      <TouchableOpacity onPress={() => setMonthOffset(o => o + 1)} hitSlop={12}>
        <Icon name="chevron-forward-outline" size={26} color={theme.colors.primary} />
      </TouchableOpacity>
    </View>
  );

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <View style={{ padding: theme.spacing.l, paddingBottom: theme.spacing.s }}>
        <SegmentedControl<Tab>
          value={tab}
          onChange={setTab}
          options={[
            { label: `Turni (${filteredShifts.length})`, value: 'shifts' },
            { label: `Cessioni/Scambi (${filteredSwaps.length})`, value: 'swaps' },
          ]}
        />
      </View>
      <MonthPicker />

      {loading ? (
        <EmptyState title="Caricamento…" />
      ) : tab === 'shifts' ? (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: 0 }}
          data={filteredShifts}
          keyExtractor={s => s.id}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListEmptyComponent={<EmptyState icon="archive-outline" title="Nessun turno in questo mese" />}
          renderItem={({ item: s }) => (
            <Card>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                <Icon name={shiftCodeIcon(s.code) as any} size={20} color={theme.colors.primary} />
                <Badge label={s.code} tone={shiftCodeTone(s.code) as any} />
                <Text style={[theme.typography.body, { fontWeight: '600', textTransform: 'capitalize', flex: 1 }]}>
                  {formatDayLong(s.date, locale === 'it' ? 'it-IT' : 'en-GB')}
                </Text>
                {s.isMineTurno
                  ? <Badge label="Turno" tone="success" />
                  : s.isMineReperibile
                    ? <Badge label="Reperibile" tone="warning" />
                    : null}
              </View>
              <Text style={theme.typography.caption}>
                {shiftCodeShort(s.code)} · {s.startLocal} – {s.endLocal}
              </Text>
            </Card>
          )}
        />
      ) : (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: 0 }}
          data={filteredSwaps}
          keyExtractor={s => s.id}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListEmptyComponent={<EmptyState icon="swap-horizontal-outline" title="Nessuna cessione/scambio in questo mese" />}
          renderItem={({ item }) => (
            <Card>
              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <Text style={theme.typography.h3}>{swapTypeLabel(item.type)}</Text>
                <Badge label={swapStatusLabel(item.status)} tone={swapStatusTone(item.status) as any} />
              </View>
              <Text style={theme.typography.caption}>
                {item.initiatorShift.date} · {item.initiatorShift.code}
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
          )}
        />
      )}
    </SafeAreaView>
  );
}
