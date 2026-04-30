import React from 'react';
import { Text, View } from 'react-native';
import { Avatar } from './ui';
import { useTheme } from '../theme/ThemeContext';
import { useAuth } from '../auth/AuthContext';

/**
 * Header con titolo e avatar in alto a destra.
 * Cliccando l'avatar si apre la sezione Profilo.
 */
export const AppHeader: React.FC<{
  title: string;
  subtitle?: string;
  onAvatarPress?: () => void;
}> = ({ title, subtitle, onAvatarPress }) => {
  const { theme } = useTheme();
  const { user } = useAuth();

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
      <Avatar fullName={user?.fullName ?? '?'} url={null} onPress={onAvatarPress} />
    </View>
  );
};

export default AppHeader;
