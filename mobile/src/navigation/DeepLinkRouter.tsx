import React, { useCallback, useEffect, useState } from 'react';
import { Linking } from 'react-native';
import ResetPasswordScreen from '../screens/ResetPasswordScreen';
import RegisterExternalScreen from '../screens/RegisterExternalScreen';
import { AuthApi } from '../api/endpoints';
import { ConfirmModal } from '../components/ui';

type DeepLink =
  | { kind: 'reset'; email: string; token: string }
  | { kind: 'register-external'; token: string }
  | { kind: 'confirm-email'; token: string }
  | null;

/** Estrae i query param da un URL (anche con scheme custom oncallendar://path?a=1). */
const parseQuery = (url: string): Record<string, string> => {
  const out: Record<string, string> = {};
  const q = url.split('?')[1];
  if (!q) return out;
  for (const pair of q.split('&')) {
    const [k, v] = pair.split('=');
    if (k) out[decodeURIComponent(k)] = v ? decodeURIComponent(v) : '';
  }
  return out;
};

const matchLink = (url: string | null | undefined): DeepLink => {
  if (!url) return null;
  const lower = url.toLowerCase();
  if (lower.includes('/reset-password') || lower.includes('reset-password?')) {
    const q = parseQuery(url);
    if (q.email && q.token) return { kind: 'reset', email: q.email, token: q.token };
  }
  if (lower.includes('/register-external') || lower.includes('register-external?')) {
    const q = parseQuery(url);
    if (q.token) return { kind: 'register-external', token: q.token };
  }
  if (lower.includes('/confirm-email') || lower.includes('confirm-email?')) {
    const q = parseQuery(url);
    if (q.token) return { kind: 'confirm-email', token: q.token };
  }
  return null;
};

/**
 * Intercetta i deep-link `oncallendar://reset-password?email=&token=` e
 * `oncallendar://register-external?token=` e mostra la schermata corrispondente
 * in overlay su tutto il resto dell'app. Permette anche a `setPendingLink()`
 * di essere chiamata manualmente (es. dopo che l'utente incolla un link).
 */
export const DeepLinkRouter: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [pending, setPending] = useState<DeepLink>(null);
  const [confirmingEmail, setConfirmingEmail] = useState<{ status: 'ok' | 'error'; email?: string; error?: string } | null>(null);

  useEffect(() => {
    let cancelled = false;
    Linking.getInitialURL().then(url => {
      if (cancelled) return;
      const m = matchLink(url);
      if (m) setPending(m);
    });
    const sub = Linking.addEventListener('url', e => {
      const m = matchLink(e.url);
      if (m) setPending(m);
    });
    (DeepLinkRouter as any)._setPending = setPending;
    return () => { cancelled = true; sub.remove(); };
  }, []);

  // Conferma cambio email: chiamata diretta al backend, poi popup custom in tema.
  useEffect(() => {
    if (pending?.kind !== 'confirm-email') return;
    const token = pending.token;
    setPending(null);
    (async () => {
      try {
        const r = await AuthApi.confirmEmailChange(token);
        setConfirmingEmail({ status: 'ok', email: r.email });
      } catch (e: any) {
        setConfirmingEmail({ status: 'error', error: e?.response?.data?.error ?? 'Conferma non riuscita.' });
      }
    })();
  }, [pending]);

  const dismiss = useCallback(() => setPending(null), []);

  if (pending?.kind === 'reset') {
    return <ResetPasswordScreen email={pending.email} token={pending.token} onDone={dismiss} />;
  }
  if (pending?.kind === 'register-external') {
    return <RegisterExternalScreen token={pending.token} onDone={dismiss} />;
  }
  return (
    <>
      {children}
      <ConfirmModal
        visible={!!confirmingEmail}
        title={confirmingEmail?.status === 'ok' ? 'Email confermata!' : 'Conferma non riuscita'}
        message={
          confirmingEmail?.status === 'ok'
            ? `Il tuo indirizzo email ufficiale è ora ${confirmingEmail.email}. La notifica nella campanella sparirà al prossimo aggiornamento.`
            : confirmingEmail?.error
        }
        tone={confirmingEmail?.status === 'ok' ? 'success' : 'danger'}
        icon={confirmingEmail?.status === 'ok' ? 'mail-open-outline' : 'alert-circle-outline'}
        onClose={() => setConfirmingEmail(null)}
      />
    </>
  );
};

/** Permette a una schermata di "iniettare" manualmente un link (es. utente incolla URL). */
export function openDeepLink(url: string) {
  const m = matchLink(url);
  if (m && (DeepLinkRouter as any)._setPending) {
    (DeepLinkRouter as any)._setPending(m);
  }
}
