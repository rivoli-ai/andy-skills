import { SecurityContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DomSanitizer } from '@angular/platform-browser';

import { CodeHighlightPipe } from './code-highlight.pipe';

describe('CodeHighlightPipe', () => {
  let sanitizer: DomSanitizer;
  let pipe: CodeHighlightPipe;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    sanitizer = TestBed.inject(DomSanitizer);
    pipe = new CodeHighlightPipe(sanitizer);
  });

  it('escapes plaintext content', () => {
    const html = sanitizer.sanitize(
      SecurityContext.HTML,
      pipe.transform('<script>alert("x")</script>', 'text'),
    );

    expect(html).toBe('&lt;script&gt;alert("x")&lt;/script&gt;');
  });
});
