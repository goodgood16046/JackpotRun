// public/jackpotdex/img/*.png 파일명(확장자 제외) 목록으로 public/play/art_index.js 를 생성한다.
//  ui.js 의 icon() 헬퍼가 `ART.has(id)` 로 이미지 존재를 판단 → 404 깜빡임 없이 이모지 폴백.
//  이미지를 추가/삭제했으면 다시 실행: `node tools/gen_art_index.mjs`
import { readdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const imgDir = join(root, "public", "jackpotdex", "img");
const outFile = join(root, "public", "play", "art_index.js");

const ids = readdirSync(imgDir)
  .filter((f) => f.toLowerCase().endsWith(".png"))
  .map((f) => f.slice(0, -4))
  .sort();

const lines = [];
for (let i = 0; i < ids.length; i += 6) lines.push("  " + ids.slice(i, i + 6).map((s) => JSON.stringify(s)).join(", ") + ",");
const src = `// ★자동 생성 파일 — 직접 수정 금지. \`node tools/gen_art_index.mjs\` 로 재생성.
// public/jackpotdex/img/ 에 존재하는 그림 id 목록(${ids.length}개). ui.js icon() 이 이미지 사용 여부 판단에 쓴다.
export const ART = new Set([
${lines.join("\n")}
]);
`;
writeFileSync(outFile, src);
console.log(`art_index.js: ${ids.length} ids -> ${outFile}`);
