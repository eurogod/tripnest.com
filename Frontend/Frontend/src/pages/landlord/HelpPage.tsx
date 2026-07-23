import { useState } from 'react';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import { SparkleIcon, MailIcon, ChevronDownIcon } from '../../components/tenant/icons';
import { openAssistant } from '../../store/assistantStore';
import { useT } from '../../lib/i18n';

const FAQS = [
  { q: 'How do payouts work?', a: 'Earnings are released to your default payout method (MTN MoMo by default) on the 1st of each month, net of TripNest’s management fee. You can withdraw your available balance any time from the Earnings page.' },
  { q: 'How do I verify a listing?', a: 'Add your property under My Listings and submit it for review. Our team inspects the property and confirms ownership before it earns the green Verified badge and a TripNest ID.' },
  { q: 'What is Instant Book?', a: 'With Instant Book on, trusted guests can reserve without waiting for your approval. You can toggle it per account in Settings, and still review every booking afterwards.' },
  { q: 'How are disputes handled?', a: 'Report any issue from the booking or inquiry thread. Support mediates between you and the guest and can hold a payout while a dispute is open.' },
];

function Faq({ q, a }: { q: string; a: string }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="py-1">
      <button onClick={() => setOpen((o) => !o)} className="flex w-full items-center justify-between gap-4 py-3 text-left">
        <span className="font-medium text-ink">{q}</span>
        <ChevronDownIcon size={18} className={`shrink-0 text-muted transition-transform ${open ? 'rotate-180' : ''}`} />
      </button>
      {open && <p className="pb-3 text-sm text-muted">{a}</p>}
    </div>
  );
}

function ContactCard({ icon, label, value, href, onClick }: {
  icon: React.ReactNode; label: string; value: string; href?: string; onClick?: () => void;
}) {
  const inner = (
    <>
      <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-brand-50 text-brand">{icon}</span>
      <div className="text-left">
        <p className="text-sm text-muted">{label}</p>
        <p className="font-semibold text-ink">{value}</p>
      </div>
    </>
  );
  const cls = 'flex w-full items-center gap-3 p-4 no-underline transition-shadow hover:shadow-md';
  return (
    <Card className="overflow-hidden">
      {href ? <a href={href} className={cls}>{inner}</a> : <button type="button" onClick={onClick} className={cls}>{inner}</button>}
    </Card>
  );
}

export default function LandlordHelpPage() {
  const t = useT();
  return (
    <div className="max-w-3xl">
      <h1 className="mb-6 text-3xl font-bold tracking-tight text-ink">{t('Help & Support')}</h1>

      <div className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-2">
        <ContactCard icon={<SparkleIcon size={18} />} label={t('AI assistant')} value={t('Ask TripNest')} onClick={openAssistant} />
        <ContactCard icon={<MailIcon size={18} />} label={t('Email')} value="hosts@tripnest.gh" href="mailto:hosts@tripnest.gh" />
      </div>

      <Card className="p-6">
        <h2 className="mb-2 text-lg font-bold text-ink">Host FAQs</h2>
        <div className="divide-y divide-gray-100">
          {FAQS.map((f) => <Faq key={f.q} q={f.q} a={f.a} />)}
        </div>
        <div className="mt-5 rounded-xl bg-brand-50 p-4">
          <p className="font-semibold text-ink">{t('Still need help?')}</p>
          <p className="mb-3 text-sm text-muted">Ask the assistant — it loops in our host team whenever a human is needed.</p>
          <Button variant="dark" size="sm" onClick={openAssistant}>Contact host support</Button>
        </div>
      </Card>
    </div>
  );
}
