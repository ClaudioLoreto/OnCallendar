import React from 'react';
import {
  ActivityIndicator,
  Image,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  TextInputProps,
  TouchableOpacity,
  View,
  ViewStyle,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTheme } from '../theme/ThemeContext';
import { resolveAvatarUrl } from '../api/endpoints';

// ---------- Icon ----------
export type IconName = React.ComponentProps<typeof Ionicons>['name'];
export const Icon: React.FC<{ name: IconName; size?: number; color?: string }> = ({ name, size = 18, color }) => {
  const { theme } = useTheme();
  return <Ionicons name={name} size={size} color={color ?? theme.colors.textPrimary} />;
};

// ---------- Card ----------
export const Card: React.FC<{ children: React.ReactNode; style?: ViewStyle }> = ({ children, style }) => {
  const { theme } = useTheme();
  return (
    <View style={[
      {
        backgroundColor: theme.colors.surface,
        borderRadius: theme.radius.l,
        padding: theme.spacing.l,
        marginBottom: theme.spacing.m,
        ...theme.shadows.card,
      },
      style,
    ]}>{children}</View>
  );
};

// ---------- Button ----------
type BtnVariant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'subtle';
export const Button: React.FC<{
  title?: string;
  onPress: () => void;
  variant?: BtnVariant;
  loading?: boolean;
  disabled?: boolean;
  icon?: IconName;
  full?: boolean;
  compact?: boolean;
}> = ({ title, onPress, variant = 'primary', loading, disabled, icon, full = true, compact = false }) => {
  const { theme } = useTheme();
  const map: Record<BtnVariant, { bg: string; fg: string; border?: string }> = {
    primary:   { bg: theme.colors.primary,    fg: theme.scheme === 'dark' ? theme.colors.background : theme.colors.white },
    secondary: { bg: theme.colors.secondary,  fg: theme.colors.white },
    danger:    { bg: theme.colors.danger,     fg: theme.colors.white },
    subtle:    { bg: theme.colors.surfaceAlt, fg: theme.colors.textPrimary },
    ghost:     { bg: 'transparent',           fg: theme.colors.primary, border: theme.colors.primary },
  };
  const s = map[variant];
  const iconOnly = !title;

  return (
    <TouchableOpacity
      onPress={onPress}
      disabled={disabled || loading}
      activeOpacity={0.85}
      style={{
        backgroundColor: s.bg,
        opacity: disabled ? 0.5 : 1,
        paddingVertical: compact ? 8 : theme.spacing.m,
        paddingHorizontal: iconOnly ? 0 : (compact ? theme.spacing.m : theme.spacing.l),
        width: iconOnly ? (compact ? 36 : 44) : undefined,
        height: iconOnly ? (compact ? 36 : 44) : undefined,
        borderRadius: iconOnly ? 999 : theme.radius.m,
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: iconOnly ? undefined : 44,
        flexDirection: 'row',
        gap: title ? 8 : 0,
        borderWidth: s.border ? 1 : 0,
        borderColor: s.border,
        alignSelf: full && !iconOnly ? 'stretch' : 'flex-start',
      }}
    >
      {loading ? (
        <ActivityIndicator color={s.fg} />
      ) : (
        <>
          {icon ? <Ionicons name={icon} size={iconOnly ? 20 : 18} color={s.fg} /> : null}
          {title ? <Text style={{ ...theme.typography.button, color: s.fg }}>{title}</Text> : null}
        </>
      )}
    </TouchableOpacity>
  );
};

// ---------- Badge ----------
export const Badge: React.FC<{
  label: string;
  tone?: 'info' | 'success' | 'warning' | 'danger' | 'neutral';
}> = ({ label, tone = 'info' }) => {
  const { theme } = useTheme();
  const map = {
    info:    { bg: theme.colors.accent,     fg: theme.colors.primary },
    success: { bg: '#D5F0DE',               fg: theme.colors.success },
    warning: { bg: '#FBE7C6',               fg: theme.colors.warning },
    danger:  { bg: '#FBE3E1',               fg: theme.colors.danger },
    neutral: { bg: theme.colors.surfaceAlt, fg: theme.colors.textSecondary },
  } as const;
  const s = map[tone];
  return (
    <View style={{
      backgroundColor: s.bg,
      paddingHorizontal: 10,
      paddingVertical: 4,
      borderRadius: theme.radius.pill,
      alignSelf: 'flex-start',
    }}>
      <Text style={{ fontSize: 12, fontWeight: '700', color: s.fg }}>{label}</Text>
    </View>
  );
};

// ---------- Empty state ----------
export const EmptyState: React.FC<{ title: string; subtitle?: string; icon?: IconName }> = ({ title, subtitle, icon }) => {
  const { theme } = useTheme();
  return (
    <View style={{ padding: theme.spacing.xl, alignItems: 'center' }}>
      {icon ? <Ionicons name={icon} size={42} color={theme.colors.textMuted} style={{ marginBottom: theme.spacing.s }} /> : null}
      <Text style={[theme.typography.h3, { textAlign: 'center' }]}>{title}</Text>
      {subtitle ? (
        <Text style={[theme.typography.caption, { textAlign: 'center', marginTop: 4 }]}>{subtitle}</Text>
      ) : null}
    </View>
  );
};

// ---------- Avatar ----------
export const Avatar: React.FC<{
  fullName: string;
  url?: string | null;
  size?: number;
  onPress?: () => void;
}> = ({ fullName, url, size = 40, onPress }) => {
  const { theme } = useTheme();
  const initials = (fullName ?? '?')
    .split(' ').filter(Boolean).slice(0, 2)
    .map(s => s[0]?.toUpperCase()).join('') || '?';
  const resolved = resolveAvatarUrl(url);

  const inner = (
    <View style={{
      width: size, height: size, borderRadius: size / 2,
      backgroundColor: theme.colors.secondary,
      alignItems: 'center', justifyContent: 'center',
      borderWidth: 2, borderColor: theme.colors.surface,
      overflow: 'hidden',
    }}>
      {resolved ? (
        <Image source={{ uri: resolved }} style={{ width: size, height: size }} resizeMode="cover" />
      ) : (
        <Text style={{ color: theme.colors.white, fontWeight: '700', fontSize: size * 0.4 }}>{initials}</Text>
      )}
    </View>
  );
  if (!onPress) return inner;
  return (
    <TouchableOpacity onPress={onPress} activeOpacity={0.7}>
      {inner}
    </TouchableOpacity>
  );
};

// ---------- Segmented control ----------
export function SegmentedControl<T extends string>({
  options, value, onChange,
}: { options: { label: string; value: T }[]; value: T; onChange: (v: T) => void }) {
  const { theme } = useTheme();
  return (
    <View style={{
      flexDirection: 'row',
      backgroundColor: theme.colors.surfaceAlt,
      borderRadius: theme.radius.pill,
      padding: 4,
      marginBottom: theme.spacing.m,
    }}>
      {options.map(opt => {
        const active = opt.value === value;
        return (
          <TouchableOpacity
            key={opt.value}
            onPress={() => onChange(opt.value)}
            style={{
              flex: 1,
              paddingVertical: 10,
              borderRadius: theme.radius.pill,
              backgroundColor: active ? theme.colors.surface : 'transparent',
              alignItems: 'center',
              ...(active ? theme.shadows.card : {}),
            }}
          >
            <Text style={{
              fontWeight: active ? '700' : '500',
              color: active ? theme.colors.primary : theme.colors.textSecondary,
            }}>{opt.label}</Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

// ---------- Custom Modal (sheet) ----------
export const Sheet: React.FC<{
  visible: boolean;
  onClose: () => void;
  title?: string;
  children: React.ReactNode;
}> = ({ visible, onClose, title, children }) => {
  const { theme } = useTheme();
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable
        style={{ flex: 1, backgroundColor: theme.colors.overlay, justifyContent: 'flex-end' }}
        onPress={onClose}
      >
        <Pressable
          style={{
            backgroundColor: theme.colors.surface,
            borderTopLeftRadius: theme.radius.xl,
            borderTopRightRadius: theme.radius.xl,
            padding: theme.spacing.l,
            paddingBottom: theme.spacing.xxl,
            maxHeight: '85%',
          }}
          onPress={() => { /* swallow */ }}
        >
          <View style={{
            alignSelf: 'center', width: 40, height: 5, borderRadius: 3,
            backgroundColor: theme.colors.border, marginBottom: theme.spacing.m,
          }} />
          {title ? (
            <Text style={[theme.typography.h2, { marginBottom: theme.spacing.m }]}>{title}</Text>
          ) : null}
          {children}
        </Pressable>
      </Pressable>
    </Modal>
  );
};

// ---------- Themed TextInput ----------
export const Field: React.FC<{ label?: string; readonly?: boolean } & TextInputProps> = ({ label, readonly, style, ...rest }) => {
  const { theme } = useTheme();
  return (
    <View style={{ marginBottom: theme.spacing.m }}>
      {label ? (
        <Text style={[theme.typography.caption, { marginBottom: 4, color: theme.colors.textSecondary }]}>{label}</Text>
      ) : null}
      <TextInput
        editable={!readonly}
        placeholderTextColor={theme.colors.textMuted}
        style={[
          {
            borderWidth: 1, borderColor: theme.colors.border,
            borderRadius: theme.radius.m, paddingHorizontal: theme.spacing.m, paddingVertical: 12,
            fontSize: 16, color: theme.colors.textPrimary,
            backgroundColor: readonly ? theme.colors.surfaceAlt : theme.colors.surface,
            opacity: readonly ? 0.85 : 1,
          },
          style as any,
        ]}
        {...rest}
      />
    </View>
  );
};

// ---------- Read-only labeled value ----------
export const ReadValue: React.FC<{ label: string; value?: string | null; icon?: IconName }> = ({ label, value, icon }) => {
  const { theme } = useTheme();
  return (
    <View style={{ marginBottom: theme.spacing.m }}>
      <Text style={[theme.typography.caption, { color: theme.colors.textSecondary, marginBottom: 2 }]}>{label}</Text>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
        {icon ? <Ionicons name={icon} size={16} color={theme.colors.textSecondary} /> : null}
        <Text style={[theme.typography.body, { color: value ? theme.colors.textPrimary : theme.colors.textMuted }]}>
          {value || '—'}
        </Text>
      </View>
    </View>
  );
};

// ---------- Row helper ----------
export const Row: React.FC<{ children: React.ReactNode; style?: ViewStyle }> = ({ children, style }) => (
  <View style={[{ flexDirection: 'row', alignItems: 'center', gap: 8 }, style]}>{children}</View>
);

const _styles = StyleSheet.create({});
export default _styles;
