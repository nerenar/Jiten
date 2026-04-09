export function stripRubyMarkup(text: string): string {
  return text.replace(
    /([\u4E00-\u9FFF\uFF10-\uFF5A々]+)\[([\u3040-\u309F\u30A0-\u30FF]+)\]/g,
    '$2',
  );
}
