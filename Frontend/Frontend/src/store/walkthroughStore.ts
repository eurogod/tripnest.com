import { useSyncExternalStore } from 'react';

// Live walkthrough-generation progress, observable from React (same module
// state + listeners + useSyncExternalStore idiom as authStore). In-memory
// only: durable results live in IndexedDB (clips) and the backend (uploads);
// this store just narrates the run currently in flight.

export type SlotStatus = 'queued' | 'generating' | 'ready' | 'failed';

export interface SlotState {
  roomIndex: number;
  status: SlotStatus;
  error?: string;
  uploaded?: boolean; // persisted to the backend walkthrough endpoint
  uploadError?: string;
}

export interface GenerationState {
  running: boolean;
  slots: Record<number, SlotState>;
}

const states = new Map<string, GenerationState>();
const listeners = new Set<() => void>();

function emit(): void {
  listeners.forEach((l) => l());
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

export function getGeneration(propertyId: string): GenerationState | undefined {
  return states.get(propertyId);
}

export function useGeneration(propertyId: string): GenerationState | undefined {
  return useSyncExternalStore(subscribe, () => states.get(propertyId), () => undefined);
}

export function isRunning(propertyId: string): boolean {
  return states.get(propertyId)?.running ?? false;
}

export function beginRun(propertyId: string, roomIndexes: number[]): void {
  const slots: Record<number, SlotState> = { ...states.get(propertyId)?.slots };
  for (const i of roomIndexes) slots[i] = { roomIndex: i, status: 'queued' };
  states.set(propertyId, { running: true, slots });
  emit();
}

export function patchSlot(propertyId: string, roomIndex: number, patch: Partial<SlotState>): void {
  const prev = states.get(propertyId);
  if (!prev) return;
  const slot = prev.slots[roomIndex] ?? { roomIndex, status: 'queued' as const };
  states.set(propertyId, {
    ...prev,
    slots: { ...prev.slots, [roomIndex]: { ...slot, ...patch } },
  });
  emit();
}

export function endRun(propertyId: string): void {
  const prev = states.get(propertyId);
  if (!prev) return;
  states.set(propertyId, { ...prev, running: false });
  emit();
}
