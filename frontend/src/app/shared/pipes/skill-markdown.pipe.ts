import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked, type MarkedExtension } from 'marked';
import { markedHighlight } from 'marked-highlight';
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

function escapeHtml(text: string): string {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

function normalizeFenceLang(lang: string): string {
  const raw = (lang || '').toLowerCase().trim();
  if (!raw) return 'plaintext';
  const map: Record<string, string> = {
    js: 'javascript',
    ts: 'typescript',
    tsx: 'tsx',
    jsx: 'jsx',
    py: 'python',
    sh: 'bash',
    shell: 'bash',
    zsh: 'bash',
    yml: 'yaml',
    html: 'markup',
    xml: 'markup',
    cs: 'csharp',
    dockerfile: 'docker',
    md: 'markdown',
    plaintext: 'plaintext',
    text: 'plaintext',
    txt: 'plaintext',
  };
  return map[raw] ?? raw;
}

let configured = false;

function ensureMarkedConfigured(): void {
  if (configured) return;
  marked.use(
    markedHighlight({
      langPrefix: 'language-',
      highlight(code: string, lang: string) {
        const language = normalizeFenceLang(lang);
        if (language === 'plaintext') {
          return escapeHtml(code);
        }
        const grammar = (Prism.languages as Record<string, Prism.Grammar>)[language];
        if (grammar) {
          try {
            return Prism.highlight(code, grammar, language);
          } catch {
            return escapeHtml(code);
          }
        }
        return escapeHtml(code);
      },
    }) as MarkedExtension,
  );
  marked.setOptions({ gfm: true, breaks: true });
  configured = true;
}

@Pipe({ name: 'skillMarkdown', standalone: true })
export class SkillMarkdownPipe implements PipeTransform {
  constructor(private readonly sanitizer: DomSanitizer) {
    ensureMarkedConfigured();
  }

  transform(value: string | null | undefined): SafeHtml {
    if (!value) {
      return this.sanitizer.bypassSecurityTrustHtml('');
    }
    ensureMarkedConfigured();
    try {
      const html = marked(value, { async: false }) as string;
      return this.sanitizer.bypassSecurityTrustHtml(html);
    } catch (e) {
      console.error('skillMarkdown', e);
      return this.sanitizer.bypassSecurityTrustHtml(
        `<pre class="skill-md-fallback">${escapeHtml(value)}</pre>`,
      );
    }
  }
}
