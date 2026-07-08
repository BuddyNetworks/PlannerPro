import { Pipe, PipeTransform, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';
import DOMPurify from 'dompurify';

marked.setOptions({ gfm: true, breaks: true });

/**
 * Renders a Markdown string to sanitized, trusted HTML for [innerHTML].
 * marked → HTML, then DOMPurify strips anything unsafe before we bypass
 * Angular's sanitizer (safe because the output is already sanitized).
 */
@Pipe({ name: 'markdown' })
export class MarkdownPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);

  transform(md: string | null | undefined): SafeHtml {
    if (!md || !md.trim()) return '';
    const raw = marked.parse(md, { async: false }) as string;
    const clean = DOMPurify.sanitize(raw, { USE_PROFILES: { html: true } });
    return this.sanitizer.bypassSecurityTrustHtml(clean);
  }
}
