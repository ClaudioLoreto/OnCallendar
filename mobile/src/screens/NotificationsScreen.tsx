import React, { useEffect } from 'react';
import { FlatList, RefreshControl, SafeAreaView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { EmptyState } from '../components/ui';
import { useTheme } from '../theme/ThemeContext';
import { useNotifications } from '../auth/NotificationsContext';

type Props = { navigation: any };

const typeConfig = (type: string): { icon: string; color: string; label: string } => {
  switch (type) {
    case 'SwapIncoming':   return { icon: 'swap-horizontal-outline', color: '#2563eb', label: 'Nuova richiesta' };
    case 'SwapAccepted':   return { icon: 'checkmark-circle-outline', color: '#16a34a', label: 'Richiesta accettata' };
    case 'SwapRejected':   return { icon: 'close-circle-outline',     color: '#dc2626', label: 'Richiesta rifiutata' };
    case 'SwapCancelled':  return { icon: 'ban-outline',              color: '#6b7280', label: 'Richiesta annullata' };
    case 'SwapAutoCancel': return { icon: 'time-outline',             color: '#d97706', label: 'Turno già preso' };
    case 'SwapCounter':         return { icon: 'repeat-outline',          color: '#7c3aed', label: 'Controproposta ricevuta' };
    case 'SwapCounterRejected': return { icon: 'close-circle-outline',    color: '#dc2626', label: 'Controproposta rifiutata' };
    default:               return { icon: 'notifications-outline',    color: '#6b7280', label: type };
  }
};

const timeAgo = (iso: string): string => {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60_000);
  if (mins < 1) return 'Ora';
  if (mins < 60) return `${mins} min fa`;
  const h = Math.floor(mins / 60);
  if (h < 24) return `${h} ore fa`;
  const d = Math.floor(h / 24);
  return `${d} giorni fa`;
};

export default function NotificationsScreen({ navigation }: Props) {
  const { theme } = useTheme();
  const { notifications, loading, refresh, markAllRead } = useNotifications();

  useEffect(() => {
    refresh();
    // Segna tutto come letto dopo 1,5 secondi (l'utente ha "visto" le notifiche)
    const t = setTimeout(() => { markAllRead(); }, 1500);
    return () => clearTimeout(t);
  }, []);

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <FlatList
        contentContainerStyle={{ padding: theme.spacing.l, paddingBottom: 80 }}
        data={notifications}
        keyExtractor={n => n.id}
        refreshControl={<RefreshControl refreshing={loading} onRefresh={refresh} />}
        ListEmptyComponent={!loading ? (
          <EmptyState icon="notifications-outline" title="Nessuna notifica" />
        ) : null}
        renderItem={({ item }) => {
          const cfg = typeConfig(item.type);
          return (
            <View style={{
              flexDirection: 'row', alignItems: 'flex-start', gap: 12,
              backgroundColor: item.isRead ? theme.colors.surface : theme.colors.accent,
              borderRadius: theme.radius.l,
              padding: theme.spacing.m,
              marginBottom: theme.spacing.s,
              ...theme.shadows.card,
            }}>
              <View style={{
                width: 38, height: 38, borderRadius: 19,
                backgroundColor: cfg.color + '18',
                alignItems: 'center', justifyContent: 'center',
              }}>
                <Ionicons name={cfg.icon as any} size={20} color={cfg.color} />
              </View>
              <View style={{ flex: 1 }}>
                <Text style={[theme.typography.caption, { color: cfg.color, fontWeight: '700', marginBottom: 2 }]}>
                  {cfg.label}
                </Text>
                <Text style={[theme.typography.body, { lineHeight: 20 }]}>{item.message}</Text>
                <Text style={[theme.typography.caption, { marginTop: 4 }]}>{timeAgo(item.createdAtUtc)}</Text>
              </View>
              {!item.isRead ? (
                <View style={{
                  width: 8, height: 8, borderRadius: 4,
                  backgroundColor: theme.colors.primary, marginTop: 5,
                }} />
              ) : null}
            </View>
          );
        }}
      />
    </SafeAreaView>
  );
}
