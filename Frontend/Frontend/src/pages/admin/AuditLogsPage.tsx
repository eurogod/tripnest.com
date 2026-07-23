import { getAuditLogs } from '../../api/adminWorkspace';
import { useAsync } from '../../hooks/useAsync';
import AsyncBoundary from '../../components/AsyncBoundary';
import Card from '../../components/ui/Card';
import { formatIsoDateFull } from '../../api/backend';

export default function AuditLogsPage() {
  const state = useAsync(() => getAuditLogs(100));

  return (
    <div>
      <h1 className="text-3xl font-bold tracking-tight text-ink">Audit logs</h1>
      <p className="mt-1 mb-6 text-sm text-muted">The last 100 recorded platform actions, newest first.</p>
      <AsyncBoundary
        state={state}
        errorMessage="Failed to load audit logs."
        emptyMessage="No audit entries recorded yet."
        isEmpty={(rows) => rows.length === 0}
      >
        {(rows) => (
          <Card className="overflow-x-auto p-0">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-gray-100 text-xs uppercase tracking-wide text-muted">
                  <th className="px-6 py-3 font-semibold">When</th>
                  <th className="px-6 py-3 font-semibold">Action</th>
                  <th className="px-6 py-3 font-semibold">Entity</th>
                  <th className="px-6 py-3 font-semibold">User</th>
                  <th className="px-6 py-3 font-semibold">IP</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((log) => (
                  <tr key={log.id} className="border-b border-gray-50 last:border-0">
                    <td className="whitespace-nowrap px-6 py-3 text-muted">{formatIsoDateFull(log.createdAt)}</td>
                    <td className="px-6 py-3 font-medium text-ink">{log.action}</td>
                    <td className="px-6 py-3 text-muted">{log.entityType} {log.entityId.slice(0, 8)}</td>
                    <td className="px-6 py-3 text-muted">{log.userId.slice(0, 8)}</td>
                    <td className="px-6 py-3 text-muted">{log.ipAddress ?? '—'}</td>
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
