interface CheckboxProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  ariaLabel?: string;
}

/** Brand-styled checkbox with a proper check mark instead of the browser default. */
export default function Checkbox({ checked, onChange, ariaLabel }: CheckboxProps) {
  return (
    <span className="relative inline-flex shrink-0">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        aria-label={ariaLabel}
        className="peer absolute inset-0 z-10 h-full w-full cursor-pointer opacity-0"
      />
      <span
        aria-hidden
        className="flex h-5 w-5 items-center justify-center rounded-md border border-gray-300 bg-white transition-colors peer-checked:border-brand peer-checked:bg-brand peer-focus-visible:ring-2 peer-focus-visible:ring-brand/40"
      >
        <svg
          width="12"
          height="12"
          viewBox="0 0 24 24"
          fill="none"
          stroke="white"
          strokeWidth="3.5"
          strokeLinecap="round"
          strokeLinejoin="round"
          className={`transition-opacity ${checked ? 'opacity-100' : 'opacity-0'}`}
        >
          <polyline points="20 6 9 17 4 12" />
        </svg>
      </span>
    </span>
  );
}
