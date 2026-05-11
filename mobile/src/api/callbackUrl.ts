import * as Linking from 'expo-linking';

/**
 * Costruisce il callback URL da inviare al backend per i template email
 * (conferma cambio email, reset password). Il backend lo userà come base
 * dei link cliccabili nel template HTML.
 *
 * - In Expo Go: ritorna `exp://<tunnel>/--/<path>` cosi` cliccando il bottone
 *   nella mail si torna alla sessione di sviluppo corrente.
 * - In una build standalone: ritorna `oncallendar://<path>` (custom scheme).
 * - Nella web app pubblica: passare l'URL della web app esplicitamente.
 *
 * IMPORTANTE: ritorniamo solo la BASE (senza la query token), perche` il
 * backend appende `?token=...` o `?email=...&token=...` al template.
 */
export function buildCallbackUrl(path: 'confirm-email' | 'reset-password' | 'register-external'): string {
  // Linking.createURL ritorna l'URL completo per il path richiesto, gestendo
  // automaticamente lo scheme corretto (Expo dev vs build standalone).
  // Esempio Expo Go: exp://172.17.0.1:8081/--/confirm-email
  const full = Linking.createURL(path);
  // Strippiamo eventuali query/hash: il backend appende quello che serve.
  return full.split('?')[0].split('#')[0].replace(/\/$/, '').replace(new RegExp(`/${path}$`), '');
}
