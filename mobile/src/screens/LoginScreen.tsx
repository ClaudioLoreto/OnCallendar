import React, { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native';
import { useAuth } from '../auth/AuthContext';
import { useTheme } from '../theme/ThemeContext';
import { useI18n } from '../i18n/I18nContext';
import { Button, Card, ConfirmModal, Field, PasswordField, Sheet } from '../components/ui';
import { AuthApi } from '../api/endpoints';
import { buildCallbackUrl } from '../api/callbackUrl';

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

  // Modal custom (sostituisce Alert nativo)
  const [info, setInfo] = useState<{
    title: string; message?: string; tone?: 'default' | 'success' | 'warning' | 'danger'; icon?: any;
  } | null>(null);

  const submit = async () => {
    if (!email.trim() || !password) return;
    setLoading(true);
    try {
      await login(email.trim(), password);
    } catch (e: any) {
      setInfo({
        title: t('common.error'),
        message: e?.response?.data?.error ?? e?.response?.data?.message ?? 'Credenziali non valide.',
        tone: 'danger',
        icon: 'alert-circle-outline',
      });
    } finally {
      setLoading(false);
    }
  };

  const sendForgot = async () => {
    const target = forgotTarget.trim();
    if (!target) return;
    setForgotSending(true);
    try {
      await AuthApi.forgotPassword(target, buildCallbackUrl('reset-password'));
      setForgotOpen(false);
      setForgotTarget('');
      setInfo({
        title: 'Email inviata',
        message: `Abbiamo inviato a ${target} il link per reimpostare la password.`,
        tone: 'success',
        icon: 'mail-outline',
      });
    } catch (e: any) {
      const status = e?.response?.status;
      const msg = status === 404
        ? 'Nessun account associato a questa email. Controlla l\'indirizzo o contatta il SuperAdmin.'
        : e?.response?.data?.error ?? 'Impossibile inviare l\'email in questo momento.';
      setInfo({ title: 'Email non inviata', message: msg, tone: 'danger', icon: 'alert-circle-outline' });
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
            placeholder={t('login.emailPlaceholder')}
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            autoCorrect={false}
            keyboardType="default"
            autoComplete="off"
            textContentType="none"
          />
          <PasswordField
            label={t('login.password')}
            value={password}
            onChangeText={setPassword}
            autoComplete="off"
            textContentType="none"
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
          Inserisci la tua email. Ti invieremo un link per reimpostare la password.
        </Text>
        <Field
          label="Email"
          value={forgotTarget}
          onChangeText={setForgotTarget}
          autoCapitalize="none"
          keyboardType="email-address"
          autoComplete="email"
        />
        <Button title="Invia" icon="send-outline" onPress={sendForgot} loading={forgotSending} />
        <View style={{ height: theme.spacing.s }} />
        <Button title={t('common.cancel')} variant="subtle" onPress={() => setForgotOpen(false)} />
      </Sheet>

      <ConfirmModal
        visible={!!info}
        title={info?.title ?? ''}
        message={info?.message}
        tone={info?.tone}
        icon={info?.icon}
        onClose={() => setInfo(null)}
      />
    </KeyboardAvoidingView>
  );
};

export default LoginScreen;
