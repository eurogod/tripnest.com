import type { Statement, StatementStatus } from '../types';
import { getStatements } from '../api/statements';
import { useAsync } from '../hooks/useAsync';
import AsyncBoundary from '../components/AsyncBoundary';
import Card from '../components/ui/Card';
import Button from '../components/ui/Button';
import Badge, { type BadgeTone } from '../components/ui/Badge';
import { formatCurrency } from '../lib/format';

const STATUS_TONE: Record<StatementStatus, BadgeTone> = {
  paid: 'green',
  pending: 'amber',
};

//  Render a statement as plain text and trigger a browser download.
function downloadStatement(s: Statement) {
  const body = [
    'TRIPNEST HOST STATEMENT',
    '=======================',
    `Month:           ${s.month}`,
    `Period:          ${s.period}`,
    `Gross revenue:   ${formatCurrency(s.grossRevenue)}`,
    `Management fee:  ${formatCurrency(s.managementFee)}`,
    `Net payout:      ${formatCurrency(s.netPayout)}`,
    `Status:          ${s.status === 'paid' ? 'Paid' : 'Pending'}`,
  ].join('\n');
  const url = URL.createObjectURL(new Blob([body], { type: 'text/plain' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = `statement-${s.month.replace(/\s+/g, '-').toLowerCase()}.txt`;
  link.click();
  URL.revokeObjectURL(url);
}

export default function StatementsPage() {
  const state = useAsync(getStatements, []);

  return (
    <div>
      <h1 className="mb-8 text-4xl font-bold text-ink">Statements</h1>

      <AsyncBoundary
        state={state}
        loadingMessage="Loading statements…"
        errorMessage="Failed to load statements."
        emptyMessage="No statements yet."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => (
          <Card className="overflow-x-auto">
            <table className="w-full min-w-[720px] text-left">
              <thead>
                <tr className="border-b border-gray-100 text-xs font-semibold uppercase tracking-wide text-muted">
                  <th className="px-6 py-3 font-semibold">Month</th>
                  <th className="px-6 py-3 font-semibold">Period</th>
                  <th className="px-6 py-3 font-semibold">Gross revenue</th>
                  <th className="px-6 py-3 font-semibold">Management fee</th>
                  <th className="px-6 py-3 font-semibold">Net payout</th>
                  <th className="px-6 py-3 font-semibold">Status</th>
                  <th className="px-6 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {rows.map((s) => (
                  <tr key={s.id}>
                    <td className="px-6 py-4 font-semibold text-ink">{s.month}</td>
                    <td className="px-6 py-4 text-muted">{s.period}</td>
                    <td className="px-6 py-4 text-ink">{formatCurrency(s.grossRevenue)}</td>
                    <td className="px-6 py-4 text-ink">{formatCurrency(s.managementFee)}</td>
                    <td className="px-6 py-4 font-semibold text-ink">{formatCurrency(s.netPayout)}</td>
                    <td className="px-6 py-4">
                      <Badge tone={STATUS_TONE[s.status]}>
                        {s.status === 'paid' ? 'Paid' : 'Pending'}
                      </Badge>
                    </td>
                    <td className="px-6 py-4 text-right">
                      <Button variant="ghost" size="sm" onClick={() => downloadStatement(s)}>Download</Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        )}
      </AsyncBoundary>
    </div>
  );
}
