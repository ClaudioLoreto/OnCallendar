import React, { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { NavigationContainer, DefaultTheme, DarkTheme } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Ionicons } from '@expo/vector-icons';

import { useAuth } from '../auth/AuthContext';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';
import { UsersApi } from '../api/endpoints';

import LoginScreen from '../screens/LoginScreen';
import CalendarScreen from '../screens/CalendarScreen';
import SwapsScreen from '../screens/SwapsScreen';
import ProfileScreen from '../screens/ProfileScreen';
import HistoryScreen from '../screens/HistoryScreen';
import NotificationsScreen from '../screens/NotificationsScreen';
import NotificationPreferencesScreen from '../screens/NotificationPreferencesScreen';
import PasswordExpiredScreen from '../screens/PasswordExpiredScreen';

const Tabs = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

const tabIcon = (name: keyof typeof Ionicons.glyphMap, focusedName?: keyof typeof Ionicons.glyphMap) =>
  ({ color, focused, size }: { color: string; focused: boolean; size: number }) => (
    <Ionicons name={focused ? (focusedName ?? name) : name} size={size} color={color} />
  );

const MainTabs: React.FC = () => {
  const { theme } = useTheme();
  const { t } = useI18n();
  return (
    <Tabs.Navigator
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: theme.colors.primary,
        tabBarInactiveTintColor: theme.colors.textMuted,
        tabBarStyle: {
          backgroundColor: theme.colors.surface,
          borderTopColor: theme.colors.border,
        },
        tabBarLabelStyle: { fontWeight: '600' },
      }}
    >
      <Tabs.Screen name="Calendar" component={CalendarScreen} options={{ title: t('tabs.calendar'), tabBarIcon: tabIcon('calendar-outline', 'calendar') }} />
      <Tabs.Screen name="Swaps"    component={SwapsScreen}    options={{ title: t('tabs.swaps'),    tabBarIcon: tabIcon('swap-horizontal-outline', 'swap-horizontal') }} />
    </Tabs.Navigator>
  );
};

const RootNavigator: React.FC = () => {
  const { user, loading } = useAuth();
  const { theme, scheme } = useTheme();
  const { t } = useI18n();

  // Carica i flag account (passwordExpired, ecc.) ogni volta che cambia user.
  // Se passwordExpired è true mostriamo solo la schermata bloccante.
  const [passwordExpired, setPasswordExpired] = useState<boolean | null>(null);
  const refreshFlags = useCallback(async () => {
    if (!user) { setPasswordExpired(null); return; }
    try {
      const me = await UsersApi.me();
      setPasswordExpired(!!me.passwordExpired);
    } catch {
      setPasswordExpired(false); // se non riusciamo a leggerlo, non blocchiamo
    }
  }, [user]);
  useEffect(() => { refreshFlags(); }, [refreshFlags]);

  if (loading || (user && passwordExpired === null)) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: theme.colors.background }}>
        <ActivityIndicator color={theme.colors.primary} size="large" />
      </View>
    );
  }

  const navTheme = {
    ...(scheme === 'dark' ? DarkTheme : DefaultTheme),
    colors: {
      ...(scheme === 'dark' ? DarkTheme.colors : DefaultTheme.colors),
      background: theme.colors.background,
      card: theme.colors.surface,
      text: theme.colors.textPrimary,
      border: theme.colors.border,
      primary: theme.colors.primary,
    },
  };

  return (
    <NavigationContainer theme={navTheme}>
      {user ? (
        passwordExpired ? (
          <Stack.Navigator screenOptions={{ headerShown: false }}>
            <Stack.Screen name="PasswordExpired">
              {() => <PasswordExpiredScreen onPasswordChanged={refreshFlags} />}
            </Stack.Screen>
          </Stack.Navigator>
        ) : (
        <Stack.Navigator
          screenOptions={{
            headerStyle: { backgroundColor: theme.colors.surface },
            headerTintColor: theme.colors.textPrimary,
            headerTitleStyle: { fontWeight: '700' },
            headerBackTitle: '',
            headerBackTitleVisible: false,
          } as any}
        >
          <Stack.Screen name="Main"          component={MainTabs}           options={{ headerShown: false }} />
          <Stack.Screen name="Profile"       component={ProfileScreen}      options={{ title: t('profile.title') }} />
          <Stack.Screen name="History"       component={HistoryScreen}      options={{ title: t('profile.section.history') }} />
          <Stack.Screen name="Notifications" component={NotificationsScreen} options={{ title: 'Notifiche' }} />
          <Stack.Screen name="NotificationPreferences" component={NotificationPreferencesScreen} options={{ title: t('notifPrefs.title') }} />
        </Stack.Navigator>
        )
      ) : (
        <Stack.Navigator screenOptions={{ headerShown: false }}>
          <Stack.Screen name="Login" component={LoginScreen} />
        </Stack.Navigator>
      )}
    </NavigationContainer>
  );
};

export default RootNavigator;
