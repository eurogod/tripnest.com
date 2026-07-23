import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

const CATEGORIES = ['All', 'Student Rooms', 'Apartments', 'Long-term', 'Short Stay', 'Near UMaT', 'Furnished'];

export default function CategoryChips() {
  const [active, setActive] = useState('All');
  const navigate = useNavigate();

  // Chips are a discovery entry point — selecting one opens the search page.
  const choose = (c: string) => {
    setActive(c);
    navigate('/search');
  };

  return (
    <div className="flex flex-wrap items-center gap-2">
      {CATEGORIES.map((c) => (
        <button
          key={c}
          onClick={() => choose(c)}
          className={`rounded-full border px-3.5 py-1.5 text-sm font-medium transition-colors ${
            active === c
              ? 'border-brand bg-brand-50 text-brand'
              : 'border-gray-200 text-gray-600 hover:bg-gray-100'
          }`}
        >
          {c}
        </button>
      ))}
      <button
        onClick={() => navigate('/search')}
        className="ml-auto rounded-full border border-gray-200 px-3.5 py-1.5 text-sm font-medium text-gray-600 hover:bg-gray-100"
      >
        Filters
      </button>
    </div>
  );
}
