import type { Role } from '../store/authStore';

/** Where each role lands after auth / onboarding. */
export const homeForRole = (role: Role): string =>
  role === 'landlord' ? '/landlord'
    : role === 'agent' ? '/agent'
      : role === 'caretaker' ? '/caretaker'
        : role === 'admin' ? '/admin'
          : '/';

/** Where the top-bar avatar / account shortcuts should take each role. */
export const profilePathForRole = (role: Role): string =>
  role === 'landlord' ? '/landlord/settings'
    : role === 'agent' ? '/agent/profile'
      : role === 'caretaker' ? '/caretaker'
        : role === 'admin' ? '/admin'
          : '/profile';

/** Each role's own Messages surface (the chat backend is shared). */
export const messagesPathForRole = (role: Role): string =>
  role === 'landlord' ? '/landlord/messages'
    : role === 'agent' ? '/agent/messages'
      : role === 'caretaker' ? '/caretaker/messages'
        : role === 'admin' ? '/admin/messages'
          : '/messages';
