import { useEffect, useState } from 'react';
import {
  convertToServiceRequest, getPropertyMaintenance, updateMaintenanceStatus,
  MAINTENANCE_STATUSES, type MaintenanceStatusName,
} from '../../api/maintenance';
import { getListings } from '../../api/listings';
import type { MaintenanceResponseDto } from '../../api/backend';
import Card from '../ui/Card';
import Button from '../ui/Button';
import Badge, { type BadgeTone } from '../ui/Badge';

const STATUS_LABEL: Record<number, { name: MaintenanceStatusName; tone: BadgeTone }> = {
  0: { name: 'Reported', tone: 'amber' },
  1: { name: 'Assigned', tone: 'blue' },
  2: { name: 'InProgress', tone: 'blue' },
  3: { name: 'Completed', tone: 'green' },
  4: { name: 'Cancelled', tone: 'gray' },
};

interface Row extends MaintenanceResponseDto {
  propertyTitle: string;
}

/**
 * The landlord's maintenance queue: tenant-reported issues across all their properties, with
 * status updates and one-click hand-off to a caretaker as a service request.
 */
export default function MaintenanceSection() {
  const [rows, setRows] = useState<Row[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const listings = await getListings();
        const perProperty = await Promise.all(
          listings.map(async (l) => {
            const dtos = await getPropertyMaintenance(l.id).catch(() => []);
            return dtos.map((d) => ({ ...d, propertyTitle: l.title }));
          }),
        );
        setRows(perProperty.flat().sort((a, b) => b.createdAt.localeCompare(a.createdAt)));
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Could not load maintenance requests.');
      }
    })();
  }, []);

  const setStatus = async (id: string, status: MaintenanceStatusName) => {
    setBusyId(id);
    try {
      await updateMaintenanceStatus(id, status);
      const statusInt = MAINTENANCE_STATUSES.indexOf(status);
      setRows((rs) => rs?.map((r) => (r.maintenanceId === id ? { ...r, status: statusInt } : r)) ?? null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not update the status.');
    } finally {
      setBusyId(null);
    }
  };

  const handOff = async (id: string) => {
    setBusyId(id);
    try {
      await convertToServiceRequest(id);
      setRows((rs) => rs?.map((r) => (r.maintenanceId === id ? { ...r, status: 1 } : r)) ?? null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not hand this off to a caretaker.');
    } finally {
      setBusyId(null);
    }
  };

  if (rows === null && !error) return null; // loading quietly under the tasks list
  if (error) return <p className="mt-6 text-sm text-red-600">{error}</p>;
  if (rows!.length === 0) return null; // nothing reported — keep the page clean

  return (
    <section className="mt-10">
      <h2 className="mb-4 text-2xl font-bold text-ink">Maintenance requests</h2>
      <div className="space-y-3">
        {rows!.map((r) => {
          const st = STATUS_LABEL[r.status] ?? STATUS_LABEL[0];
          return (
            <Card key={r.maintenanceId} className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center">
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <p className="truncate font-medium text-ink">{r.propertyTitle}</p>
                  <Badge tone={st.tone}>{st.name}</Badge>
                </div>
                <p className="mt-1 text-sm text-muted">{r.description}</p>
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <select
                  value={st.name}
                  disabled={busyId === r.maintenanceId}
                  onChange={(e) => { void setStatus(r.maintenanceId, e.target.value as MaintenanceStatusName); }}
                  aria-label="Update status"
                  className="rounded-lg border border-gray-300 px-2 py-1.5 text-sm"
                >
                  {MAINTENANCE_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
                </select>
                {r.status === 0 && (
                  <Button size="sm" variant="ghost" disabled={busyId === r.maintenanceId}
                    onClick={() => { void handOff(r.maintenanceId); }}>
                    Send to caretaker
                  </Button>
                )}
              </div>
            </Card>
          );
        })}
      </div>
    </section>
  );
}
