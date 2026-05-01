import apiClient, { API_BASE_URL } from './apiClient';

/** Risolve un avatar URL: se è relativo ("/uploads/...") lo prefissa con il base URL. */
export const resolveAvatarUrl = (url?: string | null): string | null => {
  if (!url) return null;
  if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) return url;
  if (url.startsWith('/')) return `${API_BASE_URL}${url}`;
  return url;
};

// ---------- Shift / Calendar ----------

/** Codice del turno secondo la classificazione storica del calendario di Navelli. */
export type ShiftCode = 'F' | 'FN' | 'P' | 'PN' | 'N';

/** Numerico per allineamento col backend (System.Text.Json serializza enum come int). */
export const ShiftStatus = {
  Assigned: 1,
  OnBoard: 2,
  Completed: 3,
  Cancelled: 4,
} as const;
export type ShiftStatusValue = (typeof ShiftStatus)[keyof typeof ShiftStatus];

export type MedicoRefDto = {
  id: string;
  fullName: string;
  avatarUrl: string | null;
  number: number | null;
};

export type ShiftDto = {
  id: string;
  date: string;          // yyyy-MM-dd
  code: ShiftCode;
  codeLabel: string;     // "Festivo Diurno", ecc.
  startUtc: string;
  endUtc: string;
  startLocal: string;    // "08:00"
  endLocal: string;      // "20:00"
  status: ShiftStatusValue;
  medicoTurno: MedicoRefDto | null;
  medicoReperibile: MedicoRefDto | null;
  isMineTurno: boolean;
  isMineReperibile: boolean;
  isPast: boolean;
};

export type DayDto = {
  date: string;          // yyyy-MM-dd
  shifts: ShiftDto[];
};

// ---------- Swaps ----------

export const SwapStatus = {
  Pending: 1,
  AutoApproved: 2,
  Rejected: 3,
  Cancelled: 4,
  BlockedByRules: 5,
} as const;
export type SwapStatusValue = (typeof SwapStatus)[keyof typeof SwapStatus];

export const SwapType = {
  Giveaway: 1,
  Swap: 2,
  PickFromBoard: 3,
} as const;
export type SwapTypeValue = (typeof SwapType)[keyof typeof SwapType];

export type ShiftBriefDto = {
  id: string;
  date: string;
  code: ShiftCode;
  startUtc: string;
  endUtc: string;
};

export type SwapDto = {
  id: string;
  type: SwapTypeValue;
  status: SwapStatusValue;
  initiatorId: string;
  initiatorName: string;
  initiatorShift: ShiftBriefDto;
  counterpartId: string | null;
  counterpartName: string | null;
  counterpartShift: ShiftBriefDto | null;
  message: string | null;
  resolutionReason: string | null;
  createdAtUtc: string;
  resolvedAtUtc: string | null;
};

// ---------- Users ----------

export type MedicoDto = {
  id: string;
  fullName: string;
  email: string;
  avatarUrl: string | null;
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

// ---------- API ----------

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
      from: from ? from.toISOString().slice(0, 10) : undefined,
      to:   to   ? to.toISOString().slice(0, 10)   : undefined,
    },
  }).then(r => r.data),
};

export const BoardApi = {
  list: () => apiClient.get<ShiftDto[]>('/api/board').then(r => r.data),
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
  history:  () => apiClient.get<SwapDto[]>('/api/swaps/history').then(r => r.data),
  giveaway: (shiftId: string, toMedicoId: string, message?: string) =>
    apiClient.post<SwapDto>('/api/swaps/giveaway', { shiftId, toMedicoId, message }).then(r => r.data),
  swap: (myShiftId: string, otherShiftId: string, message?: string) =>
    apiClient.post<SwapDto>('/api/swaps/swap', { myShiftId, otherShiftId, message }).then(r => r.data),
  pick: (shiftId: string) =>
    apiClient.post<SwapDto>(`/api/swaps/pick/${shiftId}`).then(r => r.data),
  accept: (id: string, force = false) =>
    apiClient.post<SwapDto>(`/api/swaps/${id}/accept${force ? '?force=true' : ''}`).then(r => r.data),
  reject: (id: string, reason?: string) =>
    apiClient.post<SwapDto>(`/api/swaps/${id}/reject`, { reason }).then(r => r.data),
  cancel: (id: string) => apiClient.post<SwapDto>(`/api/swaps/${id}/cancel`).then(r => r.data),
};

// ---------- Helpers ----------

/** Etichetta breve del codice ("Festivo D.", "Notte", ...). */
export const shiftCodeShort = (c: ShiftCode): string => {
  switch (c) {
    case 'F':  return 'Festivo';
    case 'FN': return 'Festivo notte';
    case 'P':  return 'Prefestivo';
    case 'PN': return 'Prefest. notte';
    case 'N':  return 'Notte';
  }
};

/** Icona Ionicons per il codice. */
export const shiftCodeIcon = (c: ShiftCode): string => {
  switch (c) {
    case 'F':
    case 'P':  return 'sunny-outline';
    case 'FN':
    case 'PN':
    case 'N':  return 'moon-outline';
  }
};

/** Tonalità badge per il codice. */
export const shiftCodeTone = (c: ShiftCode): 'success' | 'warning' | 'info' | 'neutral' | 'danger' => {
  switch (c) {
    case 'F':
    case 'FN': return 'danger';      // festivi → rosso
    case 'P':
    case 'PN': return 'warning';     // prefestivi → arancio
    case 'N':  return 'info';        // infrasettimanale → blu
  }
};

export const swapStatusLabel = (s: SwapStatusValue): string => {
  switch (s) {
    case SwapStatus.Pending:        return 'In attesa';
    case SwapStatus.AutoApproved:   return 'Approvato';
    case SwapStatus.Rejected:       return 'Rifiutato';
    case SwapStatus.Cancelled:      return 'Annullato';
    case SwapStatus.BlockedByRules: return 'Bloccato';
    default: return String(s);
  }
};

export const swapStatusTone = (s: SwapStatusValue): 'success' | 'warning' | 'info' | 'neutral' | 'danger' => {
  switch (s) {
    case SwapStatus.AutoApproved:   return 'success';
    case SwapStatus.Rejected:       return 'danger';
    case SwapStatus.BlockedByRules: return 'danger';
    case SwapStatus.Cancelled:      return 'neutral';
    case SwapStatus.Pending:
    default: return 'warning';
  }
};

export const swapTypeLabel = (t: SwapTypeValue): string => {
  switch (t) {
    case SwapType.Giveaway:      return 'Cessione';
    case SwapType.Swap:          return 'Scambio';
    case SwapType.PickFromBoard: return 'Da bacheca';
    default: return String(t);
  }
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
  // accetta sia "yyyy-MM-dd" che ISO completo
  const d = iso.length === 10 ? new Date(`${iso}T00:00:00`) : new Date(iso);
  return d.toLocaleDateString(locale, {
    weekday: 'long', day: '2-digit', month: 'long',
  });
};
