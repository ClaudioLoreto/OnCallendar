import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import { NavigationContainer, DefaultTheme, DarkTheme } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Text } from 'react-native';

import { useAuth } from '../auth/AuthContext';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';

import LoginScreen from '../screens/LoginScreen';
import CalendarScreen from '../screens/CalendarScreen';
import BoardScreen from '../screens/BoardScreen';
import SwapsScreen from '../screens/SwapsScreen';
import ProfileScreen from '../screens/ProfileScreen';
import HistoryScreen from '../screens/HistoryScreen';

const Tabs = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

const tabIcon = (emoji: string) => ({ color, focused }: { color: string; focused: boolean }) => (
  <Text style={{ fontSize: focused ? 22 : 18 }}>{emoji}</Text>
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
      <Tabs.Screen name="Calendar"  component={CalendarScreen} options={{ title: t('tabs.calendar'), tabBarIcon: tabIcon('📅') }} />
      <Tabs.Screen name="Board"     component={BoardScreen}    options={{ title: t('tabs.board'),    tabBarIcon: tabIcon('📌') }} />
      <Tabs.Screen name="Swaps"     component={SwapsScreen}    options={{ title: t('tabs.swaps'),    tabBarIcon: tabIcon('🔄') }} />
    </Tabs.Navigator>
  );
};

const RootNavigator: React.FC = () => {
  const { user, loading } = useAuth();
  const { theme, scheme } = useTheme();
  const { t } = useI18n();

  if (loading) {
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
        <Stack.Navigator
          screenOptions={{
            headerStyle: { backgroundColor: theme.colors.surface },
            headerTintColor: theme.colors.textPrimary,
            headerTitleStyle: { fontWeight: '700' },
          }}
        >
          <Stack.Screen name="Main"    component={MainTabs}      options={{ headerShown: false }} />
          <Stack.Screen name="Profile" component={ProfileScreen} options={{ title: t('profile.title'), presentation: 'modal' }} />
          <Stack.Screen name="History" component={HistoryScreen} options={{ title: t('profile.section.history') }} />
        </Stack.Navigator>
      ) : (
        <Stack.Navigator screenOptions={{ headerShown: false }}>
          <Stack.Screen name="Login" component={LoginScreen} />
        </Stack.Navigator>
      )}
    </NavigationContainer>
  );
};

export default RootNavigator;
