#!/usr/bin/env node
// Publish existing Unity output without requiring a Unity license on CI.
import fs from 'node:fs';
import path from 'node:path';
import { brotliDecompressSync } from 'node:zlib';

const args = process.argv.slice(2);
const compressed = args.includes('--keep-compressed');
const option = (name, fallback) => {
  const i = args.indexOf(name);
  if (i < 0) return fallback;
  if (!args[i + 1] || args[i + 1].startsWith('--')) throw new Error(`Missing ${name}`);
  return args[i + 1];
};
const input = fs.realpathSync(option('--input', 'Build/WebGL'));
const output = path.resolve(option('--output', 'Releases/pages-site'));
if (output === input || output.startsWith(input + path.sep) || input.startsWith(output + path.sep)) {
  throw new Error('Input and output must be separate directories.');
}
if (fs.existsSync(output) && (fs.lstatSync(output).isSymbolicLink() || fs.readdirSync(output).length)) {
  throw new Error('Output must be new or empty; existing files are never overwritten.');
}
let html = fs.readFileSync(path.join(input, 'index.html'), 'utf8');
const refs = [...html.matchAll(/(?:dataUrl|frameworkUrl|codeUrl):\s*buildUrl\s*\+\s*"\/([^"/]+)"/g)]
  .map(match => match[1]);
if (refs.length !== 3) throw new Error('Expected three Unity build resource URLs.');
const resources = ['WebGL.loader.js', ...refs];
// Preflight before writing so a missing/incomplete build cannot become a site.
for (const file of resources) {
  const full = path.join(input, 'Build', file);
  if (!fs.statSync(full).isFile() || fs.lstatSync(full).isSymbolicLink()) throw new Error(`Invalid ${file}`);
}
fs.mkdirSync(path.join(output, 'Build'), { recursive: true });
for (const file of resources) {
  let bytes = fs.readFileSync(path.join(input, 'Build', file));
  let name = file;
  if (!compressed && file.endsWith('.br')) {
    bytes = brotliDecompressSync(bytes);
    name = file.slice(0, -3);
    html = html.replaceAll(`"/${file}"`, `"/${name}"`);
  }
  fs.writeFileSync(path.join(output, 'Build', name), bytes);
}
for (const folder of ['TemplateData', 'StreamingAssets']) {
  if (fs.existsSync(path.join(input, folder))) {
    fs.cpSync(path.join(input, folder), path.join(output, folder), { recursive: true, dereference: true });
  }
}
html = html.replaceAll('Unity Web Player | haoxi-kaiyan-web', 'Let the Show Begin · 好戏开演')
  .replaceAll('productName: "haoxi-kaiyan-web"', 'productName: "Let the Show Begin"');
fs.writeFileSync(path.join(output, 'index.html'), html);
fs.writeFileSync(path.join(output, '.nojekyll'), '');
const project = path.resolve(import.meta.dirname, '..');
for (const [source, destination] of [
  ['LICENSE', 'LICENSE'], ['THIRD_PARTY_NOTICES.md', 'THIRD_PARTY_NOTICES.md'],
  ['Assets/Resources/OFL-NotoSansSC.txt', 'OFL-NotoSansSC.txt'],
]) {
  const original = path.join(input, destination);
  fs.copyFileSync(fs.existsSync(original) ? original : path.join(project, source), path.join(output, destination));
}
console.log(`Prepared ${compressed ? 'compressed publishing payload' : 'static Pages site'}: ${output}`);
