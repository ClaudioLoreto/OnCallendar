import { Platform } from 'react-native';
import Constants from 'expo-constants';
import { DeviceTokensApi } from '../api/endpoints';

/**
 * Registra il device per le push notifications Expo.
 *
 * Richiede `expo-notifications` (e implicitamente `expo-device`) installati.
 * Se il pacchetto non è disponibile (es. su web), la funzione esce senza fare
 * nulla — l'app continua a funzionare con le notifiche in-app + email.
 *
 * Strategy:
 *  - su web ritorna null subito (Expo Push non supporta browser);
 *  - prova un dynamic require di expo-notifications: se manca, esce silente;
 *  - chiede permesso, ottiene il token, lo registra sul backend.
 */
export async function registerForPushNotificationsAsync(): Promise<string | null> {
  if (Platform.OS === 'web') return null;

  let Notifications: any;
  try {
    // Lazy require: evita di rompere il bundling web se la dipendenza non è installata.
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    Notifications = require('expo-notifications');
  } catch {
    console.warn('[push] expo-notifications non installato — push disabilitate.');
    return null;
  }

  try {
    // Imposta il behavior di display in foreground.
    if (Notifications.setNotificationHandler) {
      Notifications.setNotificationHandler({
        handleNotification: async () => ({
          shouldShowAlert: true,
          shouldPlaySound: true,
          shouldSetBadge: true,
          shouldShowBanner: true,
          shouldShowList: true,
        }),
      });
    }

    // Su Android serve un canale.
    if (Platform.OS === 'android' && Notifications.setNotificationChannelAsync) {
      await Notifications.setNotificationChannelAsync('default', {
        name: 'OnCallendar',
        importance: 4, // Notifications.AndroidImportance.HIGH
        vibrationPattern: [0, 250, 250, 250],
      });
    }

    // Permessi
    const { status: existing } = await Notifications.getPermissionsAsync();
    let granted = existing === 'granted';
    if (!granted) {
      const req = await Notifications.requestPermissionsAsync();
      granted = req.status === 'granted';
    }
    if (!granted) {
      console.warn('[push] Permesso negato dall\'utente.');
      return null;
    }

    // EAS projectId è richiesto dal nuovo SDK Expo Push.
    const projectId =
      (Constants?.expoConfig as any)?.extra?.eas?.projectId ??
      (Constants as any)?.easConfig?.projectId;

    const tokenResp = await Notifications.getExpoPushTokenAsync(
      projectId ? { projectId } : undefined,
    );
    const token: string | undefined = tokenResp?.data;
    if (!token) return null;

    // Best effort: se il backend è offline non blocchiamo l'app.
    try {
      await DeviceTokensApi.register(token, Platform.OS, undefined);
    } catch (e) {
      console.warn('[push] register backend failed', e);
    }
    return token;
  } catch (e) {
    console.warn('[push] init error', e);
    return null;
  }
}

/** Chiama l'unregister al logout. Best-effort. */
export async function unregisterPushAsync(token: string | null | undefined) {
  if (!token) return;
  try {
    await DeviceTokensApi.unregister(token);
  } catch {
    /* ignore */
  }
}
