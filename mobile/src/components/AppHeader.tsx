import React from 'react';
import { Text, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from './ui';
import { useTheme } from '../theme/ThemeContext';
import { useAuth } from '../auth/AuthContext';
import { useNotifications } from '../auth/NotificationsContext';

/**
 * Header con titolo, campanella notifiche (badge) e avatar utente.
 */
export const AppHeader: React.FC<{
  title: string;
  subtitle?: string;
  onAvatarPress?: () => void;
  onBellPress?: () => void;
}> = ({ title, subtitle, onAvatarPress, onBellPress }) => {
  const { theme } = useTheme();
  const { user } = useAuth();
  const { unreadCount } = useNotifications();

  return (
    <View style={{
      paddingHorizontal: theme.spacing.l,
      paddingTop: theme.spacing.m,
      paddingBottom: theme.spacing.s,
      flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
      backgroundColor: theme.colors.background,
    }}>
      <View style={{ flex: 1 }}>
        <Text style={theme.typography.h2}>{title}</Text>
        {subtitle ? (
          <Text style={theme.typography.caption}>{subtitle}</Text>
        ) : null}
      </View>

      {/* Campanella notifiche */}
      {onBellPress ? (
        <TouchableOpacity onPress={onBellPress} activeOpacity={0.7}
          style={{ marginRight: theme.spacing.m, position: 'relative' }}>
          <Ionicons
            name={unreadCount > 0 ? 'notifications' : 'notifications-outline'}
            size={24}
            color={unreadCount > 0 ? theme.colors.primary : theme.colors.textSecondary}
          />
          {unreadCount > 0 ? (
            <View style={{
              position: 'absolute', top: -4, right: -6,
              minWidth: 18, height: 18, borderRadius: 9,
              backgroundColor: theme.colors.primary,
              borderWidth: 1.5, borderColor: theme.colors.background,
              alignItems: 'center', justifyContent: 'center',
              paddingHorizontal: 3,
            }}>
              <Text style={{ color: theme.colors.white, fontSize: 10, fontWeight: '800', lineHeight: 12 }}>
                {unreadCount > 99 ? '99+' : String(unreadCount)}
              </Text>
            </View>
          ) : null}
        </TouchableOpacity>
      ) : null}

      <Avatar fullName={user?.fullName ?? '?'} url={null} onPress={onAvatarPress} />
    </View>
  );
};

export default AppHeader;
