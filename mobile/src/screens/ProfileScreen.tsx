import React, { useEffect, useState } from 'react';
import {
  ActivityIndicator, Alert, SafeAreaView, ScrollView, Text, TouchableOpacity, View,
} from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { Avatar, Badge, Button, Card, Field, Icon, ReadValue, SegmentedControl } from '../components/ui';
import { UsersApi, MeDto, AuthApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { useTheme } from '../theme/ThemeContext';
import { useI18n, Locale } from '../i18n/I18nContext';
import { ThemePreference } from '../theme/ThemeContext';

type Props = { navigation: { goBack: () => void; navigate: (r: string) => void } };

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
    setEditing(false);
    setError(null);
  };

  const save = async () => {
    setSaving(true); setError(null);
    try {
      const updated = await UsersApi.updateMe({ firstName, lastName, email, phone });
      setMe(updated);
      setEditing(false);
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setSaving(false);
    }
  };

  const pickAvatar = async () => {
    const perm = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!perm.granted) {
      Alert.alert('Permessi', 'Serve l\'accesso alla galleria per cambiare l\'avatar.');
      return;
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

  const requestEmailVerification = async () => {
    try {
      await AuthApi.forgotPassword(me?.email ?? '');
      Alert.alert('OK', 'Email di verifica inviata (se attiva).');
    } catch {
      Alert.alert(t('common.notImplemented'), 'Verifica email in arrivo.');
    }
  };

  const onLogout = async () => {
    await logout();
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
      <ScrollView contentContainerStyle={{ padding: theme.spacing.l, paddingBottom: theme.spacing.xxl }}>
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

        {/* Anagrafica */}
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
              {!me.emailConfirmed ? (
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 4 }}>
                  <Badge label={t('profile.unverified.email')} tone="warning" />
                  <TouchableOpacity onPress={requestEmailVerification}>
                    <Text style={{ color: theme.colors.primary, fontWeight: '600' }}>{t('profile.verify')}</Text>
                  </TouchableOpacity>
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
              {error ? (
                <Text style={{ color: theme.colors.danger, marginBottom: theme.spacing.s }}>{error}</Text>
              ) : null}
              <View style={{ flexDirection: 'row', gap: theme.spacing.s }}>
                <View style={{ flex: 1 }}>
                  <Button title={t('common.cancel')} variant="subtle" onPress={cancelEdit} />
                </View>
                <View style={{ flex: 1 }}>
                  <Button title={t('profile.save')} icon="checkmark" onPress={save} loading={saving} />
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
            onPress={() => {
              // Profile è ora uno screen normale, navigate funziona direttamente
              navigation.navigate('History');
            }}
          />
        </Card>

        <Card>
          <Button title={t('profile.logout')} variant="danger" icon="log-out-outline" onPress={onLogout} />
        </Card>
      </ScrollView>
    </SafeAreaView>
  );
}
