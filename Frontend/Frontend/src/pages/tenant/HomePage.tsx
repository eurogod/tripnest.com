import Hero from '../../components/tenant/home/Hero';
import CategoryChips from '../../components/tenant/home/CategoryChips';
import FeaturedProperties from '../../components/tenant/home/FeaturedProperties';
import InfoSections from '../../components/tenant/home/InfoSections';

export default function HomePage() {
  return (
    <div className="space-y-8">
      <Hero />
      <CategoryChips />
      <FeaturedProperties />
      <InfoSections />
    </div>
  );
}
