import apiClient, { API_BASE_URL } from './apiClient';

/** Risolve un avatar URL: se è relativo ("/uploads/...") lo prefissa con il base URL. */
export const resolveAvatarUrl = (url?: string | null): string | null => {
  if (!url) return null;
  if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) return url;
  if (url.startsWith('/')) return `${API_BASE_URL}${url}`;
  return url;
};

export type ShiftStatus = 'Assigned' | 'OnBoard' | 'Completed' | 'Cancelled';

export type ShiftDto = {
  id: string;
  startUtc: string;
  endUtc: string;
  location: string | null;
  notes: string | null;
  status: ShiftStatus;
  assignedMedicoId: string | null;
  assignedMedicoName: string | null;
};

export type AssigneeDto = { medicoId: string; fullName: string; avatarUrl: string | null };
export type SlotDto = {
  shiftId: string;
  startUtc: string;
  endUtc: string;
  capacity: number;
  label: string;
  status: ShiftStatus;
  assignees: AssigneeDto[];
  isMine: boolean;
  hasFreeSpot: boolean;
};
export type DayDto = { dateUtc: string; slots: SlotDto[] };

export type BoardItemDto = {
  shiftId: string;
  startUtc: string;
  endUtc: string;
  location: string | null;
  notes: string | null;
  offeredByMedicoId: string;
  offeredByMedicoName: string;
};

export type MedicoDto = { id: string; fullName: string; email: string; avatarUrl: string | null };

export type SwapStatus =
  | 'Pending' | 'AutoApproved' | 'Rejected' | 'Cancelled' | 'BlockedByRules';
export type SwapType = 'Giveaway' | 'Swap' | 'PickFromBoard';

export type SwapDto = {
  id: string;
  type: SwapType;
  status: SwapStatus;
  initiatorId: string;
  initiatorName: string;
  initiatorShiftId: string;
  initiatorShiftStart: string;
  initiatorShiftEnd: string;
  counterpartId: string | null;
  counterpartName: string | null;
  counterpartShiftId: string | null;
  counterpartShiftStart: string | null;
  counterpartShiftEnd: string | null;
  message: string | null;
  resolutionReason: string | null;
  createdAtUtc: string;
  resolvedAtUtc: string | null;
};

export type MeDto = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phone: string | null;
  avatarUrl: string | null;
  preferredLanguage: string;
  themePreference: string;
  emailConfirmed: boolean;
  phoneConfirmed: boolean;
};

export const AuthApi = {
  forgotPassword: (emailOrPhone: string) =>
    apiClient.post('/api/auth/forgot-password', { emailOrPhone }).then(r => r.data),
  verifyOtp: (emailOrPhone: string, otp: string, newPassword: string) =>
    apiClient.post('/api/auth/verify-otp', { emailOrPhone, otp, newPassword }).then(r => r.data),
};

export const ShiftsApi = {
  mine: () => apiClient.get<ShiftDto[]>('/api/shifts/mine').then(r => r.data),
  all:  () => apiClient.get<ShiftDto[]>('/api/shifts').then(r => r.data),
  publish:   (id: string) => apiClient.post(`/api/shifts/${id}/publish-on-board`),
  unpublish: (id: string) => apiClient.post(`/api/shifts/${id}/unpublish`),
};

export const CalendarApi = {
  list: (from?: Date, to?: Date) => apiClient.get<DayDto[]>('/api/calendar', {
    params: {
      from: from?.toISOString(),
      to: to?.toISOString(),
    },
  }).then(r => r.data),
  join: (shiftIds: string[]) =>
    apiClient.post('/api/calendar/join', { shiftIds }).then(r => r.data),
};

export const BoardApi = {
  list: () => apiClient.get<BoardItemDto[]>('/api/board').then(r => r.data),
};

export const UsersApi = {
  medici: () => apiClient.get<MedicoDto[]>('/api/users/medici').then(r => r.data),
  me: () => apiClient.get<MeDto>('/api/users/me').then(r => r.data),
  updateMe: (patch: Partial<{
    firstName: string; lastName: string; email: string; phone: string;
    avatarUrl: string; preferredLanguage: string; themePreference: string;
  }>) => apiClient.patch<MeDto>('/api/users/me', patch).then(r => r.data),
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post('/api/users/me/change-password', { currentPassword, newPassword }),
  uploadAvatar: async (uri: string) => {
    const fd = new FormData();
    const ext = (uri.split('.').pop() || 'jpg').toLowerCase();
    const mime = ext === 'png' ? 'image/png' : ext === 'webp' ? 'image/webp' : 'image/jpeg';
    // @ts-expect-error - React Native FormData accetta { uri, name, type }
    fd.append('file', { uri, name: `avatar.${ext}`, type: mime });
    const r = await apiClient.post<MeDto>('/api/users/me/avatar', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
      transformRequest: (data) => data,
    });
    return r.data;
  },
};

export const SwapsApi = {
  incoming: () => apiClient.get<SwapDto[]>('/api/swaps/incoming').then(r => r.data),
  outgoing: () => apiClient.get<SwapDto[]>('/api/swaps/outgoing').then(r => r.data),
  giveaway: (shiftId: string, toMedicoId: string, message?: string) =>
    apiClient.post<SwapDto>('/api/swaps/giveaway', { shiftId, toMedicoId, message }).then(r => r.data),
  swap: (myShiftId: string, otherShiftId: string, message?: string) =>
    apiClient.post<SwapDto>('/api/swaps/swap', { myShiftId, otherShiftId, message }).then(r => r.data),
  pick: (shiftId: string) =>
    apiClient.post<SwapDto>(`/api/swaps/pick/${shiftId}`).then(r => r.data),
  accept: (id: string) => apiClient.post<SwapDto>(`/api/swaps/${id}/accept`).then(r => r.data),
  reject: (id: string, reason?: string) =>
    apiClient.post<SwapDto>(`/api/swaps/${id}/reject`, { reason }).then(r => r.data),
  cancel: (id: string) => apiClient.post<SwapDto>(`/api/swaps/${id}/cancel`).then(r => r.data),
};

export const formatRange = (startIso: string, endIso: string) => {
  const s = new Date(startIso);
  const e = new Date(endIso);
  const day = s.toLocaleDateString('it-IT', { weekday: 'short', day: '2-digit', month: 'short' });
  const sh = s.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
  const eh = e.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
  return `${day} • ${sh} → ${eh}`;
};

export const formatDayLong = (iso: string, locale = 'it-IT') => {
  const d = new Date(iso);
  return d.toLocaleDateString(locale, {
    weekday: 'long', day: '2-digit', month: 'long',
  });
};
