interface PagePlaceholderProps {
  title: string;
  description?: string;
}

// Temporary stub for dashboard pages that are scaffolded but not yet built.
export default function PagePlaceholder({ title, description }: PagePlaceholderProps) {
  return (
    <div>
      <h1 className="m-0 text-4xl font-bold text-ink">{title}</h1>
      <p className="mt-3 text-muted">
        {description ?? 'This page is scaffolded and ready to be built out.'}
      </p>
    </div>
  );
}
