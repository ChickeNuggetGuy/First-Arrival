import fs from "node:fs/promises";

const locations = [
  { id: "alma", name: "Alma Apartments", address: "9220 N Interstate Highway 35, Austin, TX 78753" },
  { id: "keystone", name: "Keystone Apartments", address: "5230 Thunder Creek Rd, Austin, TX 78759" },
  { id: "copperline", name: "Copperline at Village Oaks", address: "9811 Copper Creek Dr, Austin, TX 78729" },
  { id: "san_gabriel", name: "San Gabriel Square Apartments", address: "2212 San Gabriel St, Austin, TX 78705" },
  { id: "chevy_chase", name: "Chevy Chase Downs", address: "2504 Huntwick Dr, Austin, TX 78741" },
  { id: "creeks_edge", name: "Creeks Edge Apartments", address: "1124 Rutland Dr, Austin, TX 78758" },
  { id: "the_brook", name: "The Brook Apartments", address: "1824 S Interstate 35, Austin, TX 78704" },
  { id: "bridge_south", name: "Bridge at South Point", address: "6808 S Interstate 35, Austin, TX 78745" },
  { id: "walnut", name: "Walnut Creek Crossing", address: "2000 Cedar Bend Dr, Austin, TX 78758" },
  { id: "wildwood", name: "Wildwood Apartments", address: "7610 Cameron Rd, Austin, TX 78752" },
  { id: "patton", name: "1706 Patton Lane #102", address: "1706 Patton Ln, Austin, TX 78723" },
];

const results = [];
for (const location of locations) {
  const url = new URL("https://geocoding.geo.census.gov/geocoder/locations/onelineaddress");
  url.searchParams.set("address", location.address);
  url.searchParams.set("benchmark", "Public_AR_Current");
  url.searchParams.set("format", "json");
  const response = await fetch(url, {
    headers: { "User-Agent": "CodexApartmentRoute/1.0" },
  });
  if (!response.ok) throw new Error(`${location.id}: ${response.status}`);
  const payload = await response.json();
  results.push({ ...location, matches: payload.result?.addressMatches ?? [] });
}

await fs.writeFile("census_geocodes.json", JSON.stringify(results, null, 2));
console.log(JSON.stringify(results.map(({ id, name, address, matches }) => ({
  id,
  name,
  address,
  matches: matches.map((match) => ({
    matchedAddress: match.matchedAddress,
    coordinates: match.coordinates,
    tigerLine: match.tigerLine,
  })),
})), null, 2));
