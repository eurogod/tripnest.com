import { useEffect, useState } from 'react';
import {
  deleteRoommateProfile, getMatchExplanation, getMyRoommateProfile, getRoommateMatches,
  upsertRoommateProfile, type MatchExplanation, type RoommateMatch, type RoommateProfileInput,
} from '../../api/roommates';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Badge from '../../components/ui/Badge';
import Checkbox from '../../components/ui/Checkbox';
import { formatCedi } from '../../lib/format';
import { useT } from '../../lib/i18n';

const EMPTY: RoommateProfileInput = {
  bio: '', university: '', preferredLocation: '', monthlyBudget: 500, moveInDate: undefined,
  smokes: false, okWithSmoker: false, hasPets: false, okWithPets: true,
  nightOwl: false, cleanlinessLevel: 3, isVisible: true,
};

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex items-center gap-2 text-sm text-ink">
      <Checkbox checked={checked} onChange={onChange} />
      {label}
    </label>
  );
}

/**
 * Roommate matching: publish a preferences profile, browse compatibility-scored matches
 * (hard conflicts are filtered out server-side), and read the AI take on why a match works.
 */
export default function RoommatesPage() {
  const t = useT();
  const [form, setForm] = useState<RoommateProfileInput>(EMPTY);
  const [hasProfile, setHasProfile] = useState(false);
  const [matches, setMatches] = useState<RoommateMatch[]>([]);
  const [explanations, setExplanations] = useState<Record<string, MatchExplanation>>({});
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState<string | null>(null);

  const set = <K extends keyof RoommateProfileInput>(k: K, v: RoommateProfileInput[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const loadMatches = () => getRoommateMatches().then(setMatches).catch(() => setMatches([]));

  useEffect(() => {
    getMyRoommateProfile()
      .then((p) => {
        setHasProfile(true);
        setForm({
          bio: p.bio ?? '', university: p.university ?? '', preferredLocation: p.preferredLocation,
          monthlyBudget: p.monthlyBudget, moveInDate: p.moveInDate?.slice(0, 10),
          smokes: p.smokes, okWithSmoker: p.okWithSmoker, hasPets: p.hasPets, okWithPets: p.okWithPets,
          nightOwl: p.nightOwl, cleanlinessLevel: p.cleanlinessLevel, isVisible: p.isVisible,
        });
        void loadMatches();
      })
      .catch(() => setHasProfile(false)); // 404 = no profile yet — show the empty form
  }, []);

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setNote(null);
    try {
      await upsertRoommateProfile({ ...form, bio: form.bio || undefined, university: form.university || undefined });
      setHasProfile(true);
      setNote('Profile saved — matches update as compatible people join.');
      void loadMatches();
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Could not save the profile.');
    } finally {
      setBusy(false);
    }
  };

  const unlist = async () => {
    setBusy(true);
    try {
      await deleteRoommateProfile();
      setHasProfile(false);
      setMatches([]);
      setForm(EMPTY);
      setNote('Profile removed — you no longer appear in matches.');
    } catch (err) {
      setNote(err instanceof Error ? err.message : 'Could not remove the profile.');
    } finally {
      setBusy(false);
    }
  };

  const explain = async (userId: string) => {
    try {
      const ex = await getMatchExplanation(userId);
      setExplanations((x) => ({ ...x, [userId]: ex }));
    } catch {
      setNote('The AI explanation is unavailable right now.');
    }
  };

  return (
    <div>
      <h1 className="mb-2 text-3xl font-bold text-ink">{t('Find a roommate')}</h1>
      <p className="mb-6 text-sm text-muted">
        For students and long-term stays: share your preferences, see who's compatible. Matches
        show identity verification so you know who you're talking to.
      </p>

      <Card className="p-5">
        <h2 className="text-base font-bold text-ink">{hasProfile ? 'Your roommate profile' : 'Create your profile'}</h2>
        <form onSubmit={save} className="mt-3 space-y-3">
          <div className="grid gap-2 sm:grid-cols-2">
            <input value={form.preferredLocation} onChange={(e) => set('preferredLocation', e.target.value)}
              placeholder="Preferred area (e.g. East Legon)" required
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm" />
            <input type="number" min="1" value={form.monthlyBudget}
              onChange={(e) => set('monthlyBudget', Number(e.target.value))}
              placeholder="Monthly budget (GH₵)" required
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm" />
            <input value={form.university} onChange={(e) => set('university', e.target.value)}
              placeholder="University (optional)"
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm" />
            <input type="date" value={form.moveInDate ?? ''} onChange={(e) => set('moveInDate', e.target.value || undefined)}
              aria-label="Move-in date" className="rounded-lg border border-gray-300 px-3 py-2 text-sm" />
          </div>
          <textarea value={form.bio} onChange={(e) => set('bio', e.target.value)} rows={2}
            placeholder="A line about you (optional)"
            className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm" />
          <div className="grid gap-2 sm:grid-cols-3">
            <Toggle label="I smoke" checked={form.smokes} onChange={(v) => set('smokes', v)} />
            <Toggle label="OK with a smoker" checked={form.okWithSmoker} onChange={(v) => set('okWithSmoker', v)} />
            <Toggle label="I have pets" checked={form.hasPets} onChange={(v) => set('hasPets', v)} />
            <Toggle label="OK with pets" checked={form.okWithPets} onChange={(v) => set('okWithPets', v)} />
            <Toggle label="Night owl" checked={form.nightOwl} onChange={(v) => set('nightOwl', v)} />
            <Toggle label="Visible in matches" checked={form.isVisible} onChange={(v) => set('isVisible', v)} />
          </div>
          <label className="flex items-center gap-2 text-sm text-ink">
            Cleanliness
            <input type="range" min="1" max="5" value={form.cleanlinessLevel}
              onChange={(e) => set('cleanlinessLevel', Number(e.target.value))} />
            <span className="text-muted">{form.cleanlinessLevel}/5</span>
          </label>
          <div className="flex gap-2">
            <Button size="sm" disabled={busy}>{busy ? 'Saving…' : hasProfile ? 'Update profile' : 'Publish profile'}</Button>
            {hasProfile && (
              <Button size="sm" variant="ghost" disabled={busy} onClick={() => { void unlist(); }}>Remove profile</Button>
            )}
          </div>
        </form>
        {note && <p className="mt-2 text-sm text-muted">{note}</p>}
      </Card>

      {matches.length > 0 && (
        <section className="mt-6">
          <h2 className="mb-3 text-base font-bold text-ink">Your matches</h2>
          <div className="space-y-3">
            {matches.map((m) => (
              <Card key={m.profile.userId} className="p-4">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <p className="font-medium text-ink">{m.profile.fullName ?? 'TripNest member'}</p>
                    {m.profile.isVerified && <Badge tone="green">ID verified</Badge>}
                  </div>
                  <Badge tone="blue">{m.score}% match</Badge>
                </div>
                <p className="mt-1 text-sm text-muted">
                  {m.profile.preferredLocation} · {formatCedi(m.profile.monthlyBudget)}/mo
                  {m.profile.university && ` · ${m.profile.university}`}
                </p>
                {m.profile.bio && <p className="mt-1 text-sm text-muted">{m.profile.bio}</p>}
                {explanations[m.profile.userId] ? (
                  <div className="mt-2 rounded-lg bg-gray-50 p-3 text-sm">
                    <p className="text-ink">{explanations[m.profile.userId].explanation}</p>
                    {explanations[m.profile.userId].considerations.length > 0 && (
                      <ul className="mt-1 list-disc pl-5 text-muted">
                        {explanations[m.profile.userId].considerations.map((c) => <li key={c}>{c}</li>)}
                      </ul>
                    )}
                  </div>
                ) : (
                  <Button size="sm" variant="ghost" className="mt-2"
                    onClick={() => { void explain(m.profile.userId); }}>
                    Why this match?
                  </Button>
                )}
              </Card>
            ))}
          </div>
        </section>
      )}

      {hasProfile && matches.length === 0 && (
        <p className="mt-6 text-sm text-muted">No compatible matches yet — check back as more people join.</p>
      )}
    </div>
  );
}
