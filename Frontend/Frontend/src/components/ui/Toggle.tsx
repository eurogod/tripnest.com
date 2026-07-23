interface ToggleProps {
  on: boolean;
  onChange: (on: boolean) => void;
}

/** Accessible on/off switch styled in the brand color. */
export default function Toggle({ on, onChange }: ToggleProps) {
  return (
    <button
      type="button"
      onClick={() => onChange(!on)}
      aria-pressed={on}
      className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${on ? 'bg-brand' : 'bg-gray-300'}`}
    >
      <span
        className={`absolute left-0 top-0.5 h-5 w-5 rounded-full bg-white transition-[translate] ${on ? 'translate-x-5' : 'translate-x-0.5'}`}
      />
    </button>
  );
}
