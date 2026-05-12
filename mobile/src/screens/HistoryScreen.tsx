import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { FlatList, RefreshControl, SafeAreaView, Text, TouchableOpacity, View } from 'react-native';
import { Avatar, Badge, Card, EmptyState, Icon, SegmentedControl } from '../components/ui';
import {
  CalendarApi, ShiftDto, SwapDto, SwapsApi,
  formatDayLong, shiftCodeIcon, shiftCodeShort, shiftCodeTone,
  swapStatusLabel, swapStatusTone, swapTypeLabel,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';

type Tab = 'shifts' | 'swaps';
type Scope = 'mine' | 'all';

export default function HistoryScreen() {
  const { theme } = useTheme();
  const { locale } = useI18n();
  const [tab, setTab] = useState<Tab>('shifts');
  const [scope, setScope] = useState<Scope>('mine');
  const [myShifts, setMyShifts] = useState<ShiftDto[]>([]);
  const [allShifts, setAllShifts] = useState<ShiftDto[]>([]);
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

  const sourceShifts = scope === 'mine' ? myShifts : allShifts;

  const filteredShifts = useMemo(() => {
    const y = filterMonth.getFullYear(); const m = filterMonth.getMonth();
    return sourceShifts.filter(s => {
      const d = new Date(s.date);
      return d.getFullYear() === y && d.getMonth() === m;
    });
  }, [sourceShifts, filterMonth]);

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
    const mine: ShiftDto[] = [];
    const all: ShiftDto[] = [];
    for (const d of days) for (const s of d.shifts) {
      all.push(s);
      if (s.isMineTurno || s.isMineReperibile) mine.push(s);
    }
    // "I miei" desc (più recente in alto), "Tutti" asc (dal 1° al 30 del mese)
    const cmpDesc = (a: ShiftDto, b: ShiftDto) => b.startUtc.localeCompare(a.startUtc);
    const cmpAsc  = (a: ShiftDto, b: ShiftDto) => a.startUtc.localeCompare(b.startUtc);
    setMyShifts(mine.sort(cmpDesc));
    setAllShifts(all.sort(cmpAsc));
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

      {/* Filtro ambito: visibile solo nel tab Turni */}
      {tab === 'shifts' ? (
        <View style={{ paddingHorizontal: theme.spacing.l, paddingBottom: theme.spacing.s }}>
          <SegmentedControl<Scope>
            value={scope}
            onChange={setScope}
            options={[
              { label: 'Solo i miei', value: 'mine' },
              { label: 'Tutti', value: 'all' },
            ]}
          />
        </View>
      ) : null}

      {loading ? (
        <EmptyState title="Caricamento…" />
      ) : tab === 'shifts' ? (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: 0 }}
          data={filteredShifts}
          keyExtractor={s => s.id}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListEmptyComponent={
            <EmptyState
              icon="archive-outline"
              title={scope === 'mine' ? 'Nessun tuo turno in questo mese' : 'Nessun turno in questo mese'}
            />
          }
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

              {/* In modalità "Tutti": mostra chi era di turno e chi reperibile */}
              {scope === 'all' ? (
                <View style={{ marginTop: theme.spacing.s, gap: 4 }}>
                  {s.medicoTurno ? (
                    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <Avatar fullName={s.medicoTurno.fullName} url={s.medicoTurno.avatarUrl} size={22} />
                      <Text style={[theme.typography.caption, { flex: 1, fontWeight: s.isMineTurno ? '700' : '400' }]}>
                        {s.medicoTurno.fullName}
                      </Text>
                    </View>
                  ) : s.externalDoctor ? (
                    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <Icon name="person-add-outline" size={16} color={theme.colors.warning} />
                      <Text style={[theme.typography.caption, { flex: 1, fontWeight: '600' }]}>
                        {s.externalDoctor.fullName}
                      </Text>
                      <Badge label="Esterno" tone="warning" />
                    </View>
                  ) : (
                    <Text style={[theme.typography.caption, { fontStyle: 'italic' }]}>
                      Nessun medico di turno
                    </Text>
                  )}
                  {s.medicoReperibile ? (
                    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                      <Avatar fullName={s.medicoReperibile.fullName} url={s.medicoReperibile.avatarUrl} size={20} />
                      <Text style={[theme.typography.caption, {
                        flex: 1,
                        fontWeight: s.isMineReperibile ? '700' : '400',
                        color: theme.colors.textSecondary,
                      }]}>
                        {s.medicoReperibile.fullName}
                      </Text>
                    </View>
                  ) : null}
                </View>
              ) : null}
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
