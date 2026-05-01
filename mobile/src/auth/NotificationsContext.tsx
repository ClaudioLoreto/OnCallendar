import React, {
  createContext, useCallback, useContext, useEffect, useRef, useState,
} from 'react';
import { NotificationDto, NotificationsApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';

type NotificationsCtx = {
  unreadCount: number;
  notifications: NotificationDto[];
  loading: boolean;
  refresh: () => Promise<void>;
  markAllRead: () => Promise<void>;
};

const Ctx = createContext<NotificationsCtx>({
  unreadCount: 0,
  notifications: [],
  loading: false,
  refresh: async () => {},
  markAllRead: async () => {},
});

export const useNotifications = () => useContext(Ctx);

const POLL_MS = 30_000;

export const NotificationsProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { user } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [loading, setLoading] = useState(false);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);

  const fetchCount = useCallback(async () => {
    try {
      const n = await NotificationsApi.unreadCount();
      setUnreadCount(n);
    } catch {
      // silenzioso
    }
  }, []);

  const refresh = useCallback(async () => {
    if (!user) return;
    setLoading(true);
    try {
      const list = await NotificationsApi.list();
      setNotifications(list);
      setUnreadCount(list.filter(n => !n.isRead).length);
    } catch {
      // silenzioso
    } finally {
      setLoading(false);
    }
  }, [user]);

  const markAllRead = useCallback(async () => {
    await NotificationsApi.markAllRead();
    setUnreadCount(0);
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
  }, []);

  useEffect(() => {
    if (!user) {
      setUnreadCount(0);
      setNotifications([]);
      if (timer.current) clearInterval(timer.current);
      return;
    }
    fetchCount();
    timer.current = setInterval(fetchCount, POLL_MS);
    return () => { if (timer.current) clearInterval(timer.current); };
  }, [user, fetchCount]);

  return (
    <Ctx.Provider value={{ unreadCount, notifications, loading, refresh, markAllRead }}>
      {children}
    </Ctx.Provider>
  );
};
