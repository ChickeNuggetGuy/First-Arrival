import fs from "node:fs/promises";

const template = await fs.readFile("austin-apartment-route.template.html", "utf8");
const streets = await fs.readFile("austin_major_streets_merged.geojson", "utf8");
const routeResults = JSON.parse(await fs.readFile("route_results.json", "utf8"));
const outputPath = "/Users/malikhawkins/.codex/visualizations/2026/08/12/019ff403-0f6d-7452-9715-4c6c7f757642/austin-apartment-route.html";

const fragment = template
  .replace("__STREETS_GEOJSON__", streets.trim())
  .replace("__ROUTE_GEOMETRY__", JSON.stringify(routeResults.fastest.geometry));

if (fragment.includes("__STREETS_GEOJSON__") || fragment.includes("__ROUTE_GEOMETRY__")) {
  throw new Error("Visualization placeholders were not fully replaced.");
}
if (fragment.includes('\\"') || fragment.includes("\\n")) {
  throw new Error("Visualization contains escaped literal markup.");
}

await fs.mkdir(new URL(".", `file://${outputPath}`).pathname, { recursive: true });
await fs.writeFile(outputPath, fragment);
console.log(outputPath);
console.log(`${Buffer.byteLength(fragment)} bytes`);
