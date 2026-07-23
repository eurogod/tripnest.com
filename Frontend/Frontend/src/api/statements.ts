import type { Statement } from '../types';
import { apiGetList } from './client';
import { mapStatement, type StatementResponseDto } from './backend';

// Monthly payout statements, computed server-side from completed bookings.

export async function getStatements(): Promise<Statement[]> {
  const dtos = await apiGetList<StatementResponseDto>('/api/statements');
  return dtos.map(mapStatement);
}
