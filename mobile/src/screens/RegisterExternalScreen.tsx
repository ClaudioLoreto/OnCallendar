import React, { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native';
import { Button, Card, ConfirmModal, Field, PasswordField } from '../components/ui';
import { AuthApi } from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';
import { PasswordRules, isPasswordStrong } from './ResetPasswordScreen';

type Props = {
  token: string;
  onDone: () => void;
};

export default function RegisterExternalScreen({ token, onDone }: Props) {
  const { theme } = useTheme();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<{ firstName: string; lastName: string; email: string | null } | null>(null);

  const [email, setEmail] = useState('');
  const [pwd, setPwd] = useState('');
  const [pwd2, setPwd2] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [popup, setPopup] = useState<{ title: string; message?: string; tone?: 'default' | 'success' | 'warning' | 'danger'; icon?: any; onConfirm?: () => void } | null>(null);

  const strong = useMemo(() => isPasswordStrong(pwd), [pwd]);
  const match = pwd.length > 0 && pwd === pwd2;

  useEffect(() => {
    (async () => {
      try {
        const data = await AuthApi.getExternalInvite(token);
        setInfo(data);
        if (data.email) setEmail(data.email);
      } catch (e: any) {
        setError(e?.response?.data?.error ?? 'Invito non valido o scaduto.');
      } finally {
        setLoading(false);
      }
    })();
  }, [token]);

  const submit = async () => {
    if (!email.trim()) { setPopup({ title: 'Email obbligatoria', tone: 'warning', icon: 'alert-circle-outline' }); return; }
    if (!strong) { setPopup({ title: 'Password troppo debole', message: 'Verifica i requisiti elencati.', tone: 'warning', icon: 'shield-half-outline' }); return; }
    if (!match) { setPopup({ title: 'Le password non coincidono', tone: 'warning', icon: 'alert-circle-outline' }); return; }
    setSubmitting(true);
    try {
      await AuthApi.registerExternal(token, email.trim(), pwd);
      setPopup({
        title: 'Registrazione completata',
        message: 'Ora puoi effettuare il login con la tua email e la password scelta.',
        tone: 'success', icon: 'checkmark-circle-outline',
        onConfirm: onDone,
      });
    } catch (e: any) {
      setPopup({ title: 'Errore', message: e?.response?.data?.error ?? 'Registrazione non riuscita.', tone: 'danger', icon: 'alert-circle-outline' });
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: theme.colors.background }}>
        <ActivityIndicator color={theme.colors.primary} />
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      style={{ flex: 1, backgroundColor: theme.colors.background }}
    >
      <ScrollView contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: theme.spacing.l }}>
        <View style={{ alignItems: 'center', marginBottom: theme.spacing.l }}>
          <Text style={[theme.typography.h1, { textAlign: 'center' }]}>Registrazione</Text>
          {info ? (
            <Text style={[theme.typography.caption, { textAlign: 'center', marginTop: 4 }]}>
              {info.firstName} {info.lastName}
            </Text>
          ) : null}
        </View>
        <Card>
          {error ? (
            <Text style={{ color: theme.colors.danger, marginBottom: theme.spacing.m }}>{error}</Text>
          ) : (
            <>
              <Field
                label="Email"
                value={email}
                onChangeText={setEmail}
                keyboardType="email-address"
                autoCapitalize="none"
                autoCorrect={false}
              />
              <PasswordField label="Password" value={pwd} onChangeText={setPwd} />
              <PasswordRules value={pwd} />
              <PasswordField label="Conferma password" value={pwd2} onChangeText={setPwd2} />
              <View style={{ height: theme.spacing.s }} />
              <Button title="Crea account" icon="checkmark-circle-outline" onPress={submit} loading={submitting} disabled={!strong || !match} />
            </>
          )}
          <View style={{ height: theme.spacing.s }} />
          <Button title="Annulla" variant="subtle" onPress={onDone} />
        </Card>
      </ScrollView>

      <ConfirmModal
        visible={!!popup}
        title={popup?.title ?? ''}
        message={popup?.message}
        tone={popup?.tone}
        icon={popup?.icon}
        onConfirm={popup?.onConfirm ? () => { const fn = popup.onConfirm; setPopup(null); fn?.(); } : undefined}
        confirmLabel={popup?.onConfirm ? 'Vai al login' : 'OK'}
        onClose={() => setPopup(null)}
      />
    </KeyboardAvoidingView>
  );
}
