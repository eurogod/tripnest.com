import type { HotspotCategory, PropertyTour, TourChapter, TourRoom, TourSegment } from '../types';
import { buildTour } from '../data/tours';
import { getPropertyById } from './properties';
import { getClipsForProperty } from '../lib/clipStore';
import { apiGet, apiPut } from './client';
import type { PropertyTourResponseDto, TourRoomDto } from './backend';

// Service layer for property virtual tours. The tour is derived from the
// property (walkthrough video metadata, including generation status, rides
// inside PropertyTour). Listings are fetched from the backend so their
// uploaded photos feed the walkthrough; a landlord-authored tour wins over
// the synthesized rooms. Veo-generated clips stored in this browser upgrade
// photo rooms to video.

// Blob object URLs handed out per tour, so VirtualTour can release them on
// close instead of leaking one URL per generated clip per open.
const tourUrls = new Map<string, string[]>();

export function releaseTourMedia(propertyId: string): void {
  tourUrls.get(propertyId)?.forEach((url) => URL.revokeObjectURL(url));
  tourUrls.delete(propertyId);
}

// Gradient pairs for authored rooms (cycled), matching data/tours.ts's look.
const AUTHORED_GRADIENTS: [string, string][] = [
  ['#f5ecd9', '#d8c4a3'],
  ['#e6f4ea', '#bcd9c4'],
  ['#e9eef3', '#c3ced9'],
  ['#e9e6f6', '#c3bce0'],
  ['#e3f0fb', '#b8d8f0'],
  ['#e8f3dd', '#bcd99a'],
];

const AREAS = new Set(['Entrance', 'Indoor', 'Outdoor', 'Exterior']);

function mapAuthoredRoom(dto: TourRoomDto, index: number, photo?: string): TourRoom {
  const [from, to] = AUTHORED_GRADIENTS[index % AUTHORED_GRADIENTS.length];
  return {
    id: dto.id,
    name: dto.name,
    area: (AREAS.has(dto.area) ? dto.area : 'Indoor') as TourRoom['area'],
    caption: dto.caption,
    dimensions: dto.dimensions ?? undefined,
    from,
    to,
    image: photo,
    hotspots: dto.hotspots.map((h) => ({
      id: h.id,
      x: h.x,
      y: h.y,
      label: h.label,
      category: (h.category || 'amenity') as HotspotCategory,
      detail: h.detail,
    })),
  };
}

/** Landlord-authored tour from the backend; null when none exists (404). */
export async function getAuthoredTour(propertyId: string): Promise<PropertyTourResponseDto | null> {
  try {
    return await apiGet<PropertyTourResponseDto>(`/api/properties/${propertyId}/tour`);
  } catch {
    return null;
  }
}

/** Create or replace the landlord-authored tour (rooms + hotspots). */
export function saveAuthoredTour(
  propertyId: string,
  tour: { title: string; rooms: TourRoomDto[] },
): Promise<PropertyTourResponseDto> {
  return apiPut<PropertyTourResponseDto>(`/api/properties/${propertyId}/tour`, tour);
}

export async function getPropertyTour(id: string): Promise<PropertyTour | undefined> {
  const property = await getPropertyById(id);
  if (!property) return undefined;
  const tour = buildTour(property);

  // A tour authored through the backend wins over the synthesized rooms;
  // property photos still supply the room stills (dto.media is a dead path
  // until Core serves /uploads).
  const authored = await getAuthoredTour(property.id);
  if (authored && authored.rooms.length > 0) {
    tour.title = authored.title || tour.title;
    tour.rooms = authored.rooms.map((room, i) => mapAuthoredRoom(room, i, property.photos?.[i]));
  }

  // Overlay locally generated walkthrough clips (photo index i → room i,
  // matching buildTour's photo assignment). Authored ready clips win.
  releaseTourMedia(property.id); // a re-open replaces the previous URL set
  try {
    const generated = await getClipsForProperty(property.id);
    if (generated.size > 0) {
      const urls: string[] = [];
      tour.rooms = tour.rooms.map((room, i) => {
        const clip = generated.get(i);
        if (!clip || room.clip?.status === 'ready') return room;
        const url = URL.createObjectURL(clip.blob);
        urls.push(url);
        return {
          ...room,
          clip: {
            url,
            status: 'ready' as const,
            provider: clip.provider ?? 'local',
            // WebM from MediaRecorder often lacks duration metadata, so
            // carry the nominal length for progress/synthesis math.
            durationSec: 8,
            sourcePhotos: clip.sourcePhoto ? [clip.sourcePhoto] : room.clip?.sourcePhotos,
            generatedAt: clip.generatedAt,
          },
        };
      });
      tourUrls.set(property.id, urls);
    }
  } catch {
    // IndexedDB unavailable (private mode etc.) — photos still show.
  }

  // No authored full video but enough per-room clips: synthesize the
  // continuous walkthrough as a sequential playlist with chapters.
  if (!tour.fullVideo) {
    const readyRooms = tour.rooms.filter((r) => r.clip?.status === 'ready' && r.clip.url);
    if (readyRooms.length >= 2) {
      const segments: TourSegment[] = [];
      const chapters: TourChapter[] = [];
      let start = 0;
      for (const room of readyRooms) {
        const durationSec = room.clip?.durationSec ?? 8; // Veo clips are 8s
        segments.push({ roomId: room.id, url: room.clip!.url!, durationSec });
        chapters.push({ roomId: room.id, startSec: start });
        start += durationSec;
      }
      tour.fullVideo = {
        status: 'ready',
        provider: 'google-flow',
        durationSec: start,
        chapters,
        segments,
      };
    }
  }
  return tour;
}
