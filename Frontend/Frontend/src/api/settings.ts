import { apiGet, apiPut, apiDelete } from './client';

export interface NotificationPrefs {
  smsEnabled: boolean;
  emailEnabled: boolean;
}

export const getNotificationPrefs = () =>
  apiGet<NotificationPrefs & { userId: string }>('/api/settings/notifications');

export const updateNotificationPrefs = (prefs: NotificationPrefs) =>
  apiPut<NotificationPrefs & { userId: string }>('/api/settings/notifications', prefs);

export const changePassword = (input: {
  currentPassword: string; newPassword: string; confirmNewPassword: string;
}) => apiPut('/api/settings/password', input);

/** Deactivates the account server-side; sign out locally afterwards. */
export const deleteAccount = () => apiDelete('/api/settings/account');
