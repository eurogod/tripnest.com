import type { PricingSettings } from '../types';
import { apiGet, apiPut } from './client';
import type { PricingSettingsResponseDto } from './backend';

// Per-listing pricing, backed by TripNest.Core's /api/pricing/{propertyId}
// (defaults are derived from the listing when nothing has been saved yet).

function toSettings(dto: PricingSettingsResponseDto): PricingSettings {
  return {
    baseRate: dto.baseRate,
    weekendRate: dto.weekendRate,
    weeklyDiscountPercent: dto.weeklyDiscountPercent,
    monthlyDiscountPercent: dto.monthlyDiscountPercent,
    minNights: dto.minNights,
    cleaningFee: dto.cleaningFee,
  };
}

export async function getPricingSettings(propertyId: string): Promise<PricingSettings> {
  return toSettings(await apiGet<PricingSettingsResponseDto>(`/api/pricing/${propertyId}`));
}

export async function savePricingSettings(
  propertyId: string,
  settings: PricingSettings,
): Promise<PricingSettings> {
  return toSettings(await apiPut<PricingSettingsResponseDto>(`/api/pricing/${propertyId}`, settings));
}
