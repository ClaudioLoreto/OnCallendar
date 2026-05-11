import React, { useMemo, useState } from 'react';
import { SafeAreaView, ScrollView, Text, View } from 'react-native';
import { Button, Card, ConfirmModal, Icon, PasswordField } from '../components/ui';
import { useTheme } from '../theme/ThemeContext';
import { useAuth } from '../auth/AuthContext';
import { UsersApi } from '../api/endpoints';
import { PasswordRules, isPasswordStrong } from './ResetPasswordScreen';

/**
 * Schermata bloccante mostrata quando la password dell'utente \u00e8 scaduta
 * (passati pi\u00f9 di 12 mesi dall'ultimo cambio). L'utente non pu\u00f2 navigare
 * in nessun'altra parte dell'app finch\u00e9 non imposta una nuova password
 * o non esegue il logout.
 */
type Props = { onPasswordChanged: () => void };

export default function PasswordExpiredScreen({ onPasswordChanged }: Props) {
  const { theme } = useTheme();
  const { user, logout } = useAuth();

  const [oldPwd, setOldPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [newPwd2, setNewPwd2] = useState('');
  const [busy, setBusy] = useState(false);
  const [popup, setPopup] = useState<{ title: string; message?: string; tone?: any; icon?: any } | null>(null);

  const strong = useMemo(() => isPasswordStrong(newPwd), [newPwd]);
  const canSubmit = oldPwd.length > 0 && strong && newPwd === newPwd2;

  const submit = async () => {
    if (!canSubmit) return;
    setBusy(true);
    try {
      await UsersApi.changePassword(oldPwd, newPwd);
      setPopup({ title: 'Password aggiornata', message: 'Ora puoi continuare a usare l\'app normalmente.', tone: 'success', icon: 'shield-checkmark-outline' });
      setTimeout(() => { setPopup(null); onPasswordChanged(); }, 1500);
    } catch (e: any) {
      const msg = e?.response?.data?.errors?.join(' ') ?? e?.response?.data?.error ?? 'Cambio password non riuscito.';
      setPopup({ title: 'Errore', message: msg, tone: 'danger', icon: 'alert-circle-outline' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <ScrollView contentContainerStyle={{ padding: theme.spacing.l, paddingTop: theme.spacing.xl }}>
        <View style={{ alignItems: 'center', marginBottom: theme.spacing.l }}>
          <View style={{
            width: 72, height: 72, borderRadius: 36,
            backgroundColor: theme.colors.warning + '22',
            alignItems: 'center', justifyContent: 'center',
            marginBottom: theme.spacing.m,
          }}>
            <Icon name="time-outline" size={36} color={theme.colors.warning} />
          </View>
          <Text style={[theme.typography.h2, { textAlign: 'center' }]}>Password scaduta</Text>
          <Text style={[theme.typography.caption, { textAlign: 'center', marginTop: 6 }]}>
            Per la sicurezza dell'account devi cambiare la password prima di continuare.
          </Text>
        </View>

        <Card>
          <Text style={[theme.typography.body, { marginBottom: theme.spacing.s }]}>
            Ciao {user?.fullName ?? ''}, sono passati pi\u00f9 di 12 mesi dall'ultimo cambio password.
            Imposta una nuova password rispettando i requisiti.
          </Text>
          <PasswordField label="Password attuale" value={oldPwd} onChangeText={setOldPwd} />
          <PasswordField label="Nuova password" value={newPwd} onChangeText={setNewPwd} />
          {newPwd.length > 0 ? <PasswordRules value={newPwd} /> : null}
          <PasswordField label="Conferma nuova password" value={newPwd2} onChangeText={setNewPwd2} />
          {newPwd2.length > 0 && newPwd !== newPwd2 ? (
            <Text style={{ color: theme.colors.danger, marginTop: -theme.spacing.s, marginBottom: theme.spacing.s }}>
              Le due password non coincidono.
            </Text>
          ) : null}
          <Button
            title="Aggiorna password"
            icon="shield-checkmark-outline"
            onPress={submit}
            loading={busy}
            disabled={!canSubmit}
          />
          <View style={{ height: theme.spacing.s }} />
          <Button title="Esci" variant="subtle" icon="log-out-outline" onPress={logout} />
        </Card>
      </ScrollView>

      <ConfirmModal
        visible={!!popup}
        title={popup?.title ?? ''}
        message={popup?.message}
        tone={popup?.tone}
        icon={popup?.icon}
        onClose={() => setPopup(null)}
      />
    </SafeAreaView>
  );
}
