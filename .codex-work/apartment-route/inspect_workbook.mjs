import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "/Users/malikhawkins/Downloads/Malik_Apartment_Hunt_Tracker.xlsx";
const previewDir = "/Users/malikhawkins/Godot Projects /first-arrival/.codex-work/apartment-route/previews";

await fs.mkdir(previewDir, { recursive: true });
const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const summary = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 12000,
  tableMaxRows: 30,
  tableMaxCols: 20,
  tableMaxCellChars: 160,
});
console.log("WORKBOOK_SUMMARY");
console.log(summary.ndjson);

for (const sheet of workbook.worksheets.items) {
  const used = sheet.getUsedRange(true);
  if (used) {
    const inspection = await workbook.inspect({
      kind: "table",
      sheetId: sheet.name,
      range: used.address,
      include: "values,formulas",
      maxChars: 20000,
      tableMaxRows: 100,
      tableMaxCols: 30,
      tableMaxCellChars: 220,
    });
    console.log(`SHEET_DATA ${sheet.name} ${used.address}`);
    console.log(inspection.ndjson);
  }

  const preview = await workbook.render({
    sheetName: sheet.name,
    autoCrop: "all",
    scale: 1.5,
    format: "png",
  });
  const safeName = sheet.name.replaceAll(/[^A-Za-z0-9_-]/g, "_");
  await fs.writeFile(
    `${previewDir}/${safeName}.png`,
    new Uint8Array(await preview.arrayBuffer()),
  );
}
