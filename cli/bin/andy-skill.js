#!/usr/bin/env node
'use strict';

const path = require('path');
const os = require('os');
const { buildInstallUrl, downloadZip, extractZip } = require('../lib/install.js');

function printHelp() {
  console.log(`
Usage:
  andy-skill install [options] <namespace> <skill> <version>
  andy-skill install [options] <namespace>/<skill>@<version>

Options:
  --registry <url>   API origin (default: $SKILL_REGISTRY_URL or http://localhost:5289)
  --dir <path>       Skills root folder (default: ~/.agents/skills)
  -h, --help         Show help

Examples:
  andy-skill install --registry http://localhost:5289 acme email-helper 1.0.0
  andy-skill install acme/email-helper@1.0.0
  SKILL_REGISTRY_URL=https://skills.example.com andy-skill install acme/email-helper@2.1.0 --dir ~/.cursor/skills

Uses: {registry}/api/install/{namespace}/{skill}/{version}/package.zip
`);
}

function parseInstallArgv(argv) {
  const opts = {
    registry: process.env.SKILL_REGISTRY_URL || 'http://localhost:5289',
    dir: path.join(os.homedir(), '.agents', 'skills'),
    positional: [],
  };

  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '-h' || a === '--help') {
      opts.help = true;
      continue;
    }
    if (a === '--registry') {
      opts.registry = argv[++i];
      if (!opts.registry) {
        throw new Error('--registry requires a URL');
      }
      continue;
    }
    if (a === '--dir') {
      opts.dir = argv[++i];
      if (!opts.dir) {
        throw new Error('--dir requires a path');
      }
      continue;
    }
    if (a.startsWith('-')) {
      throw new Error(`Unknown option: ${a}`);
    }
    opts.positional.push(a);
  }

  return opts;
}

function parseTriple(positional) {
  if (positional.length === 1) {
    const s = positional[0];
    const at = s.lastIndexOf('@');
    if (at <= 0) {
      throw new Error('Expected namespace/skill@version when using a single argument.');
    }
    const version = s.slice(at + 1);
    const left = s.slice(0, at);
    const slash = left.indexOf('/');
    if (slash <= 0 || slash === left.length - 1) {
      throw new Error('Expected namespace/skill@version');
    }
    const ns = left.slice(0, slash);
    const skill = left.slice(slash + 1);
    return { ns, skill, version };
  }

  if (positional.length === 3) {
    return { ns: positional[0], skill: positional[1], version: positional[2] };
  }

  throw new Error('Provide either three arguments (namespace skill version) or one (namespace/skill@version).');
}

async function cmdInstall(opts) {
  const { ns, skill, version } = parseTriple(opts.positional);
  const url = buildInstallUrl(opts.registry, ns, skill, version);
  console.error(`→ GET ${url}`);
  const zip = await downloadZip(url);
  const dest = extractZip(zip, path.resolve(opts.dir), skill);
  console.error(`✓ Extracted under: ${dest}`);
  console.log(dest);
}

async function main() {
  const argv = process.argv.slice(2);

  if (argv.length === 0 || argv[0] === '-h' || argv[0] === '--help') {
    printHelp();
    process.exit(argv.length === 0 ? 1 : 0);
  }

  if (argv[0] !== 'install') {
    console.error('Unknown command. Only "install" is supported.\n');
    printHelp();
    process.exit(1);
  }

  const opts = parseInstallArgv(argv.slice(1));
  if (opts.help) {
    printHelp();
    process.exit(0);
  }

  if (opts.positional.length === 0) {
    printHelp();
    process.exit(1);
  }

  await cmdInstall(opts);
}

main().catch((err) => {
  console.error(err.message || String(err));
  process.exit(1);
});
