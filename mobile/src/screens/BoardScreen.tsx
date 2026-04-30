import React, { useCallback, useEffect, useState } from 'react';
import { FlatList, RefreshControl, SafeAreaView, Text, View } from 'react-native';
import { Avatar, Badge, Button, Card, EmptyState, Icon } from '../components/ui';
import AppHeader from '../components/AppHeader';
import {
  BoardApi, ShiftDto, SwapsApi, formatDayLong, shiftCodeIcon, shiftCodeShort, shiftCodeTone,
} from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';

type Props = { navigation: { navigate: (route: string) => void } };

export default function BoardScreen({ navigation }: Props) {
  const { theme } = useTheme();
  const { t, locale } = useI18n();

  const [items, setItems] = useState<ShiftDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [pickingId, setPickingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    const list = await BoardApi.list();
    setItems(list);
  }, []);

  useEffect(() => {
    (async () => {
      try { await load(); }
      catch (e: any) { setError(e?.response?.data?.error ?? e?.message ?? 'Errore'); }
      finally { setLoading(false); }
    })();
  }, [load]);

  const onRefresh = async () => {
    setRefreshing(true);
    try { await load(); }
    catch (e: any) { setError(e?.response?.data?.error ?? e?.message ?? 'Errore'); }
    finally { setRefreshing(false); }
  };

  const onPick = async (s: ShiftDto) => {
    setPickingId(s.id); setError(null);
    try {
      await SwapsApi.pick(s.id);
      await load();
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setPickingId(null);
    }
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <AppHeader title={t('board.title')} onAvatarPress={() => navigation.navigate('Profile')} />
      {loading ? (
        <EmptyState title={t('common.loading')} />
      ) : (
        <FlatList
          contentContainerStyle={{ padding: theme.spacing.l, paddingTop: theme.spacing.s }}
          data={items}
          keyExtractor={s => s.id}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
          ListEmptyComponent={
            <EmptyState icon="albums-outline" title={t('board.empty')}
              subtitle="Trascina giù per aggiornare." />
          }
          ListHeaderComponent={
            error ? (
              <View style={{ backgroundColor: '#FBE3E1', padding: theme.spacing.m, borderRadius: theme.radius.m, marginBottom: theme.spacing.m }}>
                <Text style={{ color: theme.colors.danger, fontWeight: '600' }}>{error}</Text>
              </View>
            ) : null
          }
          renderItem={({ item: s }) => (
            <Card>
              <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: theme.spacing.s }}>
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                  <Icon name={shiftCodeIcon(s.code) as any} size={22} color={theme.colors.primary} />
                  <Badge label={s.code} tone={shiftCodeTone(s.code) as any} />
                  <Text style={[theme.typography.h3, { textTransform: 'capitalize' }]}>
                    {formatDayLong(s.date, locale === 'it' ? 'it-IT' : 'en-GB')}
                  </Text>
                </View>
                <Badge label="In bacheca" tone="warning" />
              </View>
              <Text style={theme.typography.caption}>
                {shiftCodeShort(s.code)} · {s.startLocal} – {s.endLocal}
              </Text>
              {s.medicoTurno ? (
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 6 }}>
                  <Avatar fullName={s.medicoTurno.fullName} url={s.medicoTurno.avatarUrl} size={24} />
                  <Text style={theme.typography.body}>
                    Offerto da <Text style={{ fontWeight: '700' }}>{s.medicoTurno.fullName}</Text>
                  </Text>
                </View>
              ) : null}
              {s.medicoReperibile ? (
                <Text style={[theme.typography.caption, { marginTop: 4 }]}>
                  Reperibile: {s.medicoReperibile.fullName}
                </Text>
              ) : null}
              {!s.isMineTurno ? (
                <View style={{ marginTop: theme.spacing.m }}>
                  <Button
                    title="Prendo io"
                    icon="hand-right-outline"
                    loading={pickingId === s.id}
                    onPress={() => onPick(s)}
                  />
                </View>
              ) : (
                <View style={{ marginTop: theme.spacing.m }}>
                  <Badge label="È un tuo turno (puoi ritirarlo dal Calendario)" tone="info" />
                </View>
              )}
            </Card>
          )}
        />
      )}
    </SafeAreaView>
  );
}
