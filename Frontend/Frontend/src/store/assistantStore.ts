import { useSyncExternalStore } from 'react';

// ---------------------------------------------------------------------------
// Open/closed state for the AI assistant slide-over, shared between the
// floating launcher and the Help pages so any surface can open the same panel.
// ---------------------------------------------------------------------------

const listeners = new Set<() => void>();
let open = false;

function emit() {
  listeners.forEach((l) => l());
}

export function openAssistant(): void {
  if (open) return;
  open = true;
  emit();
}

export function closeAssistant(): void {
  if (!open) return;
  open = false;
  emit();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

/** Subscribe a component to the assistant panel's open state. */
export function useAssistantOpen(): boolean {
  return useSyncExternalStore(subscribe, () => open, () => false);
}
