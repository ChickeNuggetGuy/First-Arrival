import fs from "node:fs/promises";

const locations = [
  { id: "approx_start", name: "Approximate US-183 / Anderson Mill start", query: "US 183 and Anderson Mill Road, Austin, TX 78750" },
  { id: "alma", name: "Alma Apartments", query: "Alma Apartments, 9220 N Interstate Highway 35, Austin, TX 78753" },
  { id: "keystone", name: "Keystone Apartments", query: "Keystone Apartments, 5230 Thunder Creek Rd, Austin, TX 78759" },
  { id: "copperline", name: "Copperline at Village Oaks", query: "Copperline at Village Oaks, 9811 Copper Creek Dr, Austin, TX 78729" },
  { id: "san_gabriel", name: "San Gabriel Square Apartments", query: "San Gabriel Square Apartments, 2212 San Gabriel St, Austin, TX 78705" },
  { id: "chevy_chase", name: "Chevy Chase Downs", query: "Chevy Chase Downs, 2504 Huntwick Dr, Austin, TX 78741" },
  { id: "creeks_edge", name: "Creeks Edge Apartments", query: "Creeks Edge Apartments, 1124 Rutland Dr, Austin, TX 78758" },
  { id: "the_brook", name: "The Brook Apartments", query: "The Brook Apartments, 1824 S Interstate 35, Austin, TX 78704" },
  { id: "bridge_south", name: "Bridge at South Point", query: "Bridge at South Point, 6808 S Interstate 35, Austin, TX 78745" },
  { id: "walnut", name: "Walnut Creek Crossing", query: "Walnut Creek Crossing, 2000 Cedar Bend Dr, Austin, TX 78758" },
  { id: "wildwood", name: "Wildwood Apartments", query: "Wildwood Apartments, 7610 Cameron Rd, Austin, TX 78752" },
  { id: "patton", name: "1706 Patton Lane #102", query: "1706 Patton Ln, Austin, TX 78723" },
];

const results = [];
for (const [index, location] of locations.entries()) {
  const url = new URL("https://nominatim.openstreetmap.org/search");
  url.searchParams.set("q", location.query);
  url.searchParams.set("format", "jsonv2");
  url.searchParams.set("addressdetails", "1");
  url.searchParams.set("limit", "5");
  const response = await fetch(url, {
    headers: { "User-Agent": "CodexApartmentRoute/1.0" },
  });
  if (!response.ok) throw new Error(`${location.id}: ${response.status}`);
  const candidates = await response.json();
  results.push({ ...location, candidates });
  if (index < locations.length - 1) {
    await new Promise((resolve) => setTimeout(resolve, 1100));
  }
}

await fs.writeFile("geocodes.json", JSON.stringify(results, null, 2));
console.log(JSON.stringify(results.map(({ id, name, candidates }) => ({
  id,
  name,
  candidates: candidates.map((candidate) => ({
    lat: candidate.lat,
    lon: candidate.lon,
    type: candidate.type,
    name: candidate.name,
    display_name: candidate.display_name,
  })),
})), null, 2));
