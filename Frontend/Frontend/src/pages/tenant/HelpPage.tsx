import { useState } from 'react';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import { SparkleIcon, MailIcon, ChevronDownIcon } from '../../components/tenant/icons';
import { openAssistant } from '../../store/assistantStore';
import { useT } from '../../lib/i18n';

const FAQS = [
  { q: 'How do I know a listing is verified?', a: 'Every verified listing carries a TripNest ID and a green Verified badge after our team inspects the property and confirms ownership.' },
  { q: 'How do payments work?', a: 'Rent and fees are paid securely through Mobile Money (MTN, Vodafone Cash, AirtelTigo). Payments are only released to landlords after check-in.' },
  { q: 'What are SMS safety alerts?', a: 'We send SMS alerts to your registered emergency contact for key safety events, so someone always knows where you are.' },
  { q: 'Can I cancel a booking?', a: 'Yes. Cancellation terms depend on the listing. You can review the policy on each property before you book.' },
];

function Faq({ q, a }: { q: string; a: string }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="py-1">
      <button
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between gap-4 py-3 text-left"
      >
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
      {href ? (
        <a href={href} className={cls}>{inner}</a>
      ) : (
        <button type="button" onClick={onClick} className={cls}>{inner}</button>
      )}
    </Card>
  );
}

export default function HelpPage() {
  const t = useT();
  return (
    <div className="max-w-3xl">
      <h1 className="mb-6 text-3xl font-bold text-ink">{t('Help & Support')}</h1>

      <div className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-2">
        <ContactCard icon={<SparkleIcon size={18} />} label={t('AI assistant')} value={t('Ask TripNest')} onClick={openAssistant} />
        <ContactCard icon={<MailIcon size={18} />} label={t('Email')} value="help@tripnest.gh" href="mailto:help@tripnest.gh" />
      </div>

      <Card className="p-6">
        <h2 className="mb-2 text-lg font-bold text-ink">{t('Frequently asked questions')}</h2>
        <div className="divide-y divide-gray-100">
          {FAQS.map((f) => (
            <Faq key={f.q} q={f.q} a={f.a} />
          ))}
        </div>
        <div className="mt-5 rounded-xl bg-brand-50 p-4">
          <p className="font-semibold text-ink">{t('Still need help?')}</p>
          <p className="mb-3 text-sm text-muted">Ask the assistant — it loops in our support team whenever a human is needed.</p>
          <Button size="sm" onClick={openAssistant}>{t('Contact support')}</Button>
        </div>
      </Card>
    </div>
  );
}
