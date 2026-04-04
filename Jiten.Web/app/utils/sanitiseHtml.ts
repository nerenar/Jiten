const dangerous = /<\s*\/?\s*(script|iframe|object|embed|form|input|textarea|button)\b[^>]*>/gi;
const onHandlers = /\s+on\w+\s*=\s*["'][^"']*["']/gi;

/**
 * Lightweight HTML sanitiser for trusted sources (own API, app-generated markup).
 * Strips script/iframe/embed tags and inline event handlers as defense-in-depth.
 */
export function sanitiseHtml(html: string): string {
  if (!html) return '';
  return html.replace(dangerous, '').replace(onHandlers, '');
}
