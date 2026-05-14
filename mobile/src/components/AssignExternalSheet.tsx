import React, { useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, ScrollView, Text, TouchableOpacity, View } from 'react-native';
import { Avatar, Badge, Button, Field, Icon, Sheet } from './ui';
import { useTheme } from '../theme/ThemeContext';
import {
  ExternalDoctorDto, ExternalDoctorsApi, ShiftDto, ShiftsApi, shiftCodeIcon,
} from '../api/endpoints';

type Props = {
  shift: ShiftDto | null;
  isReperibile?: boolean;
  onClose: () => void;
  onUpdated: (s: ShiftDto) => void;
};

/**
 * Sheet per affidare un turno a un medico ESTERNO (non utente dell'app).
 * - Input nome + cognome con autocomplete sui medici esterni gia` censiti.
 * - Se il turno e` gia` assegnato a un esterno, mostra opzione "Riprendi turno".
 */
export default function AssignExternalSheet({ shift, isReperibile = false, onClose, onUpdated }: Props) {
  const { theme } = useTheme();
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName]   = useState('');
  const [phone, setPhone]         = useState('');
  const [email, setEmail]         = useState('');
  const [suggestions, setSuggestions] = useState<ExternalDoctorDto[]>([]);
  const [loadingSugg, setLoadingSugg] = useState(false);
  const [submitting, setSubmitting]   = useState(false);
  const [error, setError] = useState<string | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Reset campi quando si apre/chiude
  useEffect(() => {
    if (shift) {
      setFirstName('');
      setLastName('');
      setPhone('');
      setEmail('');
      setSuggestions([]);
      setError(null);
    }
  }, [shift?.id]);

  // Debounce della ricerca suggerimenti
  const term = useMemo(() => `${firstName} ${lastName}`.trim(), [firstName, lastName]);
  useEffect(() => {
    if (!shift) return;
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (term.length < 2) {
      setSuggestions([]);
      return;
    }
    setLoadingSugg(true);
    debounceRef.current = setTimeout(async () => {
      try {
        const res = await ExternalDoctorsApi.search(term);
        setSuggestions(res);
      } catch {
        setSuggestions([]);
      } finally {
        setLoadingSugg(false);
      }
    }, 250);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [term, shift]);

  const pickSuggestion = (s: ExternalDoctorDto) => {
    setFirstName(s.firstName);
    setLastName(s.lastName);
    if (s.phone) setPhone(s.phone);
    setSuggestions([]);
  };

  const submit = async () => {
    if (!shift) return;
    if (!firstName.trim() || !lastName.trim()) {
      setError('Inserisci sia nome che cognome.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const updated = await ShiftsApi.assignExternal(shift.id, firstName.trim(), lastName.trim(), phone.trim() || undefined, email.trim() || undefined, isReperibile);
      onUpdated(updated);
      onClose();
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore durante l\'assegnazione');
    } finally {
      setSubmitting(false);
    }
  };

  const removeExternal = async () => {
    if (!shift) return;
    setSubmitting(true);
    setError(null);
    try {
      const updated = await ShiftsApi.clearExternal(shift.id, isReperibile);
      onUpdated(updated);
      onClose();
    } catch (e: any) {
      setError(e?.response?.data?.error ?? e?.message ?? 'Errore');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Sheet visible={!!shift} onClose={onClose} title={isReperibile ? 'Affida reperibilit\u00e0 a medico esterno' : 'Affida a medico esterno'}>
      {shift ? (
        <ScrollView keyboardShouldPersistTaps="handled">
          {/* Riepilogo turno */}
          <View style={{
            padding: theme.spacing.m, borderRadius: theme.radius.m,
            backgroundColor: theme.colors.accent, marginBottom: theme.spacing.m,
            flexDirection: 'row', alignItems: 'center', gap: 12,
          }}>
            <Icon name={shiftCodeIcon(shift.code) as any} size={28} color={theme.colors.primary} />
            <View style={{ flex: 1 }}>
              <Text style={[theme.typography.body, { fontWeight: '700' }]}>
                {shift.date} · {shift.codeLabel}
              </Text>
              <Text style={theme.typography.caption}>
                {shift.startLocal} – {shift.endLocal}
              </Text>
            </View>
          </View>

          {/* Stato corrente */}
          {(isReperibile ? shift.externalDoctorReperibile : shift.externalDoctor) ? (() => {
            const ext = isReperibile ? shift.externalDoctorReperibile! : shift.externalDoctor!;
            return (
            <View style={{
              padding: theme.spacing.m, borderRadius: theme.radius.m,
              backgroundColor: theme.colors.surfaceAlt, marginBottom: theme.spacing.m,
              borderWidth: 1, borderColor: theme.colors.border,
            }}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 6 }}>
                <Avatar fullName={ext.fullName} size={28} />
                <View style={{ flex: 1 }}>
                  <Text style={[theme.typography.body, { fontWeight: '700' }]}>
                    {ext.fullName}
                  </Text>
                  <Text style={theme.typography.caption}>Medico esterno attualmente assegnato</Text>
                </View>
                <Badge label="Esterno" tone="warning" />
              </View>
              <Button
                title={isReperibile ? 'Riprendi reperibilit\u00e0 (rimuovi esterno)' : 'Riprendi turno (rimuovi esterno)'}
                icon="close-circle-outline"
                variant="secondary"
                onPress={removeExternal}
                disabled={submitting}
              />
            </View>
            );
          })() : null}

          <Text style={[theme.typography.caption, { marginBottom: theme.spacing.s }]}>
            Inserisci nome e cognome del medico esterno che coprir\u00e0 {isReperibile ? 'la reperibilit\u00e0' : 'il turno'}.
            Se l{"'"}hai gia` censito in passato lo trovi tra i suggerimenti.
          </Text>

          <Field
            label="Nome"
            value={firstName}
            onChangeText={setFirstName}
            autoCapitalize="words"
            autoCorrect={false}
          />
          <Field
            label="Cognome"
            value={lastName}
            onChangeText={setLastName}
            autoCapitalize="words"
            autoCorrect={false}
          />

          {/* Suggerimenti */}
          {loadingSugg ? (
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: theme.spacing.m }}>
              <ActivityIndicator size="small" />
              <Text style={theme.typography.caption}>Cerco tra i medici gia` censiti…</Text>
            </View>
          ) : null}
          {suggestions.length > 0 ? (
            <View style={{
              borderWidth: 1, borderColor: theme.colors.border, borderRadius: theme.radius.m,
              backgroundColor: theme.colors.surface, marginBottom: theme.spacing.m, overflow: 'hidden',
            }}>
              <View style={{
                paddingHorizontal: theme.spacing.m, paddingVertical: theme.spacing.s,
                backgroundColor: theme.colors.surfaceAlt,
                borderBottomWidth: 1, borderBottomColor: theme.colors.border,
                flexDirection: 'row', alignItems: 'center', gap: 6,
              }}>
                <Icon name="bulb-outline" size={14} color={theme.colors.textSecondary} />
                <Text style={[theme.typography.caption, { fontWeight: '700' }]}>
                  Medici esterni gia` censiti
                </Text>
              </View>
              {suggestions.map((s, i) => (
                <TouchableOpacity
                  key={s.id}
                  onPress={() => pickSuggestion(s)}
                  style={{
                    flexDirection: 'row', alignItems: 'center', gap: 10,
                    paddingHorizontal: theme.spacing.m, paddingVertical: 10,
                    borderTopWidth: i === 0 ? 0 : 1, borderTopColor: theme.colors.border,
                  }}
                >
                  <Avatar fullName={s.fullName} size={24} />
                  <View style={{ flex: 1 }}>
                    <Text style={[theme.typography.body, { fontWeight: '600' }]}>{s.fullName}</Text>
                    {s.phone ? <Text style={theme.typography.caption}>{s.phone}</Text> : null}
                  </View>
                  <Icon name="add-circle-outline" size={18} color={theme.colors.primary} />
                </TouchableOpacity>
              ))}
            </View>
          ) : null}

          <Field
            label="Telefono (facoltativo)"
            value={phone}
            onChangeText={setPhone}
            keyboardType="phone-pad"
          />

          <Field
            label="Email (facoltativa)"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoCorrect={false}
          />
          <Text style={[theme.typography.caption, { marginTop: -8, marginBottom: theme.spacing.m, fontStyle: 'italic' }]}>
            Se inserisci l{"'"}email, gli verra` inviato un invito a registrarsi su OnCallendar (una sola volta).
          </Text>

          {error ? (
            <View style={{
              backgroundColor: '#FBE3E1', padding: theme.spacing.m,
              borderRadius: theme.radius.m, marginBottom: theme.spacing.m,
            }}>
              <Text style={{ color: theme.colors.danger, fontWeight: '600' }}>{error}</Text>
            </View>
          ) : null}

          <Button
            title={(isReperibile ? shift.externalDoctorReperibile : shift.externalDoctor)
              ? 'Sostituisci medico esterno'
              : isReperibile ? 'Affida reperibilit\u00e0' : 'Affida turno'}
            icon="checkmark-circle-outline"
            onPress={submit}
            disabled={submitting || !firstName.trim() || !lastName.trim()}
          />
        </ScrollView>
      ) : null}
    </Sheet>
  );
}
