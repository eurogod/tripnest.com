import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { LandlordTenant, TenantStanding } from '../../types';
import { getLandlordTenants } from '../../api/landlord';
import { startConversation } from '../../api/messages';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import Button from '../../components/ui/Button';
import Avatar from '../../components/ui/Avatar';
import Badge, { type BadgeTone } from '../../components/ui/Badge';
import { formatCedi } from '../../lib/format';
import { ChatIcon, PhoneIcon } from '../../components/tenant/icons';

const STANDING: Record<TenantStanding, { tone: BadgeTone; label: string }> = {
  current: { tone: 'green', label: 'Up to date' },
  overdue: { tone: 'red', label: 'Rent overdue' },
  'ending-soon': { tone: 'amber', label: 'Lease ending' },
};

function exportCsv(tenants: LandlordTenant[]) {
  const header = 'Name,Property,Email,Phone,Since,Lease ends,Monthly rent,Standing';
  const lines = tenants.map((t) =>
    [t.name, t.property, t.email, t.phone, t.since, t.leaseEnd, t.monthlyRent, t.standing]
      .map((v) => `"${String(v).replace(/"/g, '""')}"`)
      .join(','),
  );
  const url = URL.createObjectURL(new Blob([[header, ...lines].join('\n')], { type: 'text/csv' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = 'tenants.csv';
  link.click();
  URL.revokeObjectURL(url);
}

function TenantsView({ tenants }: { tenants: LandlordTenant[] }) {
  const navigate = useNavigate();
  const [chatBusy, setChatBusy] = useState<string | null>(null);
  const overdue = tenants.filter((t) => t.standing === 'overdue').length;
  const monthly = tenants.reduce((s, t) => s + t.monthlyRent, 0);

  // Open (or reuse) a direct conversation with the tenant, then jump to Messages.
  const startChat = async (tenantId: string) => {
    setChatBusy(tenantId);
    try {
      const conversationId = await startConversation(tenantId);
      navigate(`/landlord/messages/${conversationId}`);
    } catch {
      navigate('/landlord/messages');
    } finally {
      setChatBusy(null);
    }
  };

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">Tenants</h1>
      <p className="mt-1 mb-6 text-sm text-muted">
        {tenants.length} active · {formatCedi(monthly)}/mo in rent
        {overdue > 0 && <span className="text-rose-600"> · {overdue} overdue</span>}
      </p>

      <Card className="overflow-x-auto">
        <table className="w-full min-w-[760px] text-left">
          <thead>
            <tr className="border-b border-gray-100 text-xs font-semibold uppercase tracking-wide text-muted">
              <th className="px-5 py-3 font-semibold">Tenant</th>
              <th className="px-5 py-3 font-semibold">Property</th>
              <th className="px-5 py-3 font-semibold">Rent</th>
              <th className="px-5 py-3 font-semibold">Lease</th>
              <th className="px-5 py-3 font-semibold">Standing</th>
              <th className="px-5 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {tenants.map((t) => (
              <tr key={t.id}>
                <td className="px-5 py-4">
                  <div className="flex items-center gap-3">
                    <Avatar name={t.name} />
                    <div className="min-w-0">
                      <p className="truncate font-semibold text-ink">{t.name}</p>
                      <p className="truncate text-xs text-muted">Since {t.since}</p>
                    </div>
                  </div>
                </td>
                <td className="px-5 py-4 text-sm text-ink">{t.property}</td>
                <td className="px-5 py-4 text-sm font-semibold text-ink">{formatCedi(t.monthlyRent)}<span className="text-xs font-normal text-muted">/mo</span></td>
                <td className="px-5 py-4 text-sm text-muted">ends {t.leaseEnd}</td>
                <td className="px-5 py-4"><Badge tone={STANDING[t.standing].tone}>{STANDING[t.standing].label}</Badge></td>
                <td className="px-5 py-4">
                  <div className="flex justify-end gap-1.5">
                    <button
                      type="button"
                      onClick={() => void startChat(t.id)}
                      disabled={chatBusy !== null}
                      aria-label={`Message ${t.name}`}
                      className="flex h-8 w-8 items-center justify-center rounded-full text-brand hover:bg-brand-50 disabled:opacity-50"
                    >
                      <ChatIcon size={16} />
                    </button>
                    <a href={`tel:${t.phone.replace(/\s/g, '')}`} aria-label={`Call ${t.name}`} className="flex h-8 w-8 items-center justify-center rounded-full text-muted hover:bg-gray-100 hover:text-ink">
                      <PhoneIcon size={16} />
                    </a>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      <div className="mt-4">
        <Button variant="ghost" size="sm" onClick={() => exportCsv(tenants)}>Export tenant list</Button>
      </div>
    </div>
  );
}

export default function TenantsPage() {
  const state = useAsync(getLandlordTenants, []);
  return (
    <AsyncBoundary state={state} loadingMessage="Loading tenants…" errorMessage="Failed to load tenants." emptyMessage="No tenants yet." isEmpty={(r) => r.length === 0}>
      {(rows) => <TenantsView tenants={rows} />}
    </AsyncBoundary>
  );
}
