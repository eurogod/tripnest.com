import { useQuery } from '@tanstack/react-query';
import { receiptsApi } from '@/lib/services';
import { api } from '@/lib/api';
import { PageHeader, Async } from '@/components/dashboard';
import { Button } from '@/components/ui';
import { Cash } from '@/components/icons';
import { money, fmtDate } from '@/lib/format';
import { useAuth } from '@/auth/AuthContext';
import { useToast } from '@/components/Toast';
import type { Receipt } from '@/types/api';

export default function ReceiptsPage() {
  const { user } = useAuth();
  const toast = useToast();
  const query = useQuery({ queryKey: ['receipts'], queryFn: () => receiptsApi.mine(1, 50), enabled: !!user });

  async function download(r: Receipt) {
    try {
      const res = await api.blob(receiptsApi.downloadUrl(r.receiptId));
      const url = URL.createObjectURL(res.data as Blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `receipt-${r.receiptId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      toast.error('Download not available yet');
    }
  }

  return (
    <div>
      <PageHeader title="Receipts" subtitle="Payment receipts for your bookings and services." />
      <Async
        query={query}
        isEmpty={(d) => d.items.length === 0}
        emptyIcon={<Cash className="h-6 w-6" />}
        emptyTitle="No receipts yet"
        emptySubtitle="Receipts are generated automatically after each payment."
      >
        {(data) => (
          <div className="overflow-hidden rounded-xl border border-line bg-white">
            <table className="w-full text-sm">
              <thead className="bg-surface text-left text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th className="px-4 py-3 font-semibold">Date</th>
                  <th className="px-4 py-3 font-semibold">Description</th>
                  <th className="px-4 py-3 font-semibold">Method</th>
                  <th className="px-4 py-3 text-right font-semibold">Amount</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {data.items.map((r) => (
                  <tr key={r.receiptId}>
                    <td className="px-4 py-3 text-muted">{fmtDate(r.createdAt)}</td>
                    <td className="px-4 py-3 font-medium">{r.description ?? 'Payment'}</td>
                    <td className="px-4 py-3 text-muted">{r.paymentMethod ?? '—'}</td>
                    <td className="px-4 py-3 text-right font-bold">{money(r.amount)}</td>
                    <td className="px-4 py-3 text-right">
                      <Button variant="ghost" size="sm" onClick={() => download(r)}>
                        PDF
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Async>
    </div>
  );
}
