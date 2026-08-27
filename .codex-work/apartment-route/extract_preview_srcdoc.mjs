import fs from "node:fs/promises";

const wrapper = await fs.readFile("/private/tmp/austin-apartment-route-preview.html", "utf8");
const match = wrapper.match(/srcdoc="([\s\S]*?)"><\/iframe>/);
if (!match) throw new Error("Preview srcdoc was not found.");

const decoded = match[1]
  .replace(/&#x([0-9a-f]+);/gi, (_, value) => String.fromCodePoint(Number.parseInt(value, 16)))
  .replace(/&#([0-9]+);/g, (_, value) => String.fromCodePoint(Number.parseInt(value, 10)))
  .replaceAll("&quot;", '"')
  .replaceAll("&apos;", "'")
  .replaceAll("&lt;", "<")
  .replaceAll("&gt;", ">")
  .replaceAll("&amp;", "&");

await fs.writeFile("/private/tmp/austin-route-direct.html", decoded);
console.log(`${Buffer.byteLength(decoded)} bytes`);
