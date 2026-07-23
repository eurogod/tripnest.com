import L from 'leaflet';

/** Pulsing dot for the live user location. */
export function userIcon(): L.DivIcon {
  return L.divIcon({
    className: 'tn-marker',
    html: '<span class="tn-user-dot"><span class="tn-user-pulse"></span></span>',
    iconSize: [22, 22],
    iconAnchor: [11, 11],
  });
}

/** Teardrop pin for an apartment; red + larger when selected. */
export function apartmentIcon(label: string, selected: boolean): L.DivIcon {
  const bg = selected ? '#e11d48' : '#0f5132';
  const scale = selected ? 1.12 : 1;
  return L.divIcon({
    className: 'tn-marker',
    html: `
      <div class="tn-pin" style="transform:scale(${scale})">
        <div class="tn-pin-body" style="background:${bg}">
          <span class="tn-pin-label">${label}</span>
        </div>
        <div class="tn-pin-tip" style="border-top-color:${bg}"></div>
      </div>`,
    iconSize: [54, 46],
    iconAnchor: [27, 46],
    popupAnchor: [0, -44],
  });
}

// small coloured dot for a point of interest.
export function poiIcon(color: string, emoji: string): L.DivIcon {
  return L.divIcon({
    className: 'tn-marker',
    html: `<span class="tn-poi" style="background:${color}">${emoji}</span>`,
    iconSize: [22, 22],
    iconAnchor: [11, 11],
    popupAnchor: [0, -12],
  });
}
