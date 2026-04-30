export interface PosCategory {
  key: string;
  label: string;
  tags: { value: string; label: string }[];
}

export const posCategories: PosCategory[] = [
  {
    key: 'nouns',
    label: 'Nouns',
    tags: [
      { value: 'n', label: 'Noun' },
      { value: 'n-adv', label: 'Adverbial noun' },
      { value: 'n-t', label: 'Temporal noun' },
      { value: 'n-pr', label: 'Proper noun' },
      { value: 'n-pref', label: 'Noun prefix' },
      { value: 'n-suf', label: 'Noun suffix' },
    ],
  },
  {
    key: 'verbs',
    label: 'Verbs',
    tags: [
      { value: 'v1', label: 'Ichidan' },
      { value: 'v1-s', label: 'Ichidan (kureru)' },
      { value: 'v5u', label: 'Godan -u' },
      { value: 'v5k', label: 'Godan -ku' },
      { value: 'v5k-s', label: 'Godan -ku (iku/yuku)' },
      { value: 'v5s', label: 'Godan -su' },
      { value: 'v5r', label: 'Godan -ru' },
      { value: 'v5r-i', label: 'Godan -ru (irregular)' },
      { value: 'v5b', label: 'Godan -bu' },
      { value: 'v5g', label: 'Godan -gu' },
      { value: 'v5m', label: 'Godan -mu' },
      { value: 'v5n', label: 'Godan -nu' },
      { value: 'v5t', label: 'Godan -tsu' },
      { value: 'v5aru', label: 'Godan -aru' },
      { value: 'v5u-s', label: 'Godan -u (special)' },
      { value: 'vs', label: 'Suru verb' },
      { value: 'vs-i', label: 'Suru (included)' },
      { value: 'vs-s', label: 'Suru (special)' },
      { value: 'vk', label: 'Kuru verb' },
      { value: 'vz', label: 'Ichidan -zuru' },
      { value: 'vi', label: 'Intransitive' },
      { value: 'vt', label: 'Transitive' },
    ],
  },
  {
    key: 'adjectives',
    label: 'Adjectives',
    tags: [
      { value: 'adj-i', label: 'i-adjective' },
      { value: 'adj-ix', label: 'i-adjective (yoi/ii)' },
      { value: 'adj-na', label: 'na-adjective' },
      { value: 'adj-no', label: 'no-adjective' },
      { value: 'adj-pn', label: 'Pre-noun adjectival' },
      { value: 'adj-f', label: 'Prenominal' },
      { value: 'adj-t', label: 'taru-adjective' },
    ],
  },
  {
    key: 'adverbs',
    label: 'Adverbs',
    tags: [
      { value: 'adv', label: 'Adverb' },
      { value: 'adv-to', label: "Adverb (to)" },
    ],
  },
  {
    key: 'expressions',
    label: 'Expressions',
    tags: [
      { value: 'exp', label: 'Expression' },
      { value: 'id', label: 'Idiomatic' },
      { value: 'proverb', label: 'Proverb' },
      { value: 'yoji', label: 'Yojijukugo' },
    ],
  },
  {
    key: 'particles',
    label: 'Particles & Grammar',
    tags: [
      { value: 'prt', label: 'Particle' },
      { value: 'conj', label: 'Conjunction' },
      { value: 'aux', label: 'Auxiliary' },
      { value: 'aux-v', label: 'Auxiliary verb' },
      { value: 'aux-adj', label: 'Auxiliary adjective' },
      { value: 'cop', label: 'Copula' },
      { value: 'int', label: 'Interjection' },
    ],
  },
  {
    key: 'affixes',
    label: 'Affixes & Counters',
    tags: [
      { value: 'pref', label: 'Prefix' },
      { value: 'suf', label: 'Suffix' },
      { value: 'ctr', label: 'Counter' },
      { value: 'num', label: 'Numeric' },
    ],
  },
  {
    key: 'other',
    label: 'Other',
    tags: [
      { value: 'pn', label: 'Pronoun' },
      { value: 'unc', label: 'Unclassified' },
      { value: 'on-mim', label: 'Onomatopoeia' },
    ],
  },
];

export const allPosTags: { value: string; label: string }[] = posCategories.flatMap((c) => c.tags);
