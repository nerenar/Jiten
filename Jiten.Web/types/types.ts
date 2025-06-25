import type { MediaType, ReadingType } from '~/types';

export interface Deck {
  deckId: number;
  creationDate: Date;
  coverName?: string;
  mediaType: MediaType;
  originalTitle: string;
  romajiTitle?: string;
  englishTitle?: string;
  characterCount: number;
  wordCount: number;
  uniqueWordCount: number;
  uniqueWordUsedOnceCount: number;
  uniqueKanjiCount: number;
  uniqueKanjiUsedOnceCount: number;
  difficulty: number;
  difficultyRaw: number;
  averageSentenceLength: number;
  parentDeckId: number;
  deckWords: DeckWord[];
  links: Link[];
  childrenDeckCount: number;
  selectedWordOccurrences: number;
  dialoguePercentage: number;
}

export interface DeckDetail {
  parentDeck: Deck | null;
  mainDeck: Deck;
  subDecks: Deck[];
}

export interface DeckVocabularyList {
  parentDeck: Deck | null;
  deck: Deck;
  words: DeckWord[];
}

export interface DeckWord {
  deckId: number;
  originalText: string;
  wordId: number;
  readingType: string;
  readingIndex: number;
  conjugations: string[];
}

export interface Link {
  linkId: number;
  url: string;
  linkType: string;
  deckId: number;
}

export interface Word {
  wordId: number;
  mainReading: Reading;
  alternativeReadings: Reading[];
  partsOfSpeech: string[];
  definitions: Definition[];
  occurrences: number;
  pitchAccents: number[];
}

export interface Reading {
  text: string;
  readingType: ReadingType;
  readingIndex: number;
  frequencyRank: number;
  frequencyPercentage: number;
  usedInMediaAmount: number;
  usedInMediaAmountByType: Record<MediaType, number>;
}

export interface Definition {
  index: number;
  meanings: string[];
  partsOfSpeech: string[];
}

export class PaginatedResponse<T> {
  constructor(
    public readonly data: T,
    public readonly totalItems: number,
    public readonly pageSize: number,
    public readonly currentOffset: number
  ) {}

  get totalPages(): number {
    return Math.ceil(this.totalItems / this.pageSize);
  }

  get currentPage(): number {
    return Math.floor(this.currentOffset / this.pageSize) + 1;
  }

  get hasPreviousPage(): boolean {
    return this.currentPage > 1;
  }

  get hasNextPage(): boolean {
    return this.currentPage < this.totalPages;
  }

  get previousOffset(): number | null {
    return this.hasPreviousPage ? Math.max(0, this.currentOffset - this.pageSize) : null;
  }

  get nextOffset(): number | null {
    return this.hasNextPage ? Math.min(this.totalItems, this.currentOffset + this.pageSize) : null;
  }
}

export interface GlobalStats {
  mediaByType: Record<MediaType, number>;
  totalMojis: number;
  totalMedia: number;
}

export interface Metadata {
  originalTitle: string;
  romajiTitle: string;
  englishTitle: string;
  image: string;
  releaseDate: string;
  links: Link[];
}

export interface Issues {
  missingRomajiTitles: number[];
  missingLinks: number[];
  zeroCharacters: number[];
}

export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

export interface TokenResponse {
  accessToken: string;
  accessTokenExpiration: Date;
  refreshToken: string;
}

export interface DeckCoverage {
  deckId: number;
  totalWordCount: number;
  knownWordsOccurrences: number;
  knownUniqueWordCount: number;
  uniqueWordCount: number;
  knownWordPercentage: number;
  knownUniqueWordPercentage: number;
}

export interface ExampleSentence {
  text: string;
  wordPosition: number;
  wordLength: number;
  sourceDeck: Deck;
  sourceDeckParent: Deck;
}
