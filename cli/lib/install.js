'use strict';

const fs = require('fs');
const path = require('path');
const AdmZip = require('adm-zip');

function normalizeSlashes(p) {
  return p.replace(/\\/g, '/').trim();
}

function assertSafeRelativeZipPath(rel) {
  const n = normalizeSlashes(rel);
  if (!n || n.startsWith('/') || n.split('/').some((s) => s === '..')) {
    throw new Error(`Refusing unsafe zip entry path: ${rel}`);
  }
}

/**
 * @param {string} registryBase e.g. http://localhost:5289 (no trailing slash)
 * @param {string} namespaceSlug
 * @param {string} skillSlug
 * @param {string} version
 */
function buildInstallUrl(registryBase, namespaceSlug, skillSlug, version) {
  const b = registryBase.replace(/\/+$/, '');
  const ns = encodeURIComponent(namespaceSlug);
  const sk = encodeURIComponent(skillSlug);
  const v = encodeURIComponent(version);
  return `${b}/api/install/${ns}/${sk}/${v}/package.zip`;
}

async function downloadZip(url) {
  const res = await fetch(url, { redirect: 'follow' });
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`Download failed HTTP ${res.status}${text ? `: ${text.slice(0, 200)}` : ''}`);
  }
  const buf = Buffer.from(await res.arrayBuffer());
  if (buf.length < 4 || buf[0] !== 0x50 || buf[1] !== 0x4b) {
    throw new Error('Response is not a ZIP file (missing PK header).');
  }
  return buf;
}

/**
 * Extract zip into skillsRoot preserving paths. If archive is only a root SKILL.md, drop under skillsRoot/skillSlug/.
 * @returns {string} directory written (skillsRoot or a subfolder)
 */
function extractZip(zipBuffer, skillsRoot, skillSlug) {
  fs.mkdirSync(skillsRoot, { recursive: true });

  const zip = new AdmZip(zipBuffer);
  const entries = zip.getEntries().filter((e) => !e.isDirectory);
  if (entries.length === 0) {
    throw new Error('ZIP archive has no files.');
  }

  const names = entries.map((e) => normalizeSlashes(e.entryName));
  names.forEach(assertSafeRelativeZipPath);

  const onlyRootSkillMd =
    entries.length === 1 &&
    !names[0].includes('/') &&
    path.posix.basename(names[0]).toLowerCase() === 'skill.md';

  if (onlyRootSkillMd) {
    const destDir = path.join(skillsRoot, skillSlug);
    fs.mkdirSync(destDir, { recursive: true });
    const data = zip.readFile(entries[0]);
    fs.writeFileSync(path.join(destDir, 'SKILL.md'), data);
    return destDir;
  }

  const rootResolved = path.resolve(skillsRoot);
  for (const e of entries) {
    const rel = normalizeSlashes(e.entryName);
    assertSafeRelativeZipPath(rel);
    const dest = path.resolve(skillsRoot, rel);
    if (dest !== rootResolved && !dest.startsWith(rootResolved + path.sep)) {
      throw new Error(`Zip slip blocked for entry: ${rel}`);
    }
    const dir = path.dirname(dest);
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(dest, zip.readFile(e));
  }

  return skillsRoot;
}

module.exports = {
  buildInstallUrl,
  downloadZip,
  extractZip,
};
