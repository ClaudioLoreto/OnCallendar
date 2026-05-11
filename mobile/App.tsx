import 'react-native-gesture-handler';
import React from 'react';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { AuthProvider } from './src/auth/AuthContext';
import { NotificationsProvider } from './src/auth/NotificationsContext';
import { ThemeProvider, useTheme } from './src/theme/ThemeContext';
import { I18nProvider } from './src/i18n/I18nContext';
import RootNavigator from './src/navigation/RootNavigator';
import { DeepLinkRouter } from './src/navigation/DeepLinkRouter';

const ThemedStatusBar: React.FC = () => {
  const { scheme } = useTheme();
  return <StatusBar style={scheme === 'dark' ? 'light' : 'dark'} />;
};

export default function App() {
  return (
    <SafeAreaProvider>
      <ThemeProvider>
        <I18nProvider>
          <AuthProvider>
            <NotificationsProvider>
              <ThemedStatusBar />
              <DeepLinkRouter>
                <RootNavigator />
              </DeepLinkRouter>
            </NotificationsProvider>
          </AuthProvider>
        </I18nProvider>
      </ThemeProvider>
    </SafeAreaProvider>
  );
}
