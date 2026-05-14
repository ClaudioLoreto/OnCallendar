import React, { useMemo, useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native';
import { Button, Card, ConfirmModal, PasswordField } from '../components/ui';
import { AuthApi } from '../api/endpoints';
import { useTheme } from '../theme/ThemeContext';

type Props = {
  email: string;
  token: string;
  onDone: () => void;
};

type RuleId = 'len' | 'upper' | 'lower' | 'digit' | 'symbol';
type Rule = { id: RuleId; label: string; ok: (s: string) => boolean };
const RULES: Rule[] = [
  { id: 'len',    label: 'Almeno 8 caratteri',           ok: s => s.length >= 8 },
  { id: 'upper',  label: 'Una lettera maiuscola (A-Z)',  ok: s => /[A-Z]/.test(s) },
  { id: 'lower',  label: 'Una lettera minuscola (a-z)',  ok: s => /[a-z]/.test(s) },
  { id: 'digit',  label: 'Un numero (0-9)',              ok: s => /\d/.test(s) },
  { id: 'symbol', label: 'Un simbolo (es. !@#$%&*?)',     ok: s => /[^A-Za-z0-9]/.test(s) },
];

export function PasswordRules({ value }: { value: string }) {
  const { theme } = useTheme();
  return (
    <View style={{ backgroundColor: theme.colors.surfaceAlt, padding: 12, borderRadius: 10, marginBottom: theme.spacing.m }}>
      <Text style={[theme.typography.caption, { fontWeight: '700', marginBottom: 6 }]}>La password deve contenere:</Text>
      {RULES.map(r => {
        const ok = r.ok(value);
        return (
          <View key={r.id} style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginVertical: 2 }}>
            <Text style={{ fontSize: 14, color: ok ? theme.colors.success : theme.colors.textMuted, fontWeight: '700', width: 16 }}>
              {ok ? '✓' : '○'}
            </Text>
            <Text style={[theme.typography.caption, { color: ok ? theme.colors.success : theme.colors.textSecondary }]}>{r.label}</Text>
          </View>
        );
      })}
    </View>
  );
}

export function isPasswordStrong(pwd: string): boolean {
  return RULES.every(r => r.ok(pwd));
}

export default function ResetPasswordScreen({ email, token, onDone }: Props) {
  const { theme } = useTheme();
  const [pwd, setPwd] = useState('');
  const [pwd2, setPwd2] = useState('');
  const [loading, setLoading] = useState(false);
  const [info, setInfo] = useState<{ title: string; message?: string; tone?: 'default' | 'success' | 'warning' | 'danger'; icon?: any; onConfirm?: () => void } | null>(null);

  const strong = useMemo(() => isPasswordStrong(pwd), [pwd]);
  const match = pwd.length > 0 && pwd === pwd2;

  const submit = async () => {
    if (!strong) {
      setInfo({ title: 'Password troppo debole', message: 'Verifica i requisiti elencati.', tone: 'warning', icon: 'shield-half-outline' });
      return;
    }
    if (!match) {
      setInfo({ title: 'Le password non coincidono', tone: 'warning', icon: 'alert-circle-outline' });
      return;
    }
    setLoading(true);
    try {
      await AuthApi.resetPassword(email, token, pwd);
      setInfo({
        title: 'Password aggiornata',
        message: 'Ora puoi effettuare il login con la nuova password.',
        tone: 'success', icon: 'checkmark-circle-outline',
        onConfirm: onDone,
      });
    } catch (e: any) {
      const msg = e?.response?.data?.error ?? 'Link non valido o scaduto.';
      setInfo({ title: 'Errore', message: msg, tone: 'danger', icon: 'alert-circle-outline' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      style={{ flex: 1, backgroundColor: theme.colors.background }}
    >
      <ScrollView contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: theme.spacing.l }}>
        <View style={{ alignItems: 'center', marginBottom: theme.spacing.l }}>
          <Text style={[theme.typography.h1, { textAlign: 'center' }]}>Reimposta password</Text>
          <Text style={[theme.typography.caption, { textAlign: 'center', marginTop: 4 }]}>{email}</Text>
        </View>
        <Card>
          <PasswordField label="Nuova password" value={pwd} onChangeText={setPwd} />
          <PasswordRules value={pwd} />
          <PasswordField label="Conferma nuova password" value={pwd2} onChangeText={setPwd2} />
          {pwd2.length > 0 && pwd !== pwd2 ? (
            <Text style={{ color: theme.colors.danger, marginBottom: theme.spacing.s }}>
              Le due password non coincidono.
            </Text>
          ) : null}
          <View style={{ height: theme.spacing.s }} />
          <Button title="Conferma" icon="checkmark-circle-outline" onPress={submit} loading={loading} disabled={!strong || !match} />
          <View style={{ height: theme.spacing.s }} />
          <Button title="Annulla" variant="subtle" onPress={onDone} />
        </Card>
      </ScrollView>

      <ConfirmModal
        visible={!!info}
        title={info?.title ?? ''}
        message={info?.message}
        tone={info?.tone}
        icon={info?.icon}
        onConfirm={info?.onConfirm ? () => { const fn = info.onConfirm; setInfo(null); fn?.(); } : undefined}
        confirmLabel={info?.onConfirm ? 'Vai al login' : 'OK'}
        onClose={() => setInfo(null)}
      />
    </KeyboardAvoidingView>
  );
}
