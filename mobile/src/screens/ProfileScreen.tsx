import React, { useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator, KeyboardAvoidingView, Platform, SafeAreaView, ScrollView, Text, TouchableOpacity, View,
} from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import {
  Avatar, Badge, Button, Card, ConfirmModal, Field, Icon, PasswordField, ReadValue, SegmentedControl,
} from '../components/ui';
import { UsersApi, MeDto } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { useTheme } from '../theme/ThemeContext';
import { useI18n, Locale } from '../i18n/I18nContext';
import { ThemePreference } from '../theme/ThemeContext';
import { buildCallbackUrl } from '../api/callbackUrl';
import { PasswordRules, isPasswordStrong } from './ResetPasswordScreen';

type Props = { navigation: { goBack: () => void; navigate: (r: string) => void } };

type Popup = {
  title: string;
  message?: string;
  tone?: 'default' | 'success' | 'warning' | 'danger';
  icon?: any;
  confirmLabel?: string;
  cancelLabel?: string;
  onConfirm?: () => void;
};

export default function ProfileScreen({ navigation }: Props) {
  const { theme, preference, setPreference } = useTheme();
  const { t, locale, setLocale } = useI18n();
  const { user, logout } = useAuth();

  const [me, setMe] = useState<MeDto | null>(null);
  const [editing, setEditing] = useState(false);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Password change inline (dentro l'edit anagrafica)
  const [curPwd, setCurPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [newPwd2, setNewPwd2] = useState('');
  const [pwdOpen, setPwdOpen] = useState(false);

  const [popup, setPopup] = useState<Popup | null>(null);

  const newPwdStrong = useMemo(() => (newPwd.length === 0 ? true : isPasswordStrong(newPwd)), [newPwd]);
  const wantsPwdChange = pwdOpen && (curPwd.length > 0 || newPwd.length > 0 || newPwd2.length > 0);

  // Salva abilitato solo se ho modificato qualcosa nel form (oppure sto cambiando password).
  const isDirty = useMemo(() => {
    if (!me) return false;
    if (firstName !== me.firstName) return true;
    if (lastName !== me.lastName) return true;
    if (email.trim().toLowerCase() !== me.email.toLowerCase()) return true;
    if ((phone ?? '') !== (me.phone ?? '')) return true;
    if (wantsPwdChange) return true;
    return false;
  }, [me, firstName, lastName, email, phone, wantsPwdChange]);

  const loadMe = async () => {
    try {
      const data = await UsersApi.me();
      setMe(data);
      setFirstName(data.firstName);
      setLastName(data.lastName);
      setEmail(data.email);
      setPhone(data.phone ?? '');
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    }
  };

  useEffect(() => { loadMe(); }, []);

  const startEdit = () => {
    setError(null);
    setEditing(true);
  };

  const cancelEdit = () => {
    if (!me) return;
    setFirstName(me.firstName);
    setLastName(me.lastName);
    setEmail(me.email);
    setPhone(me.phone ?? '');
    setCurPwd(''); setNewPwd(''); setNewPwd2('');
    setPwdOpen(false);
    setEditing(false);
    setError(null);
  };

  const save = async () => {
    // Validazione cambio password (se compilato)
    if (wantsPwdChange) {
      if (!curPwd) { setPopup({ title: 'Password attuale richiesta', message: 'Per cambiare la password devi inserire quella attuale.', tone: 'warning', icon: 'alert-circle-outline' }); return; }
      if (curPwd === newPwd) { setPopup({ title: 'Password non valida', message: 'La nuova password deve essere diversa da quella attuale.', tone: 'warning', icon: 'alert-circle-outline' }); return; }
      if (!isPasswordStrong(newPwd)) { setPopup({ title: 'Nuova password troppo debole', message: 'Verifica i requisiti elencati sotto la nuova password.', tone: 'warning', icon: 'shield-half-outline' }); return; }
      if (newPwd !== newPwd2) { setPopup({ title: 'Le password non coincidono', tone: 'warning', icon: 'alert-circle-outline' }); return; }
    }

    setSaving(true); setError(null);
    try {
      const emailChanged = !!me && email.trim().toLowerCase() !== me.email.toLowerCase();
      const updated = await UsersApi.updateMe({ firstName, lastName, phone });
      setMe(updated);

      // Cambio password
      if (wantsPwdChange) {
        try {
          await UsersApi.changePassword(curPwd, newPwd);
          setCurPwd(''); setNewPwd(''); setNewPwd2('');
          // ricarica me per refresh dei flag passwordChangedAtUtc / passwordChangeRequired / passwordExpired
          await loadMe();
          setPopup({ title: 'Password aggiornata', message: 'La tua nuova password è ora attiva.', tone: 'success', icon: 'shield-checkmark-outline' });
        } catch (e: any) {
          const msg = e?.response?.data?.errors?.join(' ') ?? e?.response?.data?.error ?? 'Cambio password non riuscito.';
          setPopup({ title: 'Cambio password fallito', message: msg, tone: 'danger', icon: 'alert-circle-outline' });
          setSaving(false);
          return;
        }
      }

      // Cambio email (richiede conferma via link)
      if (emailChanged) {
        try {
          const r = await UsersApi.requestEmailChange(email.trim(), buildCallbackUrl('confirm-email'));
          setMe(prev => prev ? { ...prev, pendingEmail: r.pendingEmail } : prev);
          setPopup({
            title: 'Conferma email inviata',
            message: `Ti abbiamo inviato un link a ${r.pendingEmail}. Apri quella casella e clicca il pulsante per attivare il nuovo indirizzo.`,
            tone: 'success',
            icon: 'mail-outline',
          });
        } catch (e: any) {
          setPopup({ title: 'Errore cambio email', message: e?.response?.data?.error ?? 'Impossibile avviare la richiesta.', tone: 'danger', icon: 'alert-circle-outline' });
        }
      }
      setEditing(false);
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setSaving(false);
    }
  };

  const pickAvatar = async () => {
    // Pre-prompt brandizzato: spieghiamo prima a cosa serve l'accesso, poi
    // chiediamo il permesso di sistema (iOS/Android mostreranno il proprio
    // dialog con il testo configurato in app.json -> infoPlist / plugins).
    // Nota: in Expo Go iOS mostra ANCHE un dialog generico "Experience needs
    // permissions" che e` parte di Expo Go stesso e non si puo` rimuovere; in
    // una build standalone (EAS) appare solo il nostro testo nativo localizzato.
    const already = await ImagePicker.getMediaLibraryPermissionsAsync();
    if (!already.granted) {
      const proceed = await new Promise<boolean>(resolve => {
        setPopup({
          title: 'Foto profilo',
          message: 'Per cambiare avatar OnCallendar deve poter leggere una foto dalla tua galleria. Concedi il permesso quando il sistema te lo chiede.',
          tone: 'default',
          icon: 'image-outline',
          confirmLabel: 'Continua',
          cancelLabel: 'Annulla',
          onConfirm: () => resolve(true),
        });
        // Se l'utente chiude (X / fuori), risolvi false.
        setTimeout(() => resolve(false), 30_000);
      });
      if (!proceed) return;
      const perm = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (!perm.granted) {
        setPopup({ title: 'Permesso negato', message: "Senza accesso alla galleria non posso cambiare l'avatar. Puoi abilitarlo dalle Impostazioni del telefono.", tone: 'warning', icon: 'image-outline' });
        return;
      }
    }
    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.6,
    });
    if (result.canceled || !result.assets?.[0]) return;
    setUploading(true); setError(null);
    try {
      const updated = await UsersApi.uploadAvatar(result.assets[0].uri);
      setMe(updated);
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore upload');
    } finally {
      setUploading(false);
    }
  };

  const onLogout = async () => { await logout(); };

  const onDevExpire = () => {
    setPopup({
      title: 'DEV: simula scadenza password',
      message: 'Imposto la mia data ultimo cambio password a 366 giorni fa, poi ti faccio uscire. Al prossimo login l\'app mostrer\u00e0 la schermata bloccante di reset password.',
      tone: 'danger',
      icon: 'bug-outline',
      confirmLabel: 'Conferma',
      cancelLabel: 'Annulla',
      onConfirm: async () => {
        try {
          await UsersApi.devExpirePassword();
          await logout();
        } catch (e: any) {
          setPopup({ title: 'Errore', message: e?.response?.data?.error ?? 'Operazione non riuscita.', tone: 'danger', icon: 'alert-circle-outline' });
        }
      },
    });
  };

  if (!me) {
    return (
      <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background, justifyContent: 'center' }}>
        <ActivityIndicator color={theme.colors.primary} />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: theme.colors.background }}>
      <KeyboardAvoidingView
        style={{ flex: 1 }}
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
        keyboardVerticalOffset={Platform.OS === 'ios' ? 24 : 0}
      >
      <ScrollView
        contentContainerStyle={{ padding: theme.spacing.l, paddingBottom: theme.spacing.xxl }}
        keyboardShouldPersistTaps="handled"
      >
        {/* Header avatar */}
        <View style={{ alignItems: 'center', marginBottom: theme.spacing.l }}>
          <View>
            <Avatar fullName={user?.fullName ?? ''} url={me.avatarUrl} size={96} />
            <TouchableOpacity
              onPress={pickAvatar}
              disabled={uploading}
              style={{
                position: 'absolute', right: -4, bottom: -4,
                backgroundColor: theme.colors.primary,
                width: 32, height: 32, borderRadius: 16,
                alignItems: 'center', justifyContent: 'center',
                borderWidth: 2, borderColor: theme.colors.surface,
              }}
            >
              {uploading
                ? <ActivityIndicator size="small" color={theme.colors.white} />
                : <Icon name="camera" size={16} color={theme.colors.white} />}
            </TouchableOpacity>
          </View>
          <Text style={[theme.typography.h2, { marginTop: theme.spacing.s }]}>{me.firstName} {me.lastName}</Text>
          <Text style={theme.typography.caption}>{me.email}</Text>
        </View>

        {/* Anagrafica + (in edit) cambio password */}
        <Card>
          <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: theme.spacing.m }}>
            <Text style={theme.typography.h3}>{t('profile.section.personal')}</Text>
            {!editing ? (
              <TouchableOpacity onPress={startEdit} hitSlop={10}>
                <Icon name="create-outline" size={22} color={theme.colors.primary} />
              </TouchableOpacity>
            ) : null}
          </View>

          {!editing ? (
            <>
              <ReadValue label={t('profile.firstName')} value={me.firstName} icon="person-outline" />
              <ReadValue label={t('profile.lastName')} value={me.lastName} icon="person-outline" />
              <ReadValue label={t('profile.email')} value={me.email} icon="mail-outline" />
              <ReadValue label={t('profile.phone')} value={me.phone} icon="call-outline" />
              <ReadValue label="Password" value="••••••••" icon="lock-closed-outline" />
              {!me.emailConfirmed ? (
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 4 }}>
                  <Badge label={t('profile.unverified.email')} tone="warning" />
                </View>
              ) : null}
            </>
          ) : (
            <>
              <Field label={t('profile.firstName')} value={firstName} onChangeText={setFirstName} />
              <Field label={t('profile.lastName')} value={lastName} onChangeText={setLastName} />
              <Field
                label={t('profile.email')}
                value={email} onChangeText={setEmail}
                keyboardType="email-address" autoCapitalize="none"
              />
              <Field
                label={t('profile.phone')}
                value={phone} onChangeText={setPhone}
                keyboardType="phone-pad"
              />

              {/* CAMBIO PASSWORD: campo mascherato + matita per aprire la tendina */}
              <View style={{ marginTop: 4 }}>
                <Text style={[theme.typography.caption, { marginBottom: 6, color: theme.colors.textSecondary }]}>Password</Text>
                <TouchableOpacity
                  activeOpacity={0.7}
                  onPress={() => setPwdOpen(o => !o)}
                  style={{
                    flexDirection: 'row', alignItems: 'center',
                    borderWidth: 1, borderColor: theme.colors.border,
                    backgroundColor: theme.colors.surface,
                    borderRadius: theme.radius.m,
                    paddingHorizontal: 12, paddingVertical: 12,
                    gap: 8,
                  }}
                >
                  <Icon name="lock-closed-outline" size={16} color={theme.colors.textSecondary} />
                  <Text style={[theme.typography.body, { flex: 1, letterSpacing: 2 }]}>••••••••</Text>
                  <Icon name={pwdOpen ? 'chevron-up' : 'create-outline'} size={18} color={theme.colors.primary} />
                </TouchableOpacity>

                {pwdOpen ? (
                  <View style={{ marginTop: theme.spacing.s, paddingTop: theme.spacing.s, borderTopWidth: 1, borderTopColor: theme.colors.border }}>
                    <Text style={[theme.typography.caption, { marginBottom: theme.spacing.s }]}>
                      Inserisci la password attuale e la nuova password.
                    </Text>
                    <PasswordField label="Password attuale" value={curPwd} onChangeText={setCurPwd} />
                    <PasswordField label="Nuova password" value={newPwd} onChangeText={setNewPwd} />
                    {newPwd.length > 0 ? <PasswordRules value={newPwd} /> : null}
                    {newPwd.length > 0 && curPwd.length > 0 && newPwd === curPwd ? (
                      <Text style={{ color: theme.colors.danger, marginTop: -theme.spacing.s, marginBottom: theme.spacing.s }}>
                        La nuova password deve essere diversa da quella attuale.
                      </Text>
                    ) : null}
                    <PasswordField label="Conferma nuova password" value={newPwd2} onChangeText={setNewPwd2} />
                  </View>
                ) : null}
              </View>

              {error ? (
                <Text style={{ color: theme.colors.danger, marginBottom: theme.spacing.s }}>{error}</Text>
              ) : null}
              <View style={{
                flexDirection: 'row',
                gap: theme.spacing.m,
                marginTop: pwdOpen ? theme.spacing.m : theme.spacing.l,
              }}>
                <View style={{ flex: 1 }}>
                  <Button title={t('common.cancel')} variant="subtle" compact onPress={cancelEdit} />
                </View>
                <View style={{ flex: 1 }}>
                  <Button
                    title={t('profile.save')}
                    icon="checkmark"
                    compact
                    onPress={save}
                    loading={saving}
                    disabled={!isDirty || (wantsPwdChange && (!newPwdStrong || curPwd === newPwd))}
                  />
                </View>
              </View>
            </>
          )}
        </Card>

        {/* Impostazioni */}
        <Card>
          <Text style={[theme.typography.h3, { marginBottom: theme.spacing.m }]}>{t('profile.section.settings')}</Text>

          <Text style={[theme.typography.caption, { marginBottom: 6 }]}>{t('profile.theme')}</Text>
          <SegmentedControl<ThemePreference>
            value={preference}
            onChange={async v => {
              await setPreference(v);
              try { await UsersApi.updateMe({ themePreference: v }); } catch {}
            }}
            options={[
              { label: t('profile.theme.light'), value: 'light' },
              { label: t('profile.theme.dark'), value: 'dark' },
            ]}
          />

          <Text style={[theme.typography.caption, { marginBottom: 6, marginTop: theme.spacing.s }]}>
            {t('profile.language')}
          </Text>
          <SegmentedControl<Locale>
            value={locale}
            onChange={async v => {
              await setLocale(v);
              try { await UsersApi.updateMe({ preferredLanguage: v }); } catch {}
            }}
            options={[
              { label: 'Italiano', value: 'it' },
              { label: 'English', value: 'en' },
            ]}
          />
        </Card>

        {/* Storico */}
        <Card>
          <Text style={[theme.typography.h3, { marginBottom: theme.spacing.s }]}>{t('profile.section.history')}</Text>
          <Text style={[theme.typography.caption, { marginBottom: theme.spacing.m }]}>
            Vedi tutti i turni passati e le richieste di scambio.
          </Text>
          <Button title="Apri storico" variant="secondary" icon="time-outline"
            onPress={() => navigation.navigate('History')}
          />
        </Card>

        {/* Preferenze notifiche */}
        <Card>
          <Text style={[theme.typography.h3, { marginBottom: theme.spacing.s }]}>{t('notifPrefs.title')}</Text>
          <Text style={[theme.typography.caption, { marginBottom: theme.spacing.m }]}>
            {t('notifPrefs.intro')}
          </Text>
          <Button title={t('notifPrefs.open')} variant="secondary" icon="notifications-outline"
            onPress={() => navigation.navigate('NotificationPreferences')} />
        </Card>

        {/* DEV: scadenza password (sempre visibile) */}
        <Card>
          <Text style={[theme.typography.h3, { marginBottom: theme.spacing.s }]}>Strumenti sviluppatore</Text>
          <Text style={[theme.typography.caption, { marginBottom: theme.spacing.m }]}>
            Bottone usato solo per testare il flusso di scadenza password.
          </Text>
          <Button
            title="DEV: simula scadenza password (1 anno)"
            variant="danger"
            icon="bug-outline"
            onPress={onDevExpire}
          />
        </Card>

        <Card>
          <Button title={t('profile.logout')} variant="danger" icon="log-out-outline" onPress={onLogout} />
        </Card>
      </ScrollView>
      </KeyboardAvoidingView>

      <ConfirmModal
        visible={!!popup}
        title={popup?.title ?? ''}
        message={popup?.message}
        tone={popup?.tone}
        icon={popup?.icon}
        confirmLabel={popup?.confirmLabel}
        cancelLabel={popup?.cancelLabel}
        onConfirm={popup?.onConfirm ? () => { const fn = popup.onConfirm; setPopup(null); fn?.(); } : undefined}
        onClose={() => setPopup(null)}
      />
    </SafeAreaView>
  );
}
