import { Link } from 'react-router-dom';
import { Button } from '@/components/ui';
import { Logo } from '@/components/Logo';

export default function NotFound() {
  return (
    <div className="grid min-h-full place-items-center px-4 py-20 text-center">
      <div>
        <div className="mb-6 flex justify-center">
          <Logo />
        </div>
        <p className="text-6xl font-extrabold text-brand-600">404</p>
        <h1 className="mt-3 text-2xl font-extrabold">We can't find that page</h1>
        <p className="mx-auto mt-2 max-w-sm text-sm text-muted">
          The link may be broken or the listing may have been removed. Let's get you back to safe ground.
        </p>
        <div className="mt-6 flex justify-center gap-3">
          <Link to="/">
            <Button>Back home</Button>
          </Link>
          <Link to="/search">
            <Button variant="outline">Browse stays</Button>
          </Link>
        </div>
      </div>
    </div>
  );
}
