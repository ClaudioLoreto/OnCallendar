import React, { useState } from 'react';
import { Alert, KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native';
import { useAuth } from '../auth/AuthContext';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';
import { Button, Card, Field, Sheet } from '../components/ui';
import { AuthApi } from '../api/endpoints';

const LoginScreen: React.FC = () => {
  const { theme } = useTheme();
  const { t } = useI18n();
  const { login } = useAuth();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const [forgotOpen, setForgotOpen] = useState(false);
  const [forgotTarget, setForgotTarget] = useState('');
  const [forgotSending, setForgotSending] = useState(false);

  const submit = async () => {
    if (!email.trim() || !password) return;
    setLoading(true);
    try {
      await login(email.trim(), password);
    } catch (e: any) {
      Alert.alert(t('common.error'), e?.response?.data?.message ?? 'Credenziali non valide');
    } finally {
      setLoading(false);
    }
  };

  const sendForgot = async () => {
    if (!forgotTarget.trim()) return;
    setForgotSending(true);
    try {
      await AuthApi.forgotPassword(forgotTarget.trim());
      Alert.alert('OK', 'Se l\'account esiste, riceverai le istruzioni.');
      setForgotOpen(false);
      setForgotTarget('');
    } catch (e: any) {
      // Lo stub backend ritorna 501: lo trattiamo come "non ancora attivo"
      Alert.alert(t('common.notImplemented'), 'Il recupero password sarà presto disponibile.');
      setForgotOpen(false);
    } finally {
      setForgotSending(false);
    }
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      style={{ flex: 1, backgroundColor: theme.colors.background }}
    >
      <ScrollView contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: theme.spacing.l }}>
        <View style={{ alignItems: 'center', marginBottom: theme.spacing.xl }}>
          <Text style={[theme.typography.h1, { textAlign: 'center' }]}>OnCallendar</Text>
          <Text style={[theme.typography.caption, { textAlign: 'center', marginTop: 4 }]}>
            {t('login.title')}
          </Text>
        </View>

        <Card>
          <Field
            label={t('login.email')}
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            keyboardType="email-address"
            autoComplete="email"
          />
          <Field
            label={t('login.password')}
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoComplete="password"
          />
          <View style={{ height: theme.spacing.s }} />
          <Button title={t('login.submit')} icon="log-in-outline" onPress={submit} loading={loading} />
          <View style={{ height: theme.spacing.s }} />
          <Button title={t('login.forgot')} variant="ghost" icon="key-outline" onPress={() => setForgotOpen(true)} />
        </Card>

        <Text style={[theme.typography.caption, { textAlign: 'center', marginTop: theme.spacing.m }]}>
          {t('login.noRegister')}
        </Text>
      </ScrollView>

      <Sheet visible={forgotOpen} onClose={() => setForgotOpen(false)} title={t('login.forgot')}>
        <Text style={[theme.typography.body, { marginBottom: theme.spacing.m }]}>
          Inserisci la tua email o numero di telefono. Ti invieremo un OTP per reimpostare la password.
        </Text>
        <Field
          label="Email o telefono"
          value={forgotTarget}
          onChangeText={setForgotTarget}
          autoCapitalize="none"
        />
        <Button title="Invia OTP" icon="send-outline" onPress={sendForgot} loading={forgotSending} />
        <View style={{ height: theme.spacing.s }} />
        <Button title={t('common.cancel')} variant="subtle" onPress={() => setForgotOpen(false)} />
      </Sheet>
    </KeyboardAvoidingView>
  );
};

export default LoginScreen;
