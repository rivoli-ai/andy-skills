import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import Prism from 'prismjs';

import 'prismjs/components/prism-markup';
import 'prismjs/components/prism-javascript';
import 'prismjs/components/prism-typescript';
import 'prismjs/components/prism-jsx';
import 'prismjs/components/prism-tsx';
import 'prismjs/components/prism-json';
import 'prismjs/components/prism-python';
import 'prismjs/components/prism-bash';
import 'prismjs/components/prism-css';
import 'prismjs/components/prism-scss';
import 'prismjs/components/prism-java';
import 'prismjs/components/prism-csharp';
import 'prismjs/components/prism-go';
import 'prismjs/components/prism-rust';
import 'prismjs/components/prism-sql';
import 'prismjs/components/prism-yaml';
import 'prismjs/components/prism-docker';
import 'prismjs/components/prism-markdown';

/** Maps `overviewLanguageLabel` output to Prism grammar ids. */
const LANG_MAP: Record<string, string> = {
  text: 'plaintext',
  markdown: 'markdown',
  typescript: 'typescript',
  javascript: 'javascript',
  tsx: 'tsx',
  jsx: 'jsx',
  json: 'json',
  html: 'markup',
  xml: 'markup',
  css: 'css',
  scss: 'scss',
  python: 'python',
  csharp: 'csharp',
  rust: 'rust',
  go: 'go',
  yaml: 'yaml',
  shell: 'bash',
};

function escapeHtml(text: string): string {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

@Pipe({ name: 'codeHighlight', standalone: true })
export class CodeHighlightPipe implements PipeTransform {
  constructor(private readonly sanitizer: DomSanitizer) {}

  transform(line: string, langLabel: string): SafeHtml {
    const raw = (langLabel || 'text').toLowerCase();
    const prismLang = LANG_MAP[raw] ?? raw;
    if (line.length === 0) {
      return this.sanitizer.bypassSecurityTrustHtml('');
    }
    if (prismLang === 'plaintext') {
      return this.sanitizer.bypassSecurityTrustHtml(escapeHtml(line));
    }
    const grammar = (Prism.languages as Record<string, Prism.Grammar>)[prismLang];
    if (!grammar) {
      return this.sanitizer.bypassSecurityTrustHtml(escapeHtml(line));
    }
    try {
      return this.sanitizer.bypassSecurityTrustHtml(
        Prism.highlight(line, grammar, prismLang),
      );
    } catch {
      return this.sanitizer.bypassSecurityTrustHtml(escapeHtml(line));
    }
  }
}
