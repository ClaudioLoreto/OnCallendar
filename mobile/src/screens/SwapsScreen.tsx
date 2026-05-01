import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert, FlatList, RefreshControl, SafeAreaView, ScrollView, Text, TouchableOpacity, View,
} from 'react-native';
import {
  Avatar, Badge, Button, Card, EmptyState, Field, Icon, SegmentedControl, Sheet,
} from '../components/ui';
import AppHeader from '../components/AppHeader';
import {
  CalendarApi, DayDto, MedicoDto, ShiftDto, SwapDto, SwapStatus, SwapsApi, UsersApi,
  CounterOfferDto,
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
  const [mode, setMode] = useState<'cessione' | 'scambio'>('cessione');
  const [days, setDays] = useState<DayDto[]>([]);
  const [colleagues, setColleagues] = useState<MedicoDto[]>([]);
  const [pickedShift, setPickedShift] = useState<ShiftDto | null>(null);
  const [pickedColleagues, setPickedColleagues] = useState<Set<string>>(new Set());
  const [pickedCounterShiftIds, setPickedCounterShiftIds] = useState<Set<string>>(new Set());
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [stepError, setStepError] = useState<string | null>(null);

  // Modal custom: avviso soglia di tutela (riposo/lavoro continuativo)
  const [warnOpen, setWarnOpen] = useState(false);
  const [warnViolations, setWarnViolations] = useState<Array<{ message: string; ruleCode?: string }>>([]);
  const [warnConfirmFn, setWarnConfirmFn] = useState<(() => Promise<void>) | null>(null);

  // ----- Trattative -----
  const [counterSwap, setCounterSwap] = useState<SwapDto | null>(null);
  const [counterOffers, setCounterOffers] = useState<CounterOfferDto[]>([]);
  const [counterDays, setCounterDays] = useState<DayDto[]>([]);
  const [counterPicked, setCounterPicked] = useState<ShiftDto | null>(null);
  const [counterMsg, setCounterMsg] = useState('');
  const [counterBusy, setCounterBusy] = useState(false);
  const [counterErr, setCounterErr] = useState<string | null>(null);

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
      if (action === 'accept') await acceptWithWarningGuard(id, false);
      else if (action === 'reject') await SwapsApi.reject(id);
      else await SwapsApi.cancel(id);
      await loadLists();
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setBusy(null);
    }
  };

  /**
   * Tenta l'accept. Se il backend torna 422 con canForce=true, mostra DUE
   * conferme in cascata all'utente (la 12h è una soglia di tutela, non un
   * divieto: la reperibilità è standby, non lavoro effettivo). Se l'utente
   * conferma entrambe, ripete con force=true.
   */
  const acceptWithWarningGuard = async (id: string, force: boolean): Promise<void> => {
    try {
      await SwapsApi.accept(id, force);
    } catch (e: any) {
      const data = e?.response?.data;
      if (e?.response?.status === 422 && data?.canForce === true && !force) {
        const violations: Array<{ message: string; ruleCode?: string }> = data.violations ?? [];
        await new Promise<void>((resolve, reject) => {
          setWarnViolations(violations);
          setWarnConfirmFn(() => async () => {
            try { await SwapsApi.accept(id, true); resolve(); }
            catch (e2) { reject(e2); }
          });
          setWarnOpen(true);
          // user closes the modal → reject if no confirm
          // resolve/reject are handled inside confirm or closeWarning
        });
      } else {
        throw e;
      }
    }
  };

  const closeWarning = () => {
    setWarnOpen(false);
    setWarnViolations([]);
    setWarnConfirmFn(null);
  };
  const confirmWarning = async () => {
    const fn = warnConfirmFn;
    setWarnOpen(false);
    if (fn) {
      try { await fn(); } catch {}
    }
    setWarnConfirmFn(null);
    setWarnViolations([]);
  };

  // ---- nuova richiesta ----
  const openNew = async () => {
    setNewOpen(true);
    setMode('cessione');
    setPickedShift(null); setPickedColleagues(new Set()); setPickedCounterShiftIds(new Set());
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

  // Date dei miei turni → set di stringhe (per evitare candidati nello stesso giorno)
  const myShiftDates = useMemo<Set<string>>(() => {
    const s = new Set<string>();
    for (const d of days) for (const sh of d.shifts) if (sh.isMineTurno && !sh.isPast) s.add(sh.date);
    return s;
  }, [days]);

  // Turni candidati per scambio: tutti i turni futuri dei colleghi selezionati,
  // raggruppati per collega. Esclude i turni in giorni in cui ho già un mio turno
  // (per evitare che il sistema rifiuti subito per sovrapposizione).
  type ColleagueGroup = { medico: MedicoDto; shifts: ShiftDto[] };
  const counterGroups = useMemo<ColleagueGroup[]>(() => {
    if (mode !== 'scambio' || pickedColleagues.size === 0) return [];
    const out: ColleagueGroup[] = [];
    for (const m of colleagues) {
      if (!pickedColleagues.has(m.id)) continue;
      const shifts: ShiftDto[] = [];
      for (const d of days) for (const s of d.shifts) {
        if (s.isPast) continue;
        if (s.medicoTurno?.id !== m.id) continue;
        if (myShiftDates.has(s.date) && s.date !== pickedShift?.date) continue;
        shifts.push(s);
      }
      if (shifts.length > 0) out.push({ medico: m, shifts });
    }
    return out;
  }, [days, mode, pickedColleagues, colleagues, myShiftDates, pickedShift]);

  const submitNew = async () => {
    if (!pickedShift || pickedColleagues.size === 0) return;
    setSubmitting(true); setStepError(null);
    try {
      if (mode === 'scambio') {
        if (pickedCounterShiftIds.size === 0) {
          setStepError('Seleziona almeno un turno candidato dei colleghi.');
          setSubmitting(false);
          return;
        }
        await SwapsApi.swapMulti(pickedShift.id, Array.from(pickedCounterShiftIds), message || undefined);
      } else {
        await SwapsApi.giveawayMulti(pickedShift.id, Array.from(pickedColleagues), message || undefined);
      }
      setNewOpen(false);
      setTab('outgoing');           // mostra subito la nuova richiesta in "Inviati"
      await loadLists();
    } catch (e: any) {
      const code = e?.response?.status;
      const msg = e?.response?.data?.error ?? e?.message ?? 'Errore invio';
      setStepError(code === 409 ? 'Esiste già una richiesta in sospeso per questo turno.' : msg);
    } finally {
      setSubmitting(false);
    }
  };

  // ---- trattative ----
  const openCounter = async (swap: SwapDto) => {
    setCounterSwap(swap);
    setCounterPicked(null);
    setCounterMsg('');
    setCounterErr(null);
    setCounterOffers([]);
    try {
      const today = new Date(); today.setHours(0, 0, 0, 0);
      const to = new Date(today); to.setDate(to.getDate() + 30);
      const [d, offers] = await Promise.all([
        CalendarApi.list(today, to),
        SwapsApi.listCounters(swap.id),
      ]);
      setCounterDays(d);
      setCounterOffers(offers);
    } catch (e: any) {
      setCounterErr(e?.response?.data?.error ?? e?.message ?? 'Errore caricamento');
    }
  };

  const myCounterShifts = useMemo<ShiftDto[]>(() => {
    const out: ShiftDto[] = [];
    for (const d of counterDays) for (const s of d.shifts) {
      if (s.isMineTurno && !s.isPast) out.push(s);
    }
    return out;
  }, [counterDays]);

  const submitCounter = async () => {
    if (!counterSwap || !counterPicked) return;
    setCounterBusy(true); setCounterErr(null);
    try {
      await SwapsApi.proposeCounter(counterSwap.id, counterPicked.id, counterMsg || undefined);
      setCounterSwap(null);
      await loadLists();
    } catch (e: any) {
      setCounterErr(e?.response?.data?.error ?? e?.message ?? 'Errore invio');
    } finally {
      setCounterBusy(false);
    }
  };

  const acceptCounterOffer = async (swapId: string, offerId: string) => {
    setCounterBusy(true); setCounterErr(null);
    try {
      await SwapsApi.acceptCounter(swapId, offerId, false);
      setCounterSwap(null);
      await loadLists();
    } catch (e: any) {
      const data = e?.response?.data;
      if (e?.response?.status === 422 && data?.canForce === true) {
        const list = (data.violations ?? []).map((v: any) => `• ${v.message}`).join('\n');
        Alert.alert('⚠️ Soglia di tutela superata', `${list}\n\nProcedere comunque?`, [
          { text: 'Annulla', style: 'cancel' },
          {
            text: 'Sì, accetto', style: 'destructive',
            onPress: async () => {
              try {
                await SwapsApi.acceptCounter(swapId, offerId, true);
                setCounterSwap(null);
                await loadLists();
              } catch (e2: any) {
                setCounterErr(e2?.response?.data?.error ?? e2?.message ?? 'Errore');
              }
            },
          },
        ]);
      } else {
        setCounterErr(data?.error ?? e?.message ?? 'Errore');
      }
    } finally {
      setCounterBusy(false);
    }
  };

  const rejectCounterOffer = async (swapId: string, offerId: string) => {
    setCounterBusy(true); setCounterErr(null);
    try {
      await SwapsApi.rejectCounter(swapId, offerId);
      const fresh = await SwapsApi.listCounters(swapId);
      setCounterOffers(fresh);
    } catch (e: any) {
      setCounterErr(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setCounterBusy(false);
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
      <AppHeader title={t('swaps.title')} onAvatarPress={() => navigation.navigate('Profile')} onBellPress={() => navigation.navigate('Notifications')} />

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
                  <View style={{ marginTop: theme.spacing.m, gap: theme.spacing.s }}>
                    <View style={{ flexDirection: 'row', gap: theme.spacing.s }}>
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
                    <Button title="Controproposta" variant="subtle" icon="repeat-outline"
                      onPress={() => openCounter(item)} />
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
          {/* Tipo richiesta */}
          <View style={{ flexDirection: 'row', gap: theme.spacing.s, marginBottom: theme.spacing.l }}>
            <TouchableOpacity
              onPress={() => { setMode('cessione'); setPickedCounterShiftIds(new Set()); }}
              style={{
                flex: 1, padding: theme.spacing.m, borderRadius: theme.radius.m,
                borderWidth: 2,
                borderColor: mode === 'cessione' ? theme.colors.primary : theme.colors.border,
                backgroundColor: mode === 'cessione' ? theme.colors.accent : theme.colors.surface,
                alignItems: 'center', gap: 6,
              }}>
              <Icon name="arrow-forward-circle-outline" size={22} color={mode === 'cessione' ? theme.colors.primary : theme.colors.textSecondary} />
              <Text style={{ fontWeight: '700', color: mode === 'cessione' ? theme.colors.primary : theme.colors.textPrimary }}>
                Cessione
              </Text>
              <Text style={[theme.typography.caption, { textAlign: 'center' }]}>
                Cedi un tuo turno (senza contropartita)
              </Text>
            </TouchableOpacity>
            <TouchableOpacity
              onPress={() => {
                setMode('scambio');
                setPickedCounterShiftIds(new Set());
              }}
              style={{
                flex: 1, padding: theme.spacing.m, borderRadius: theme.radius.m,
                borderWidth: 2,
                borderColor: mode === 'scambio' ? theme.colors.primary : theme.colors.border,
                backgroundColor: mode === 'scambio' ? theme.colors.accent : theme.colors.surface,
                alignItems: 'center', gap: 6,
              }}>
              <Icon name="swap-horizontal-outline" size={22} color={mode === 'scambio' ? theme.colors.primary : theme.colors.textSecondary} />
              <Text style={{ fontWeight: '700', color: mode === 'scambio' ? theme.colors.primary : theme.colors.textPrimary }}>
                Scambio
              </Text>
              <Text style={[theme.typography.caption, { textAlign: 'center' }]}>
                Cedi un turno e ne prendi uno in cambio
              </Text>
            </TouchableOpacity>
          </View>

          <Text style={[theme.typography.body, { fontWeight: '700', marginBottom: theme.spacing.s }]}>
            <Icon name="calendar-outline" size={16} color={theme.colors.textPrimary} />  1. Quale turno {mode === 'scambio' ? 'offri' : 'cedi'}?
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
                <Icon name="people-outline" size={16} color={theme.colors.textPrimary} />
                {'  '}2. {mode === 'scambio' ? 'Con quali colleghi puoi scambiare?' : 'A chi cedi? (seleziona uno o più)'}
              </Text>
              {colleagues.map(m => {
                const active = pickedColleagues.has(m.id);
                return (
                  <TouchableOpacity
                    key={m.id}
                    onPress={() => {
                      setPickedColleagues(prev => {
                        const next = new Set(prev);
                        if (next.has(m.id)) {
                          next.delete(m.id);
                          // togli anche i suoi turni candidati selezionati
                          setPickedCounterShiftIds(prevIds => {
                            const ids = new Set(prevIds);
                            for (const d of days) for (const s of d.shifts) {
                              if (s.medicoTurno?.id === m.id) ids.delete(s.id);
                            }
                            return ids;
                          });
                        } else {
                          next.add(m.id);
                        }
                        return next;
                      });
                    }}
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
                    <View style={{
                      width: 22, height: 22, borderRadius: 4,
                      borderWidth: 2,
                      borderColor: active ? theme.colors.primary : theme.colors.border,
                      backgroundColor: active ? theme.colors.primary : 'transparent',
                      alignItems: 'center', justifyContent: 'center',
                    }}>
                      {active ? <Icon name="checkmark" size={14} color="#fff" /> : null}
                    </View>
                  </TouchableOpacity>
                );
              })}
              {pickedColleagues.size > 1 ? (
                <Text style={[theme.typography.caption, { color: theme.colors.textSecondary, marginBottom: theme.spacing.s }]}>
                  {mode === 'cessione'
                    ? 'Il primo che accetta prende il turno; gli altri ricevono cancellazione automatica.'
                    : 'Verranno create più richieste; appena una viene accettata le altre si cancellano da sole.'}
                </Text>
              ) : null}

              {/* Selezione turni candidati per scambio (raggruppati per collega) */}
              {mode === 'scambio' && pickedColleagues.size >= 1 ? (
                <>
                  <Text style={[theme.typography.body, { fontWeight: '700', marginBottom: theme.spacing.s, marginTop: theme.spacing.m }]}>
                    <Icon name="repeat-outline" size={16} color={theme.colors.textPrimary} />  3. Quali turni potresti prendere in cambio? (seleziona uno o più)
                  </Text>
                  {counterGroups.length === 0 ? (
                    <View style={{ padding: theme.spacing.m, backgroundColor: theme.colors.surfaceAlt, borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
                      <Text style={theme.typography.caption}>
                        Nessun turno candidato disponibile (nessun turno futuro nei colleghi scelti, oppure cadono in giorni in cui hai già un tuo turno).
                      </Text>
                    </View>
                  ) : (
                    counterGroups.map(g => (
                      <View key={g.medico.id} style={{ marginBottom: theme.spacing.m }}>
                        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: theme.spacing.s }}>
                          <Avatar fullName={g.medico.fullName} url={g.medico.avatarUrl} size={22} />
                          <Text style={[theme.typography.caption, { fontWeight: '700' }]}>{g.medico.fullName}</Text>
                          <Text style={theme.typography.caption}>· {g.shifts.length} turni</Text>
                        </View>
                        <View style={{ gap: 6 }}>
                          {g.shifts.map(s => {
                            const active = pickedCounterShiftIds.has(s.id);
                            return (
                              <TouchableOpacity
                                key={s.id}
                                onPress={() => setPickedCounterShiftIds(prev => {
                                  const next = new Set(prev);
                                  if (next.has(s.id)) next.delete(s.id); else next.add(s.id);
                                  return next;
                                })}
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
                                <View style={{
                                  width: 22, height: 22, borderRadius: 4,
                                  borderWidth: 2,
                                  borderColor: active ? theme.colors.primary : theme.colors.border,
                                  backgroundColor: active ? theme.colors.primary : 'transparent',
                                  alignItems: 'center', justifyContent: 'center',
                                }}>
                                  {active ? <Icon name="checkmark" size={14} color="#fff" /> : null}
                                </View>
                              </TouchableOpacity>
                            );
                          })}
                        </View>
                      </View>
                    ))
                  )}
                </>
              ) : null}

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
                title={
                  mode === 'scambio'
                    ? (pickedCounterShiftIds.size > 1 ? `Proponi a ${pickedCounterShiftIds.size}` : 'Proponi scambio')
                    : pickedColleagues.size > 1 ? `Invia a ${pickedColleagues.size}` : 'Invia'
                }
                icon="send"
                loading={submitting}
                disabled={
                  !pickedShift || pickedColleagues.size === 0 ||
                  (mode === 'scambio' && pickedCounterShiftIds.size === 0)
                }
                onPress={submitNew}
              />
            </View>
          </View>
        </ScrollView>
      </Sheet>

      {/* Sheet trattativa / controproposte */}
      <Sheet visible={!!counterSwap} onClose={() => setCounterSwap(null)} title="Trattativa">
        <ScrollView>
          {counterSwap ? (
            <>
              <View style={{ backgroundColor: theme.colors.surfaceAlt, padding: theme.spacing.m, borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
                <Text style={[theme.typography.caption, { fontWeight: '700' }]}>Richiesta originale</Text>
                <Text style={theme.typography.body}>{briefLabel(counterSwap.initiatorShift)}</Text>
                <Text style={theme.typography.caption}>
                  da {counterSwap.initiatorName}{counterSwap.counterpartName ? ` → ${counterSwap.counterpartName}` : ''}
                </Text>
              </View>

              <Text style={[theme.typography.body, { fontWeight: '700', marginBottom: theme.spacing.s }]}>
                Controproposte ({counterOffers.length})
              </Text>
              {counterOffers.length === 0 ? (
                <Text style={[theme.typography.caption, { marginBottom: theme.spacing.m }]}>
                  Nessuna controproposta ancora.
                </Text>
              ) : (
                <View style={{ gap: theme.spacing.s, marginBottom: theme.spacing.m }}>
                  {counterOffers.map(o => {
                    const mine = o.proposedById === user?.id;
                    return (
                      <View key={o.id} style={{
                        padding: theme.spacing.m, borderRadius: theme.radius.m,
                        backgroundColor: mine ? theme.colors.accent : theme.colors.surface,
                        borderWidth: 1, borderColor: theme.colors.border,
                      }}>
                        <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: 4 }}>
                          <Text style={{ fontWeight: '700' }}>{mine ? 'Tu' : o.proposedByName}</Text>
                          <Badge label={o.status}
                            tone={o.status === 'Pending' ? 'warning' : o.status === 'Accepted' ? 'success' : 'neutral' as any} />
                        </View>
                        <Text style={theme.typography.body}>Offre: {briefLabel(o.offeredShift)}</Text>
                        {o.message ? (
                          <Text style={[theme.typography.caption, { fontStyle: 'italic', marginTop: 4 }]}>«{o.message}»</Text>
                        ) : null}
                        {o.status === 'Pending' && !mine ? (
                          <View style={{ flexDirection: 'row', gap: theme.spacing.s, marginTop: theme.spacing.s }}>
                            <View style={{ flex: 1 }}>
                              <Button title="Accetta" icon="checkmark" loading={counterBusy}
                                onPress={() => acceptCounterOffer(counterSwap.id, o.id)} />
                            </View>
                            <View style={{ flex: 1 }}>
                              <Button title="Rifiuta" variant="danger" icon="close" loading={counterBusy}
                                onPress={() => rejectCounterOffer(counterSwap.id, o.id)} />
                            </View>
                          </View>
                        ) : null}
                      </View>
                    );
                  })}
                </View>
              )}

              <Text style={[theme.typography.body, { fontWeight: '700', marginBottom: theme.spacing.s, marginTop: theme.spacing.s }]}>
                Proponi un tuo turno in cambio
              </Text>
              {myCounterShifts.length === 0 ? (
                <View style={{ padding: theme.spacing.m, backgroundColor: theme.colors.surfaceAlt, borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
                  <Text style={theme.typography.caption}>Non hai turni futuri da offrire nei prossimi 30 giorni.</Text>
                </View>
              ) : (
                <View style={{ gap: 8, marginBottom: theme.spacing.m }}>
                  {myCounterShifts.map(s => {
                    const active = counterPicked?.id === s.id;
                    return (
                      <TouchableOpacity
                        key={s.id}
                        onPress={() => setCounterPicked(s)}
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

              <Field label="Messaggio (opzionale)" value={counterMsg} onChangeText={setCounterMsg} multiline />

              {counterErr ? (
                <Text style={{ color: theme.colors.danger, marginVertical: theme.spacing.s }}>{counterErr}</Text>
              ) : null}

              <View style={{ flexDirection: 'row', gap: theme.spacing.s, marginTop: theme.spacing.m }}>
                <View style={{ flex: 1 }}>
                  <Button title="Chiudi" variant="subtle" onPress={() => setCounterSwap(null)} />
                </View>
                <View style={{ flex: 1 }}>
                  <Button title="Proponi" icon="send"
                    loading={counterBusy}
                    disabled={!counterPicked}
                    onPress={submitCounter} />
                </View>
              </View>
            </>
          ) : null}
        </ScrollView>
      </Sheet>

      {/* Sheet warning soglia di tutela (riposo / lavoro continuativo) */}
      <Sheet visible={warnOpen} onClose={closeWarning} title="">
        <View style={{ alignItems: 'center', marginBottom: theme.spacing.m }}>
          <View style={{
            width: 64, height: 64, borderRadius: 32,
            backgroundColor: '#FEF3C7', alignItems: 'center', justifyContent: 'center',
            marginBottom: theme.spacing.s,
          }}>
            <Icon name="moon-outline" size={34} color="#B45309" />
          </View>
          <Text style={[theme.typography.h2, { textAlign: 'center' }]}>Soglia di tutela superata</Text>
          <Text style={[theme.typography.caption, { textAlign: 'center', marginTop: 4 }]}>
            Stai per andare oltre i limiti consigliati di riposo / lavoro continuativo.
          </Text>
        </View>

        <View style={{
          backgroundColor: '#FFFBEB', borderColor: '#FCD34D', borderWidth: 1,
          borderRadius: theme.radius.m, padding: theme.spacing.m, marginBottom: theme.spacing.m,
        }}>
          {warnViolations.map((v, i) => (
            <View key={i} style={{ flexDirection: 'row', alignItems: 'flex-start', gap: 8, marginBottom: i < warnViolations.length - 1 ? 6 : 0 }}>
              <Icon name="alert-circle-outline" size={18} color="#B45309" />
              <Text style={{ flex: 1, color: '#78350F', lineHeight: 20 }}>{v.message}</Text>
            </View>
          ))}
        </View>

        <View style={{
          backgroundColor: theme.colors.surfaceAlt, borderRadius: theme.radius.m,
          padding: theme.spacing.m, marginBottom: theme.spacing.m,
        }}>
          <Text style={[theme.typography.caption, { lineHeight: 18 }]}>
            <Text style={{ fontWeight: '700' }}>Nota:</Text>{' '}
            la reperibilità è uno standby per le urgenze, non lavoro continuativo.
            Valuta se ti senti di prenderti questa responsabilità.
          </Text>
        </View>

        <View style={{ flexDirection: 'row', gap: theme.spacing.s }}>
          <View style={{ flex: 1 }}>
            <Button title="Annulla" variant="subtle" icon="close" onPress={closeWarning} />
          </View>
          <View style={{ flex: 1 }}>
            <Button title="Accetto comunque" variant="danger" icon="checkmark"
              onPress={confirmWarning} />
          </View>
        </View>
      </Sheet>
    </SafeAreaView>
  );
}
