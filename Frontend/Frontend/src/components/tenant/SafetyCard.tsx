import { useEffect, useState } from 'react';
import {
  getTrustedContact, saveTrustedContact, safetyCheckIn, sendEmergencyAlert,
  type TrustedContact,
} from '../../api/safety';
import { getNotificationPrefs, updateNotificationPrefs, type NotificationPrefs } from '../../api/settings';
import { ApiError } from '../../api/client';
import Card from '../ui/Card';
import Toggle from '../ui/Toggle';
import Checkbox from '../ui/Checkbox';
import Button from '../ui/Button';
import { useT } from '../../lib/i18n';
import { BellIcon, ShieldIcon } from './icons';

const INPUT =
  'w-full rounded-lg border border-gray-200 px-3 py-2 text-sm text-ink outline-none focus:border-brand';

/**
 * "Safety First" dashboard card, wired to the real safety module: the SMS
 * toggle drives the account's notification preferences, the trusted contact
 * persists via api/safety, and check-in / emergency actions notify that
 * contact by SMS + email server-side.
 */
export default function SafetyCard({ bookingId }: {
  /** Booking the check-in/alert is for; empty → actions explain they need a stay. */
  bookingId: string;
}) {
  const t = useT();
  const [prefs, setPrefs] = useState<NotificationPrefs | null>(null);
  const [contact, setContact] = useState<TrustedContact | null>(null);
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({ name: '', phone: '', email: '' });
  const [shareLocation, setShareLocation] = useState(false);
  const [busy, setBusy] = useState<'save' | 'checkin' | 'alert' | null>(null);
  const [armed, setArmed] = useState(false); // emergency needs a second tap
  const [notice, setNotice] = useState<{ tone: 'ok' | 'bad'; text: string } | null>(null);

  useEffect(() => {
    let active = true;
    getNotificationPrefs().then((p) => { if (active) setPrefs(p); }).catch(() => {});
    getTrustedContact()
      .then((c) => {
        if (!active) return;
        setContact(c);
        setForm({ name: c.name ?? '', phone: c.phone ?? '', email: c.email ?? '' });
      })
      .catch(() => {});
    return () => { active = false; };
  }, []);

  // Auto-dismiss transient notices; disarm the emergency button after a beat.
  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), 4000);
    return () => clearTimeout(t);
  }, [notice]);
  useEffect(() => {
    if (!armed) return;
    const t = setTimeout(() => setArmed(false), 5000);
    return () => clearTimeout(t);
  }, [armed]);

  const toggleSms = async (on: boolean) => {
    if (!prefs) return;
    const next = { ...prefs, smsEnabled: on };
    setPrefs(next); // optimistic
    try {
      await updateNotificationPrefs({ smsEnabled: next.smsEnabled, emailEnabled: next.emailEnabled });
    } catch {
      setPrefs(prefs);
      setNotice({ tone: 'bad', text: 'Could not update your SMS preference.' });
    }
  };

  const saveContact = async () => {
    setBusy('save');
    setNotice(null);
    try {
      const saved = await saveTrustedContact({
        name: form.name.trim() || undefined,
        phone: form.phone.trim() || undefined,
        email: form.email.trim() || undefined,
      });
      setContact(saved);
      setEditing(false);
      setNotice({ tone: 'ok', text: 'Trusted contact saved.' });
    } catch (e) {
      setNotice({ tone: 'bad', text: e instanceof ApiError ? e.message : 'Could not save the contact.' });
    } finally {
      setBusy(null);
    }
  };

  const locate = () =>
    new Promise<{ latitude?: number; longitude?: number }>((resolve) => {
      if (!shareLocation || !navigator.geolocation) return resolve({});
      navigator.geolocation.getCurrentPosition(
        (pos) => resolve({ latitude: pos.coords.latitude, longitude: pos.coords.longitude }),
        () => resolve({}), // denied/unavailable — check in without coordinates
        { timeout: 5000 },
      );
    });

  const checkIn = async () => {
    setBusy('checkin');
    setNotice(null);
    try {
      const coords = await locate();
      const result = await safetyCheckIn({ bookingId, shareLocation, ...coords });
      setNotice({
        tone: 'ok',
        text: result.contactNotified
          ? 'Checked in — your contact has been notified.'
          : 'Checked in. Add a reachable contact to notify someone next time.',
      });
    } catch (e) {
      setNotice({ tone: 'bad', text: e instanceof ApiError ? e.message : 'Check-in failed. Please try again.' });
    } finally {
      setBusy(null);
    }
  };

  const emergency = async () => {
    if (!armed) { setArmed(true); return; }
    setArmed(false);
    setBusy('alert');
    setNotice(null);
    try {
      await sendEmergencyAlert(bookingId);
      setNotice({ tone: 'ok', text: 'Emergency alert sent to you and your trusted contact.' });
    } catch (e) {
      setNotice({ tone: 'bad', text: e instanceof ApiError ? e.message : 'Could not send the alert.' });
    } finally {
      setBusy(null);
    }
  };

  const hasContact = Boolean(contact?.phone || contact?.email);

  return (
    <Card className="p-6">
      <h2 className="flex items-center gap-2 text-lg font-bold text-ink">
        <ShieldIcon size={18} className="text-brand" /> {t('Safety First')}
      </h2>

      <div className="mt-3 flex items-center justify-between">
        <span className="flex items-center gap-2 text-sm text-ink">
          <BellIcon size={16} className="text-brand" /> {t('SMS notifications')}
        </span>
        <Toggle on={prefs?.smsEnabled ?? false} onChange={toggleSms} />
      </div>

      <div className="mt-3 border-t border-gray-100 pt-3">
        <div className="flex items-center justify-between">
          <p className="text-xs text-muted">{t('Trusted contact')}</p>
          <button
            type="button"
            onClick={() => setEditing((v) => !v)}
            className="text-xs font-semibold text-brand"
          >
            {editing ? t('Cancel') : hasContact ? t('Edit') : t('Add')}
          </button>
        </div>
        {editing ? (
          <div className="mt-2 space-y-2">
            <input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} placeholder="Name" className={INPUT} />
            <input value={form.phone} onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))} placeholder="Phone (+233 …)" className={INPUT} />
            <input value={form.email} onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))} placeholder="Email (optional)" className={INPUT} />
            <Button size="sm" disabled={busy === 'save'} onClick={saveContact}>
              {busy === 'save' ? 'Saving…' : t('Save contact')}
            </Button>
          </div>
        ) : hasContact ? (
          <div className="mt-1">
            <p className="text-sm font-semibold text-ink">{contact?.name || 'Unnamed contact'}</p>
            <p className="text-sm text-muted">{contact?.phone ?? contact?.email}</p>
          </div>
        ) : (
          <p className="mt-1 text-sm text-muted">
            No contact yet — add someone we can text when you arrive safely.
          </p>
        )}
      </div>

      <div className="mt-3 space-y-2 border-t border-gray-100 pt-3">
        <label className="flex items-center gap-2 text-xs text-muted">
          <Checkbox checked={shareLocation} onChange={setShareLocation} />
          {t('Share my location with the check-in')}
        </label>
        {bookingId ? (
          <div className="flex flex-wrap gap-2">
            <Button size="sm" disabled={busy !== null} onClick={checkIn}>
              {busy === 'checkin' ? 'Checking in…' : t("I've arrived safely")}
            </Button>
            <Button
              size="sm"
              variant="ghost"
              className={armed ? 'bg-rose-600! text-white!' : 'text-rose-600 hover:bg-rose-50'}
              disabled={busy !== null}
              onClick={emergency}
            >
              {busy === 'alert' ? 'Sending…' : armed ? t('Tap again to confirm') : t('Emergency alert')}
            </Button>
          </div>
        ) : (
          <p className="text-xs text-muted">
            Check-in and emergency alerts unlock when you have an upcoming stay.
          </p>
        )}
      </div>

      {notice && (
        <p className={`mt-3 text-xs font-medium ${notice.tone === 'ok' ? 'text-brand' : 'text-rose-600'}`} role="status">
          {notice.text}
        </p>
      )}
    </Card>
  );
}
