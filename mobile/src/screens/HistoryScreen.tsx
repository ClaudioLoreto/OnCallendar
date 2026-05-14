import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, FlatList, RefreshControl, SafeAreaView, Text, TouchableOpacity, View } from 'react-native';
import { Avatar, Badge, Card, EmptyState, Icon, SegmentedControl } from '../components/ui';
import {
  CalendarApi, ShiftDto, SwapDto, SwapStatus, SwapsApi, ReportsApi,
  formatDayLong, shiftCodeIcon, shiftCodeShort, shiftCodeTone,
  swapStatusLabel, swapStatusTone, swapTypeLabel,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';
import * as FileSystem from 'expo-file-system/legacy';
import * as Sharing from 'expo-sharing';
import { API_BASE_URL, getAuthToken } from '../api/apiClient';

type Tab = 'shifts' | 'swaps';
type Scope = 'mine' | 'all';
type SortOrder = 'asc' | 'desc';

export default function HistoryScreen() {
  const { theme } = useTheme();
  const { locale } = useI18n();
  const [tab, setTab] = useState<Tab>('shifts');
  const [scope, setScope] = useState<Scope>('mine');
  const [sortOrder, setSortOrder] = useState<SortOrder>('asc'); // Default ASC (dal 1° giorno)
  const [swapSortOrder, setSwapSortOrder] = useState<SortOrder>('desc'); // Default DESC (più recenti prima)
  const [myShifts, setMyShifts] = useState<ShiftDto[]>([]);
  const [allShifts, setAllShifts] = useState<ShiftDto[]>([]);
  const [allSwaps, setAllSwaps] = useState<SwapDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [monthOffset, setMonthOffset] = useState(0); // 0 = mese corrente
  const [showRejected, setShowRejected] = useState(false); // toggle per rifiutati nello storico scambi

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
    const filtered = sourceShifts.filter(s => {
      const d = new Date(s.date);
      return d.getFullYear() === y && d.getMonth() === m;
    });
    // Ordina in base a sortOrder
    const cmp = sortOrder === 'asc'
      ? (a: ShiftDto, b: ShiftDto) => a.startUtc.localeCompare(b.startUtc)
      : (a: ShiftDto, b: ShiftDto) => b.startUtc.localeCompare(a.startUtc);
    return filtered.sort(cmp);
  }, [sourceShifts, filterMonth, sortOrder]);

  const filteredSwaps = useMemo(() => {
    const y = filterMonth.getFullYear(); const m = filterMonth.getMonth();
    const filtered = allSwaps.filter(s => {
      const d = new Date(s.initiatorShift.date);
      if (d.getFullYear() !== y || d.getMonth() !== m) return false;
      // Mai mostrare le annullate
      if (s.status === SwapStatus.Cancelled) return false;
      // Rifiutate solo se toggle attivo
      if (s.status === SwapStatus.Rejected && !showRejected) return false;
      return true;
    });
    const cmp = swapSortOrder === 'asc'
      ? (a: SwapDto, b: SwapDto) => a.initiatorShift.date.localeCompare(b.initiatorShift.date)
      : (a: SwapDto, b: SwapDto) => b.initiatorShift.date.localeCompare(a.initiatorShift.date);
    return filtered.sort(cmp);
  }, [allSwaps, filterMonth, swapSortOrder, showRejected]);

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
    // Entrambi partiranno da ASC di default (dal 1° giorno del mese all'ultimo).
    // L'utente può invertire con la freccia.
    const cmpAsc = (a: ShiftDto, b: ShiftDto) => a.startUtc.localeCompare(b.startUtc);
    setMyShifts([...mine].sort(cmpAsc));
    setAllShifts([...all].sort(cmpAsc));
    setAllSwaps(history.sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)));
  }, []);

  useEffect(() => { (async () => { try { await load(); } finally { setLoading(false); } })(); }, [load]);

  // ---- Polling automatico: refresh ogni 30 secondi per live updates ----
  useEffect(() => {
    if (loading) return; // Non partire finché il primo load non è completato
    const interval = setInterval(async () => {
      try {
        await load();
      } catch (err) {
        console.error('Polling refresh error:', err);
      }
    }, 30000); // 30 secondi
    return () => clearInterval(interval);
  }, [loading, load]);

  const onRefresh = async () => {
    setRefreshing(true); try { await load(); } finally { setRefreshing(false); }
  };

  // ---- Export Excel ----
  const handleExportExcel = async () => {
    if (exporting) return;
    setExporting(true);
    try {
      const year = filterMonth.getFullYear();
      const month = filterMonth.getMonth() + 1;
      const endpoint = scope === 'mine'
        ? `/api/reports/my-history-excel?year=${year}&month=${month}`
        : `/api/reports/all-history-excel?year=${year}&month=${month}`;

      const fileName = scope === 'mine'
        ? `Storico_Personale_${year}_${String(month).padStart(2, '0')}.xlsx`
        : `Storico_Completo_${year}_${String(month).padStart(2, '0')}.xlsx`;
      const fileUri = `${FileSystem.cacheDirectory}${fileName}`;

      const token = getAuthToken();
      const downloadResult = await FileSystem.downloadAsync(
        `${API_BASE_URL}${endpoint}`,
        fileUri,
        { headers: token ? { Authorization: `Bearer ${token}` } : {} }
      );

      if (downloadResult.status === 200) {
        if (await Sharing.isAvailableAsync()) {
          await Sharing.shareAsync(fileUri, {
            mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            dialogTitle: fileName,
          });
        } else {
          Alert.alert('Download', `File pronto: ${fileName}`);
        }
      } else {
        throw new Error(`Download fallito (${downloadResult.status})`);
      }
    } catch (err: any) {
      Alert.alert('Errore', err?.message ?? 'Export fallito');
    } finally {
      setExporting(false);
    }
  };

  // ---- Export Excel scambi ----
  const handleExportSwapExcel = async () => {
    if (exporting) return;
    setExporting(true);
    try {
      const year = filterMonth.getFullYear();
      const month = filterMonth.getMonth() + 1;
      const endpoint = `/api/reports/swap-history-excel?year=${year}&month=${month}`;
      const fileName = `Scambi_${year}_${String(month).padStart(2, '0')}.xlsx`;
      const fileUri = `${FileSystem.cacheDirectory}${fileName}`;

      const token = getAuthToken();
      const downloadResult = await FileSystem.downloadAsync(
        `${API_BASE_URL}${endpoint}`,
        fileUri,
        { headers: token ? { Authorization: `Bearer ${token}` } : {} }
      );

      if (downloadResult.status === 200) {
        if (await Sharing.isAvailableAsync()) {
          await Sharing.shareAsync(fileUri, {
            mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            dialogTitle: fileName,
          });
        } else {
          Alert.alert('Download', `File pronto: ${fileName}`);
        }
      } else {
        throw new Error(`Download fallito (${downloadResult.status})`);
      }
    } catch (err: any) {
      Alert.alert('Errore', err?.message ?? 'Export fallito');
    } finally {
      setExporting(false);
    }
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
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.spacing.s }}>
            <View style={{ flex: 1 }}>
              <SegmentedControl<Scope>
                value={scope}
                onChange={setScope}
                options={[
                  { label: 'Solo i miei', value: 'mine' },
                  { label: 'Tutti', value: 'all' },
                ]}
              />
            </View>
            {/* Bottone ordinamento ASC/DESC */}
            <TouchableOpacity
              onPress={() => setSortOrder(o => o === 'asc' ? 'desc' : 'asc')}
              hitSlop={12}
              style={{
                backgroundColor: theme.colors.surfaceAlt,
                borderRadius: theme.radius.m,
                paddingVertical: theme.spacing.s,
                paddingHorizontal: theme.spacing.s,
                borderWidth: 1,
                borderColor: theme.colors.border,
                flexDirection: 'row',
                alignItems: 'center',
                gap: 2,
              }}
            >
              <Icon
                name="arrow-up"
                size={16}
                color={sortOrder === 'asc' ? theme.colors.primary : theme.colors.textMuted}
              />
              <Icon
                name="arrow-down"
                size={16}
                color={sortOrder === 'desc' ? theme.colors.primary : theme.colors.textMuted}
              />
            </TouchableOpacity>
            {/* Bottone export Excel */}
            <TouchableOpacity
              onPress={handleExportExcel}
              disabled={exporting}
              hitSlop={12}
              style={{
                backgroundColor: exporting ? theme.colors.border : theme.colors.success,
                borderRadius: theme.radius.m,
                padding: theme.spacing.s,
                borderWidth: 1,
                borderColor: theme.colors.border,
                opacity: exporting ? 0.6 : 1,
              }}
            >
              <Icon
                name={exporting ? 'hourglass-outline' : 'download-outline'}
                size={20}
                color={exporting ? theme.colors.text : theme.colors.white}
              />
            </TouchableOpacity>
          </View>
        </View>
      ) : (
        <View style={{ paddingHorizontal: theme.spacing.l, paddingBottom: theme.spacing.s }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'flex-end', gap: theme.spacing.s }}>
            {/* Toggle rifiutati */}
            <TouchableOpacity
              onPress={() => setShowRejected(v => !v)}
              hitSlop={12}
              style={{
                backgroundColor: showRejected ? theme.colors.accent : theme.colors.surfaceAlt,
                borderRadius: theme.radius.m,
                paddingVertical: theme.spacing.s,
                paddingHorizontal: theme.spacing.m,
                borderWidth: 1,
                borderColor: showRejected ? theme.colors.primary : theme.colors.border,
                flexDirection: 'row',
                alignItems: 'center',
                gap: 4,
              }}
            >
              <Icon
                name={showRejected ? 'eye' : 'eye-off-outline'}
                size={14}
                color={showRejected ? theme.colors.primary : theme.colors.textMuted}
              />
              <Text style={[theme.typography.caption, { color: showRejected ? theme.colors.primary : theme.colors.textMuted }]}>
                Rifiutati
              </Text>
            </TouchableOpacity>
            {/* Bottone ordinamento ASC/DESC scambi */}
            <TouchableOpacity
              onPress={() => setSwapSortOrder(o => o === 'asc' ? 'desc' : 'asc')}
              hitSlop={12}
              style={{
                backgroundColor: theme.colors.surfaceAlt,
                borderRadius: theme.radius.m,
                paddingVertical: theme.spacing.s,
                paddingHorizontal: theme.spacing.s,
                borderWidth: 1,
                borderColor: theme.colors.border,
                flexDirection: 'row',
                alignItems: 'center',
                gap: 2,
              }}
            >
              <Icon
                name="arrow-up"
                size={16}
                color={swapSortOrder === 'asc' ? theme.colors.primary : theme.colors.textMuted}
              />
              <Icon
                name="arrow-down"
                size={16}
                color={swapSortOrder === 'desc' ? theme.colors.primary : theme.colors.textMuted}
              />
            </TouchableOpacity>
            {/* Bottone export Excel scambi */}
            <TouchableOpacity
              onPress={handleExportSwapExcel}
              disabled={exporting}
              hitSlop={12}
              style={{
                backgroundColor: exporting ? theme.colors.border : theme.colors.success,
                borderRadius: theme.radius.m,
                padding: theme.spacing.s,
                borderWidth: 1,
                borderColor: theme.colors.border,
                opacity: exporting ? 0.6 : 1,
              }}
            >
              <Icon
                name={exporting ? 'hourglass-outline' : 'download-outline'}
                size={20}
                color={exporting ? theme.colors.text : theme.colors.white}
              />
            </TouchableOpacity>
          </View>
        </View>
      )}

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
