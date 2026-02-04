import { useJitenStore } from '~/stores/jitenStore';
import { convertToRubyWithFurigana } from '~/utils/convertToRuby';
import { sanitiseHtml } from '~/utils/sanitiseHtml';

export function useConvertToRuby() {
  const store = useJitenStore();

  return (text: string): string => {
    const html = convertToRubyWithFurigana(text, store.displayFurigana);
    return sanitiseHtml(html);
  };
}
