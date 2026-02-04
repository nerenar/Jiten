import DOMPurify from 'isomorphic-dompurify';

/**
 * Sanitises HTML content to prevent XSS attacks.
 * Allows basic formatting tags used for ruby text and styling.
 */
export function sanitiseHtml(html: string): string {
  if (!html) return '';

  return DOMPurify.sanitize(html, {
    ALLOWED_TAGS: ['ruby', 'rt', 'rp', 'span', 'strong', 'em', 'br', 'a', 'b', 'i'],
    ALLOWED_ATTR: ['href', 'target', 'rel', 'class', 'style', 'lang'],
    ALLOW_DATA_ATTR: false,
  });
}
