import React, { useCallback, useEffect, useState } from 'react';
import { RefreshControl, SafeAreaView, ScrollView, Text, TouchableOpacity, View } from 'react-native';
import { LineChart } from 'react-native-chart-kit';
import { Card, EmptyState, Icon } from '../components/ui';
import AppHeader from '../components/AppHeader';
import { DashboardApi, DashboardStats, MonthlyHoursData } from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';

type Props = { navigation: { navigate: (route: string, params?: any) => void } };

export default function DashboardScreen({ navigation }: Props) {
  const { theme } = useTheme();
  const { locale } = useI18n();

  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [hoursData, setHoursData] = useState<MonthlyHoursData | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [monthOffset, setMonthOffset] = useState(0); // 0 = mese corrente

  // ---- Mese derivato dall'offset ----
  const filterMonth = React.useMemo(() => {
    const d = new Date();
    d.setDate(1);
    d.setHours(0, 0, 0, 0);
    d.setMonth(d.getMonth() + monthOffset);
    return d;
  }, [monthOffset]);

  const monthLabel = filterMonth.toLocaleDateString(locale === 'it' ? 'it-IT' : 'en-GB', {
    month: 'long',
    year: 'numeric',
  });

  const load = useCallback(async () => {
    const year = filterMonth.getFullYear();
    const month = filterMonth.getMonth() + 1; // 1-based
    const [s, h] = await Promise.all([DashboardApi.stats(year, month), DashboardApi.monthlyHours(year, month)]);
    setStats(s);
    setHoursData(h);
  }, [filterMonth]);

  useEffect(() => {
    (async () => {
      try {
        await load();
      } catch (err) {
        console.error('Dashboard load error:', err);
      } finally {
        setLoading(false);
      }
    })();
  }, [load]);

  const onRefresh = async () => {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  };

  // ---- Picker mese ----
  const MonthPicker = () => (
    <View
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: theme.spacing.m,
        backgroundColor: theme.colors.surface,
        borderRadius: theme.radius.m,
        marginHorizontal: theme.spacing.l,
        marginBottom: theme.spacing.m,
      }}
    >
      <TouchableOpacity
        onPress={() => setMonthOffset(o => o - 1)}
        hitSlop={12}
        disabled={filterMonth.getFullYear() === 2026 && filterMonth.getMonth() === 0}
        style={{ opacity: filterMonth.getFullYear() === 2026 && filterMonth.getMonth() === 0 ? 0.4 : 1 }}
      >
        <Icon name="chevron-back-outline" size={24} color={theme.colors.primary} />
      </TouchableOpacity>
      <Text style={{ fontSize: 16, fontWeight: '600', color: theme.colors.text }}>{monthLabel}</Text>
      {monthOffset < 0 ? (
        <TouchableOpacity onPress={() => setMonthOffset(o => o + 1)} hitSlop={12}>
          <Icon name="chevron-forward-outline" size={24} color={theme.colors.primary} />
        </TouchableOpacity>
      ) : (
        <View style={{ width: 24 }} />
      )}
    </View>
  );

  // ---- Quick Stats Cards ----
  const renderQuickStats = () => {
    if (!stats) return null;
    return (
      <View style={{ paddingHorizontal: theme.spacing.l, gap: theme.spacing.m }}>
        {/* Row 1: Turni + Ore */}
        <View style={{ flexDirection: 'row', gap: theme.spacing.m }}>
          <Card style={{ flex: 1, padding: theme.spacing.m }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.spacing.s }}>
              <Icon name="calendar-outline" size={20} color={theme.colors.primary} />
              <Text style={{ fontSize: 13, color: theme.colors.textSecondary }}>Turni</Text>
            </View>
            <Text style={{ fontSize: 28, fontWeight: 'bold', color: theme.colors.text, marginTop: theme.spacing.xs }}>
              {stats.totalShifts}
            </Text>
          </Card>
          <Card style={{ flex: 1, padding: theme.spacing.m }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.spacing.s }}>
              <Icon name="time-outline" size={20} color={theme.colors.success} />
              <Text style={{ fontSize: 13, color: theme.colors.textSecondary }}>Ore</Text>
            </View>
            <Text style={{ fontSize: 28, fontWeight: 'bold', color: theme.colors.text, marginTop: theme.spacing.xs }}>
              {stats.hoursWorked}h
            </Text>
          </Card>
        </View>

        {/* Row 2: Stipendio + Modifiche */}
        <View style={{ flexDirection: 'row', gap: theme.spacing.m }}>
          <Card style={{ flex: 1, padding: theme.spacing.m }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.spacing.s }}>
              <Icon name="cash-outline" size={20} color={theme.colors.warning} />
              <Text style={{ fontSize: 13, color: theme.colors.textSecondary }}>Stima €</Text>
            </View>
            <Text style={{ fontSize: 28, fontWeight: 'bold', color: theme.colors.text, marginTop: theme.spacing.xs }}>
              €{stats.estimatedSalary}
            </Text>
          </Card>
          <Card style={{ flex: 1, padding: theme.spacing.m }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.spacing.s }}>
              <Icon name="swap-horizontal-outline" size={20} color={theme.colors.info} />
              <Text style={{ fontSize: 13, color: theme.colors.textSecondary }}>Scambi</Text>
            </View>
            <Text style={{ fontSize: 28, fontWeight: 'bold', color: theme.colors.text, marginTop: theme.spacing.xs }}>
              {stats.recentChanges}
            </Text>
          </Card>
        </View>

        {/* Reperibilità */}
        {stats.onCallShifts > 0 && (
          <Card style={{ padding: theme.spacing.m }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: theme.spacing.s }}>
              <Icon name="notifications-outline" size={20} color={theme.colors.danger} />
              <Text style={{ fontSize: 13, color: theme.colors.textSecondary }}>Reperibilità</Text>
            </View>
            <Text style={{ fontSize: 24, fontWeight: 'bold', color: theme.colors.text, marginTop: theme.spacing.xs }}>
              {stats.onCallShifts} turni
            </Text>
          </Card>
        )}
      </View>
    );
  };

  // ---- Grafico ore giornaliere ----
  const renderChart = () => {
    if (!hoursData || hoursData.hoursByDay.length === 0) return null;

    // Prendi solo i giorni con almeno un'ora lavorata (o mostra comunque tutto)
    const labels = Array.from({ length: hoursData.daysInMonth }, (_, i) => (i + 1).toString());
    const data = hoursData.hoursByDay;

    return (
      <Card style={{ margin: theme.spacing.l, padding: theme.spacing.m }}>
        <Text style={{ fontSize: 16, fontWeight: '600', color: theme.colors.text, marginBottom: theme.spacing.m }}>
          Ore lavorate per giorno
        </Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false}>
          <LineChart
            data={{
              labels: labels.filter((_, i) => i % 3 === 0), // Mostra solo ogni 3 giorni per evitare affollamento
              datasets: [{ data }],
            }}
            width={Math.max(400, hoursData.daysInMonth * 20)} // Larghezza dinamica
            height={220}
            chartConfig={{
              backgroundColor: theme.colors.surface,
              backgroundGradientFrom: theme.colors.background,
              backgroundGradientTo: theme.colors.background,
              decimalPlaces: 1,
              color: (opacity = 1) => `rgba(53, 88, 114, ${opacity})`, // Primary color
              labelColor: (opacity = 1) => `rgba(0, 0, 0, ${opacity})`,
              style: { borderRadius: theme.radius.m },
              propsForDots: {
                r: '4',
                strokeWidth: '2',
                stroke: theme.colors.primary,
              },
            }}
            bezier
            style={{ borderRadius: theme.radius.m }}
          />
        </ScrollView>
      </Card>
    );
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <AppHeader title="Dashboard" onAvatarPress={() => navigation.navigate('Profile')} onBellPress={() => navigation.navigate('Notifications')} />
      <ScrollView refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}>
        <MonthPicker />
        {loading ? (
          <EmptyState title="Caricamento…" />
        ) : (
          <>
            {renderQuickStats()}
            {renderChart()}
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}
