import React, { useCallback, useEffect, useState } from 'react';
import {
  FlatList, RefreshControl, SafeAreaView, ScrollView, Text, TouchableOpacity, View,
} from 'react-native';
import { Avatar, Badge, Button, Card, EmptyState, Icon, Sheet } from '../components/ui';
import AppHeader from '../components/AppHeader';
import AssignExternalSheet from '../components/AssignExternalSheet';
import {
  CalendarApi, DayDto, ShiftDto, ShiftStatus, ShiftsApi,
  shiftCodeIcon, shiftCodeShort, shiftCodeTone,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';

type Props = { navigation: { navigate: (route: string, params?: any) => void } };

export default function CalendarScreen({ navigation }: Props) {
  const { theme } = useTheme();
  const { t, locale } = useI18n();

  const [days, setDays] = useState<DayDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Filtri retraibili
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [showOnlyMine, setShowOnlyMine] = useState(false);
  const [rangeDays, setRangeDays] = useState(14);

  // Sheet azioni sul mio turno
  const [actionShift, setActionShift] = useState<ShiftDto | null>(null);
  const [actionIsReperibile, setActionIsReperibile] = useState(false);
  const [externalShift, setExternalShift] = useState<ShiftDto | null>(null);

  const load = useCallback(async () => {
    setError(null);
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const to = new Date(today); to.setDate(to.getDate() + rangeDays);
    const d = await CalendarApi.list(today, to);
    setDays(d);
  }, [rangeDays]);

  useEffect(() => {
    (async () => {
      try { await load(); }
      catch (e: any) { setError(e?.response?.data?.error ?? e?.message ?? 'Errore'); }
      finally { setLoading(false); }
    })();
  }, [load]);

  // ── Polling automatico: refresh ogni 30 secondi per live updates ────
  useEffect(() => {
    if (loading) return;
    const interval = setInterval(async () => {
      try {
        await load();
      } catch (err) {
        console.error('Polling refresh error:', err);
      }
    }, 30000);
    return () => clearInterval(interval);
  }, [loading, load]);

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

  // ---- Azioni sul mio turno ----
  const openActions = (s: ShiftDto) => {
    if (s.isPast) return;
    if (s.isMineTurno) {
      setActionIsReperibile(false);
      setActionShift(s);
    } else if (s.isMineReperibile) {
      setActionIsReperibile(true);
      setActionShift(s);
    }
  };
  const goToWizard = (mode: 'cessione' | 'scambio') => {
    if (!actionShift) return;
    const id = actionShift.id;
    const rep = actionIsReperibile;
    setActionShift(null);
    navigation.navigate('Swaps', { initialShiftId: id, initialMode: mode, isReperibile: rep });
  };

  // ---- Render ----
  const renderShift = (s: ShiftDto) => {
    const isMine = s.isMineTurno || s.isMineReperibile;
    const onBoard = s.status === ShiftStatus.OnBoard;
    const dim = s.isPast && !isMine;

    return (
      <TouchableOpacity
        key={s.id}
        activeOpacity={(s.isMineTurno || s.isMineReperibile) && !s.isPast ? 0.7 : 1}
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
              </Text>
            </>
          ) : (
            <Text style={[theme.typography.caption, { fontStyle: 'italic' }]}>Nessun medico assegnato</Text>
          )}
        </View>

        {/* Medico reperibile */}
        {s.medicoReperibile ? (
          <View style={{
            flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 6,
          }}>
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

        {/* Medico esterno (copre il turno) */}
        {s.externalDoctor ? (
          <View style={{
            flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 6,
            backgroundColor: theme.colors.surfaceAlt,
            borderRadius: theme.radius.s,
            paddingVertical: 6, paddingHorizontal: 8,
          }}>
            <Icon name="person-add-outline" size={16} color={theme.colors.warning} />
            <Text style={[theme.typography.caption, { flex: 1, fontWeight: '700' }]}>
              Coperto da: {s.externalDoctor.fullName}
            </Text>
            <Badge label="Esterno" tone="warning" />
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
          {today
            ? <Badge label="Oggi" tone="info" />
            : item.shifts.some(s => s.isMineTurno)
              ? <Badge label="Sei in turno" tone="success" />
              : item.shifts.some(s => s.isMineReperibile)
                ? <Badge label="Sei reperibile" tone="warning" />
                : null}
        </View>
        {item.shifts.map(renderShift)}
      </Card>
    );
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <AppHeader title={t('calendar.title')} onAvatarPress={() => navigation.navigate('Profile')} onBellPress={() => navigation.navigate('Notifications')} />

      {/* Barra filtri retraibile */}
      <View style={{ paddingHorizontal: theme.spacing.l, paddingBottom: theme.spacing.s }}>
        <TouchableOpacity
          onPress={() => setFiltersOpen(o => !o)}
          activeOpacity={0.7}
          style={{
            flexDirection: 'row', alignItems: 'center', gap: 6,
            paddingVertical: 6, paddingHorizontal: 10,
            backgroundColor: theme.colors.surfaceAlt,
            borderRadius: 16, alignSelf: 'flex-start',
          }}
        >
          <Icon name="options-outline" size={14} color={theme.colors.textSecondary} />
          <Text style={[theme.typography.caption, { fontWeight: '700' }]}>
            {showOnlyMine ? 'Solo miei' : 'Tutti'} · {rangeDays}gg
          </Text>
          <Icon name={filtersOpen ? 'chevron-up' : 'chevron-down'} size={14} color={theme.colors.textSecondary} />
        </TouchableOpacity>

        {filtersOpen ? (
          <View style={{
            marginTop: 8,
            backgroundColor: theme.colors.surface,
            borderRadius: theme.radius.m,
            padding: theme.spacing.m,
            borderWidth: 1, borderColor: theme.colors.border,
          }}>
            <Text style={[theme.typography.caption, { fontWeight: '700', marginBottom: 6 }]}>Mostra</Text>
            <View style={{ flexDirection: 'row', gap: 8, marginBottom: 12 }}>
              {(['only', 'all'] as const).map(k => {
                const active = (k === 'only') === showOnlyMine;
                return (
                  <TouchableOpacity
                    key={k}
                    onPress={() => setShowOnlyMine(k === 'only')}
                    activeOpacity={0.7}
                    style={{
                      paddingVertical: 6, paddingHorizontal: 12,
                      borderRadius: 14,
                      backgroundColor: active ? theme.colors.primary : theme.colors.surfaceAlt,
                    }}
                  >
                    <Text style={{ color: active ? '#fff' : theme.colors.textSecondary, fontSize: 12, fontWeight: '700' }}>
                      {k === 'only' ? 'Solo miei' : 'Tutti'}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>

            <Text style={[theme.typography.caption, { fontWeight: '700', marginBottom: 6 }]}>Giorni mostrati</Text>
            <View style={{ flexDirection: 'row', gap: 8, flexWrap: 'wrap' }}>
              {[7, 14, 30, 60, 90].map(n => {
                const active = rangeDays === n;
                return (
                  <TouchableOpacity
                    key={n}
                    onPress={() => setRangeDays(n)}
                    activeOpacity={0.7}
                    style={{
                      paddingVertical: 6, paddingHorizontal: 12,
                      borderRadius: 14,
                      backgroundColor: active ? theme.colors.primary : theme.colors.surfaceAlt,
                    }}
                  >
                    <Text style={{ color: active ? '#fff' : theme.colors.textSecondary, fontSize: 12, fontWeight: '700' }}>
                      {n}gg
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>
          </View>
        ) : null}
      </View>

      {loading ? (
        <EmptyState title={t('common.loading')} />
      ) : (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: theme.spacing.s }}
          data={showOnlyMine
            ? days.filter(d => d.shifts.some(s => s.isMineTurno || s.isMineReperibile))
            : days}
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
      <Sheet visible={!!actionShift} onClose={() => setActionShift(null)} title={actionIsReperibile ? 'Gestisci reperibilità' : 'Gestisci turno'}>
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

            {actionShift.externalDoctor ? (
              <View style={{ gap: theme.spacing.s }}>
                <View style={{
                  padding: theme.spacing.m, borderRadius: theme.radius.m,
                  backgroundColor: theme.colors.surfaceAlt, marginBottom: theme.spacing.s,
                  borderWidth: 1, borderColor: theme.colors.border,
                  flexDirection: 'row', alignItems: 'center', gap: 8,
                }}>
                  <Icon name="person-add-outline" size={18} color={theme.colors.warning} />
                  <Text style={[theme.typography.body, { flex: 1, fontWeight: '600' }]}>
                    Coperto da: {actionShift.externalDoctor.fullName}
                  </Text>
                </View>
                <Button
                  title="Rimuovi turno dottore esterno"
                  icon="close-circle-outline"
                  variant="danger"
                  onPress={async () => {
                    const s = actionShift;
                    if (!s) return;
                    try {
                      const updated = await ShiftsApi.clearExternal(s.id);
                      setDays(prev => prev.map(d => ({
                        ...d,
                        shifts: d.shifts.map(x => x.id === updated.id ? updated : x),
                      })));
                      setActionShift(null);
                    } catch {
                      setActionShift(null);
                    }
                  }}
                />
                <Button
                  title="Annulla"
                  variant="subtle"
                  onPress={() => setActionShift(null)}
                />
              </View>
            ) : (
              <View style={{ gap: theme.spacing.s }}>
                <Button
                  title={actionIsReperibile ? 'Cedi reperibilità' : 'Proponi cessione'}
                  icon="arrow-forward-circle-outline"
                  onPress={() => goToWizard('cessione')}
                />
                <Button
                  title={actionIsReperibile ? 'Scambia reperibilità' : 'Proponi scambio'}
                  icon="swap-horizontal-outline"
                  variant="secondary"
                  onPress={() => goToWizard('scambio')}
                />
                <Button
                  title={actionIsReperibile ? 'Affida reperibilit\u00e0 a esterno' : 'Affida a medico esterno'}
                  icon="person-add-outline"
                  variant="secondary"
                  onPress={() => {
                    const s = actionShift;
                    setActionShift(null);
                    setExternalShift(s);
                  }}
                />
              </View>
            )}
          </ScrollView>
        ) : null}
      </Sheet>

      {/* Sheet assegnazione medico esterno */}
      <AssignExternalSheet
        shift={externalShift}
        isReperibile={actionIsReperibile}
        onClose={() => setExternalShift(null)}
        onUpdated={(updated) => {
          // Aggiorno il turno nella lista in memoria
          setDays(prev => prev.map(d => ({
            ...d,
            shifts: d.shifts.map(x => x.id === updated.id ? updated : x),
          })));
        }}
      />
    </SafeAreaView>
  );
}
