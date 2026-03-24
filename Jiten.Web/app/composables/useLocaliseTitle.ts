import { useJitenStore } from '~/stores/jitenStore';
import { localiseTitleWithLanguage } from '~/utils/localiseTitle';

export function useLocaliseTitle() {
  const store = useJitenStore();

  return (deck: { originalTitle: string; romajiTitle?: string | null; englishTitle?: string | null }): string => {
    return localiseTitleWithLanguage(deck, store.titleLanguage);
  };
}
