import type {
  HotspotCategory, Property, PropertyTour, TourHotspot, TourRoom, TourVideo,
} from '../types';

// ---------------------------------------------------------------------------
// Per-property virtual tours. Rather than hand-author a scene list for every
// listing, we derive a realistic walkthrough from each property's attributes
// (rooms, baths, type, amenities). Real walkthrough media (locally generated
// clips, landlord-authored tours) is layered on top by api/tours.ts; rooms
// without a ready clip fall back to a still image, then to the styled
// gradient scene.
// ---------------------------------------------------------------------------

// Scene gradient palettes per area, tuned for a warm, welcoming feel.
const PALETTE = {
  entrance: { from: '#f5ecd9', to: '#d8c4a3' },
  living: { from: '#e6f4ea', to: '#bcd9c4' },
  kitchen: { from: '#e9eef3', to: '#c3ced9' },
  dining: { from: '#fbe6cf', to: '#e6c79a' },
  bedroom: { from: '#e9e6f6', to: '#c3bce0' },
  bathroom: { from: '#e0f1f1', to: '#b6dad9' },
  workspace: { from: '#eef0f2', to: '#cfd4da' },
  balcony: { from: '#e3f0fb', to: '#b8d8f0' },
  garden: { from: '#e8f3dd', to: '#bcd99a' },
  parking: { from: '#ecebe8', to: '#cbc8c1' },
  exterior: { from: '#fde9d2', to: '#f3b98f' },
} as const;

let hsSeq = 0;
function spot(
  x: number,
  y: number,
  label: string,
  category: HotspotCategory,
  detail: string,
): TourHotspot {
  return { id: `hs-${++hsSeq}`, x, y, label, category, detail };
}

function has(property: Property, ...names: string[]): boolean {
  return property.amenities.some((a) =>
    names.some((n) => a.toLowerCase().includes(n.toLowerCase())),
  );
}

function buildRooms(property: Property): TourRoom[] {
  const rooms: TourRoom[] = [];
  const bedrooms = Math.max(1, property.beds);
  const bathrooms = Math.max(1, property.baths);
  const isHouse = /house/i.test(property.type) || /house/i.test(property.title);
  const isShortStay = /short|guest|hostel/i.test(property.type);
  const isStudent = /student|room/i.test(property.type);

  // 1. Entrance
  rooms.push({
    id: 'entrance',
    name: 'Main Entrance',
    area: 'Entrance',
    caption: 'Step inside through the main door',
    ...PALETTE.entrance,
    hotspots: [
      spot(28, 58, 'Secure entry', 'amenity', 'Private, lockable entrance with a well-lit approach.'),
      has(property, 'security')
        ? spot(70, 46, 'Gated security', 'amenity', '24/7 gated security and a guarded compound.')
        : spot(70, 46, 'Key handover', 'amenity', 'Self check-in with a lockbox — keys ready on arrival.'),
    ],
  });

  // 2. Living room
  rooms.push({
    id: 'living',
    name: 'Living Room',
    area: 'Indoor',
    caption: 'Relax in the bright, open living space',
    dimensions: isStudent ? '3.2 × 3.6 m' : '4.6 × 5.2 m',
    ...PALETTE.living,
    hotspots: [
      spot(30, 62, 'Lounge seating', 'seating', 'Comfortable sofa seating for the whole group to unwind.'),
      spot(62, 40, 'Smart TV', 'entertainment', '50" smart TV with Netflix and YouTube ready to stream.'),
      has(property, 'ac')
        ? spot(82, 30, 'Air conditioning', 'amenity', 'Quiet split-unit A/C keeps the room cool all day.')
        : spot(82, 30, 'Natural airflow', 'view', 'Cross-ventilated windows keep the space breezy.'),
    ],
  });

  // 3. Kitchen
  rooms.push({
    id: 'kitchen',
    name: isStudent ? 'Kitchenette' : 'Kitchen',
    area: 'Indoor',
    caption: 'A fully equipped space to cook and prep',
    dimensions: isStudent ? '2.4 × 2.2 m' : '3.4 × 3.0 m',
    ...PALETTE.kitchen,
    hotspots: [
      spot(34, 50, 'Appliances', 'kitchen', 'Fridge, gas cooktop, microwave and kettle provided.'),
      spot(64, 58, 'Cookware & storage', 'storage', 'Stocked cabinets with pots, pans, plates and utensils.'),
      has(property, 'water')
        ? spot(80, 40, 'Reliable water', 'amenity', 'Constant running water with backup storage.')
        : spot(80, 40, 'Prep counter', 'kitchen', 'Generous counter space for meal prep.'),
    ],
  });

  // 4. Dining (skip for the smallest student rooms)
  if (!isStudent) {
    rooms.push({
      id: 'dining',
      name: 'Dining Area',
      area: 'Indoor',
      caption: 'Gather around the dining table',
      dimensions: '3.0 × 2.8 m',
      ...PALETTE.dining,
      hotspots: [
        spot(48, 54, 'Dining set', 'seating', `Seats ${Math.max(2, bedrooms * 2)} comfortably for shared meals.`),
        spot(74, 36, 'Pendant lighting', 'amenity', 'Warm overhead lighting sets a cosy evening mood.'),
      ],
    });
  }

  // 5. Bedrooms
  for (let i = 0; i < bedrooms; i++) {
    const master = i === 0;
    rooms.push({
      id: `bedroom-${i + 1}`,
      name: master ? (bedrooms > 1 ? 'Master Bedroom' : 'Bedroom') : `Bedroom ${i + 1}`,
      area: 'Indoor',
      caption: master ? 'Unwind in the main bedroom' : 'A restful guest bedroom',
      dimensions: master ? '4.0 × 4.4 m' : '3.4 × 3.6 m',
      ...PALETTE.bedroom,
      hotspots: [
        spot(38, 56, master ? 'King bed' : 'Queen bed', 'bed', master
          ? 'Plush king bed with fresh linens and extra pillows.'
          : 'Comfortable queen bed made up with quality linens.'),
        spot(70, 50, 'Wardrobe', 'storage', 'Built-in wardrobe with hangers and shelf space.'),
        spot(84, 30, 'Window view', 'view', 'Large window with natural light and blackout curtains.'),
      ],
    });
  }

  // 6. Bathrooms
  for (let i = 0; i < bathrooms; i++) {
    rooms.push({
      id: `bathroom-${i + 1}`,
      name: bathrooms > 1 ? `Bathroom ${i + 1}` : 'Bathroom',
      area: 'Indoor',
      caption: 'Fresh, clean and well-appointed',
      dimensions: '2.2 × 2.0 m',
      ...PALETTE.bathroom,
      hotspots: [
        spot(36, 52, 'Hot shower', 'bathroom', 'Walk-in shower with reliable hot water.'),
        spot(68, 56, 'Vanity & towels', 'storage', 'Vanity with mirror; fresh towels and toiletries provided.'),
      ],
    });
  }

  // 7. Workspace
  if (has(property, 'wifi', 'desk', 'study')) {
    rooms.push({
      id: 'workspace',
      name: 'Workspace',
      area: 'Indoor',
      caption: 'A quiet corner to get things done',
      dimensions: '1.8 × 1.6 m',
      ...PALETTE.workspace,
      hotspots: [
        spot(42, 54, 'Desk & chair', 'workspace', 'Dedicated desk and ergonomic chair for remote work or study.'),
        spot(72, 40, 'High-speed WiFi', 'amenity', 'Fast, reliable fibre WiFi throughout the property.'),
      ],
    });
  }

  // 8. Balcony / outdoor seating
  if (!isStudent) {
    rooms.push({
      id: 'balcony',
      name: isHouse ? 'Patio' : 'Balcony',
      area: 'Outdoor',
      caption: 'Step out for fresh air and a view',
      dimensions: isHouse ? '3.6 × 2.4 m' : '2.2 × 1.4 m',
      ...PALETTE.balcony,
      hotspots: [
        spot(40, 58, 'Outdoor seating', 'outdoor', 'Chairs and a small table — perfect for morning coffee.'),
        spot(72, 34, 'Neighbourhood view', 'view', `Open view over ${property.location.split(',').slice(-1)[0].trim()}.`),
      ],
    });
  }

  // 9. Garden (houses)
  if (isHouse) {
    rooms.push({
      id: 'garden',
      name: 'Garden',
      area: 'Outdoor',
      caption: 'A green, private outdoor space',
      dimensions: '8 × 6 m',
      ...PALETTE.garden,
      hotspots: [
        spot(34, 60, 'Lawn & seating', 'outdoor', 'Fenced lawn with seating — room for kids to play.'),
        spot(70, 44, 'Shaded corner', 'outdoor', 'Mature trees offer shade through the afternoon.'),
      ],
    });
  }

  // 10. Parking
  if (has(property, 'parking') || isHouse) {
    rooms.push({
      id: 'parking',
      name: 'Parking',
      area: 'Outdoor',
      caption: 'Secure space for your vehicle',
      ...PALETTE.parking,
      hotspots: [
        spot(46, 56, 'Private parking', 'parking', 'On-site parking space within the secure compound.'),
      ],
    });
  }

  // 11. Exterior & neighbourhood (final overview)
  rooms.push({
    id: 'exterior',
    name: 'Exterior & Neighbourhood',
    area: 'Exterior',
    caption: `The building and surroundings in ${property.location}`,
    ...PALETTE.exterior,
    hotspots: [
      spot(30, 50, 'Building frontage', 'view', 'Well-maintained exterior with a welcoming entrance.'),
      spot(58, 38, 'Nearby amenities', 'amenity', isShortStay
        ? 'Walking distance to markets, eateries and transport.'
        : 'Close to shops, transport and local conveniences.'),
      spot(80, 60, 'Best views', 'view', 'Catch the sunset from the front of the property.'),
    ],
  });

  return rooms;
}

/**
 * Build the virtual tour for a property. Uploaded listing photos become room
 * stills, each carrying a pending google-flow clip whose sourcePhotos record
 * what the video would be generated from; locally generated clips and
 * landlord-authored tours are layered on later by api/tours.ts.
 */
export function buildTour(property: Property): PropertyTour {
  const photos = property.photos ?? [];
  const rooms = buildRooms(property).map((room, i) => {
    const photo: string | undefined = photos[i];
    const clip = photo
      ? ({ status: 'pending', provider: 'google-flow', sourcePhotos: [photo] } satisfies TourVideo)
      : undefined;
    return { ...room, image: photo, clip };
  });
  return {
    propertyId: property.id,
    title: property.title,
    rooms,
  };
}
