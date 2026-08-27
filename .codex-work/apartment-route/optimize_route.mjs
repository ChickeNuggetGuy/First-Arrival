import fs from "node:fs/promises";

const points = [
  { id: "approx_start", name: "Starting area (near 13207 N US 183)", address: "13207 N US 183 SVRD NB, Austin, TX 78750-3213", lon: -97.784514, lat: 30.445539 },
  { id: "alma", name: "Alma Apartments", address: "9220 N Interstate Highway 35, Austin, TX 78753", lon: -97.6897877, lat: 30.3579451 },
  { id: "keystone", name: "Keystone Apartments", address: "5230 Thunder Creek Rd, Austin, TX 78759", lon: -97.7435285, lat: 30.4157838 },
  { id: "copperline", name: "Copperline at Village Oaks", address: "9811 Copper Creek Dr, Austin, TX 78729", lon: -97.7870653, lat: 30.4584949 },
  { id: "san_gabriel", name: "San Gabriel Square Apartments", address: "2212 San Gabriel St, Austin, TX 78705", lon: -97.7480632, lat: 30.2864335 },
  { id: "chevy_chase", name: "Chevy Chase Downs", address: "2504 Huntwick Dr, Austin, TX 78741", lon: -97.7269282, lat: 30.2256107 },
  { id: "creeks_edge", name: "Creeks Edge Apartments", address: "1124 Rutland Dr, Austin, TX 78758", lon: -97.7027033, lat: 30.3680113 },
  { id: "the_brook", name: "The Brook Apartments", address: "1824 S Interstate 35, Austin, TX 78704", lon: -97.7381585, lat: 30.2397793 },
  { id: "bridge_south", name: "Bridge at South Point", address: "6808 S Interstate 35, Austin, TX 78745", lon: -97.7714937, lat: 30.1899522 },
  { id: "walnut", name: "Walnut Creek Crossing", address: "2000 Cedar Bend Dr, Austin, TX 78758", lon: -97.6977156, lat: 30.4084997 },
  { id: "wildwood", name: "Wildwood Apartments", address: "7610 Cameron Rd, Austin, TX 78752", lon: -97.6874666, lat: 30.3313332 },
  { id: "patton", name: "1706 Patton Lane #102", address: "1706 Patton Ln #102, Austin, TX 78723", lon: -97.6893124, lat: 30.3191197 },
];

const coordinateString = points.map(({ lon, lat }) => `${lon},${lat}`).join(";");
const tableUrl = `https://router.project-osrm.org/table/v1/driving/${coordinateString}?annotations=duration,distance`;
const tableResponse = await fetch(tableUrl, { headers: { "User-Agent": "CodexApartmentRoute/1.0" } });
if (!tableResponse.ok) throw new Error(`OSRM table: ${tableResponse.status}`);
const table = await tableResponse.json();
if (table.code !== "Ok") throw new Error(`OSRM table: ${table.code}`);

function optimize(matrix) {
  const stopCount = points.length - 1;
  const stateCount = 1 << stopCount;
  const dp = Array.from({ length: stateCount }, () => new Float64Array(stopCount).fill(Infinity));
  const prev = Array.from({ length: stateCount }, () => new Int16Array(stopCount).fill(-1));

  for (let j = 0; j < stopCount; j += 1) {
    dp[1 << j][j] = matrix[0][j + 1];
  }

  for (let mask = 1; mask < stateCount; mask += 1) {
    for (let j = 0; j < stopCount; j += 1) {
      if ((mask & (1 << j)) === 0 || !Number.isFinite(dp[mask][j])) continue;
      for (let k = 0; k < stopCount; k += 1) {
        if (mask & (1 << k)) continue;
        const nextMask = mask | (1 << k);
        const candidate = dp[mask][j] + matrix[j + 1][k + 1];
        if (candidate < dp[nextMask][k]) {
          dp[nextMask][k] = candidate;
          prev[nextMask][k] = j;
        }
      }
    }
  }

  const fullMask = stateCount - 1;
  let end = 0;
  for (let j = 1; j < stopCount; j += 1) {
    if (dp[fullMask][j] < dp[fullMask][end]) end = j;
  }

  const reverseOrder = [];
  let mask = fullMask;
  let current = end;
  while (current >= 0) {
    reverseOrder.push(current + 1);
    const prior = prev[mask][current];
    mask ^= 1 << current;
    current = prior;
  }

  return { value: dp[fullMask][end], order: [0, ...reverseOrder.reverse()] };
}

const fastest = optimize(table.durations);
const shortest = optimize(table.distances);

async function routeFor(order) {
  const coords = order.map((index) => `${points[index].lon},${points[index].lat}`).join(";");
  const routeUrl = `https://router.project-osrm.org/route/v1/driving/${coords}?overview=full&geometries=geojson&steps=false&annotations=false`;
  const response = await fetch(routeUrl, { headers: { "User-Agent": "CodexApartmentRoute/1.0" } });
  if (!response.ok) throw new Error(`OSRM route: ${response.status}`);
  const payload = await response.json();
  if (payload.code !== "Ok") throw new Error(`OSRM route: ${payload.code}`);
  return payload.routes[0];
}

const fastestRoute = await routeFor(fastest.order);
const shortestRoute = fastest.order.join(",") === shortest.order.join(",")
  ? fastestRoute
  : await routeFor(shortest.order);

function summarize(optimization, route) {
  const orderedPoints = optimization.order.map((index) => points[index]);
  return {
    order: orderedPoints.map(({ id, name, address, lon, lat }) => ({ id, name, address, lon, lat })),
    legs: route.legs.map((leg, index) => ({
      from: orderedPoints[index].name,
      to: orderedPoints[index + 1].name,
      miles: leg.distance / 1609.344,
      minutes: leg.duration / 60,
    })),
    totalMiles: route.distance / 1609.344,
    totalMinutes: route.duration / 60,
    geometry: route.geometry,
  };
}

const results = {
  generatedAt: new Date().toISOString(),
  assumptions: {
    startCoordinateBasis: "Nearby public landmark at 13301 N US Hwy 183; exact user-provided address was not sent to the routing service.",
    traffic: "OSRM road-network estimate; no live or departure-time traffic.",
  },
  fastest: summarize(fastest, fastestRoute),
  shortest: summarize(shortest, shortestRoute),
  durationMatrixSeconds: table.durations,
  distanceMatrixMeters: table.distances,
};

await fs.writeFile("route_results.json", JSON.stringify(results, null, 2));
await fs.writeFile("route_geometry.geojson", JSON.stringify({
  type: "FeatureCollection",
  features: [
    { type: "Feature", properties: { route: "fastest" }, geometry: results.fastest.geometry },
    ...results.fastest.order.map((point, index) => ({
      type: "Feature",
      properties: { order: index, id: point.id, name: point.name, address: point.address },
      geometry: { type: "Point", coordinates: [point.lon, point.lat] },
    })),
  ],
}, null, 2));

console.log(JSON.stringify({
  fastest: { order: results.fastest.order.map((point) => point.name), legs: results.fastest.legs, totalMiles: results.fastest.totalMiles, totalMinutes: results.fastest.totalMinutes },
  shortest: { order: results.shortest.order.map((point) => point.name), legs: results.shortest.legs, totalMiles: results.shortest.totalMiles, totalMinutes: results.shortest.totalMinutes },
}, null, 2));
