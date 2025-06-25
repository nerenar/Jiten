<script setup lang="ts">
  import type { ExampleSentence, MediaType, Word } from '~/types';
  import { formatPercentageApprox } from '~/utils/formatPercentageApprox';
  import { convertToRuby } from '~/utils/convertToRuby';
  import { getMediaTypeText } from '~/utils/mediaTypeMapper';
  import ExampleSentenceEntry from '~/components/ExampleSentenceEntry.vue';

  const props = defineProps({
    wordId: {
      type: Number,
      required: true,
    },
    readingIndex: {
      type: Number,
      required: true,
    },
    showRedirect: {
      type: Boolean,
      required: false,
    },
    conjugations: {
      type: Array as PropType<string[]>,
      default: () => [],
      required: false,
    },
  });

  const emit = defineEmits(['mainReadingTextChanged', 'readingSelected']);
  const { $api } = useNuxtApp();

  const currentReadingIndex = ref(props.readingIndex);
  const url = computed(() => `vocabulary/${props.wordId}/${currentReadingIndex.value}`);

  const { data: response, status, error, refresh } = await useAsyncData(() => useApiFetch<Word>(url.value), { immediate: true, watch: false });

  const getSortedReadings = () => {
    return response.value?.data?.alternativeReadings.sort((a, b) => b.frequencyPercentage - a.frequencyPercentage) || [];
  };

  const sortedReadings = computed(() => getSortedReadings());

  const mediaAmountUrl = 'media-deck/decks-count';
  const { data: mediaAmountResponse } = await useApiFetch<Record<MediaType, number>>(mediaAmountUrl);

  const switchReadingOrWord = async () => {
    exampleSentences.value = [];
    canLoadExampleSentences.value = true;
    getRandomExampleSentences();
    await refresh();
  }

  const selectReading = async (index: number) => {
    emit('readingSelected', index);
    currentReadingIndex.value = index;
    switchReadingOrWord();
  };

  watch(
    [() => props.wordId, () => props.readingIndex],
    async ([newWordId, newReadingIndex]) => {
      currentReadingIndex.value = newReadingIndex;
      switchReadingOrWord();
    },
    { immediate: false }
  );

  watch(
    () => response.value?.data?.mainReading.text,
    (newText) => {
      emit('mainReadingTextChanged', newText);
    },
    { immediate: true }
  );

  const conjugationString = computed(() => {
    let conjugations = [...props.conjugations];
    conjugations = conjugations.filter((conj) => !conj.startsWith('(')).filter((conj) => conj != '');
    conjugations.reverse();

    if (conjugations.length == 0) return null;

    return conjugations.join(' ; ');
  });

  const exampleSentences = ref<ExampleSentence[]>([]);
  const canLoadExampleSentences = ref(true);
  onMounted(() => {
    getRandomExampleSentences();
  });

  async function getRandomExampleSentences() {
    const alreadyLoaded = exampleSentences.value.map((sentence) => sentence.sourceDeck.deckId);
    const results = await $api<ExampleSentence[]>(`vocabulary/${props.wordId}/${currentReadingIndex.value}/random-example-sentences`, {
      method: 'POST',
      body: alreadyLoaded,
    });

    if (results.length == 0) {
      canLoadExampleSentences.value = false;
      return;
    }

    exampleSentences.value.push(...results);
  }
</script>

<template>
  <Card class="p-4">
    <template v-if="response?.data" #content>
      <div class="flex flex-col justify-between md:flex-row">
        <div class="flex flex-col gap-4 max-w-2xl">
          <div class="flex justify-between">
            <div>
              <div v-if="conjugationString != null" class="text-gray-500 text-xs font-noto-sans">(Conjugation: {{ conjugationString }})</div>
              <NuxtLink v-if="showRedirect" :to="`/vocabulary/${wordId}/${currentReadingIndex}`">
                <div class="text-3xl font-noto-sans" v-html="convertToRuby(response.data.mainReading.text)" />
              </NuxtLink>
              <div v-if="!showRedirect" class="text-3xl font-noto-sans" v-html="convertToRuby(response.data.mainReading.text)" />
            </div>
            <div class="text-gray-500 dark:text-gray-300 text-right md:hidden">Rank #{{ response.data.mainReading.frequencyRank.toLocaleString() }}</div>
          </div>

          <div>
            <h1 class="text-gray-500 dark:text-gray-300 text-sm">Meanings</h1>
            <div class="pl-2">
              <VocabularyDefinitions :definitions="response.data.definitions" :is-compact="false" />
            </div>
          </div>

          <div>
            <h1 class="text-gray-500 dark:text-gray-300 font-noto-sans text-sm">Readings</h1>
            <div class="pl-2 flex flex-row flex-wrap gap-8">
              <span v-for="reading in sortedReadings" :key="reading.readingIndex">
                <div :class="reading.readingIndex === currentReadingIndex ? 'font-bold !text-purple-500' : ' text-blue-500'">
                  <div class="text-center font-noto-sans cursor-pointer hover:underline" @click="selectReading(reading.readingIndex)">
                    {{ reading.text }}
                    <div class="text-xs">({{ formatPercentageApprox(reading.frequencyPercentage) }})</div>
                  </div>
                </div>
              </span>
            </div>
          </div>

          <ClientOnly>
            <div v-if="response.data.pitchAccents && response.data.pitchAccents.length > 0">
              <h1 class="text-gray-500 dark:text-gray-300 font-noto-sans text-sm">Pitch accents</h1>
              <div class="pl-2 flex flex-row flex-wrap gap-8">
                <span v-for="pitchAccent in response.data.pitchAccents" :key="pitchAccent">
                  <div>
                    <PitchDiagram :reading="response.data.mainReading.text" :pitch-accent="pitchAccent" />
                  </div>
                </span>
              </div>
            </div>
          </ClientOnly>
        </div>
        <div class="min-w-64">
          <div class="text-gray-500 dark:text-gray-300 text-right hidden md:block">Rank #{{ response.data.mainReading.frequencyRank }}</div>
          <div class="md:text-right pt-4">
            Appears in <b>{{ response.data.mainReading.usedInMediaAmount }} media</b>
          </div>
          <ClientOnly>
            <table v-if="response.data.mainReading.usedInMediaAmount > 0">
              <thead>
                <tr>
                  <th />
                  <th class="text-gray-500 dark:text-gray-300 text-sm pl-4">Amount</th>
                  <th class="text-gray-500 dark:text-gray-300 text-sm pl-4">% of total</th>
                </tr>
              </thead>
              <tr v-for="(amount, mediaType) in response.data.mainReading.usedInMediaAmountByType" :key="mediaType">
                <th class="text-right p-0.5 !font-bold">{{ getMediaTypeText(Number(mediaType)) }}</th>
                <th class="text-right p-0.5">{{ amount }}</th>
                <th class="text-right p-0.5">{{ mediaAmountResponse ? ((amount / mediaAmountResponse[mediaType as MediaType]) * 100).toFixed(0) : '0' }}%</th>
              </tr>
            </table>
          </ClientOnly>
        </div>
      </div>

      <ClientOnly>
        <div v-if="exampleSentences != null && exampleSentences.length > 0">
          <Accordion value="1" lazy>
            <AccordionPanel value="1">
              <AccordionHeader>
                <div class="cursor-pointer">Example sentences</div>
              </AccordionHeader>
              <AccordionContent>
                <div class="text-xs pb-2">Quotations belong to their original creators and are presented here for educational purposes only, as per the
                <NuxtLink
                  :to="`/terms`"
                  target="_blank"
                  class="hover:underline text-primary-600"
                >
                terms of service.
                </NuxtLink>
                </div>
                <ExampleSentenceEntry v-for="(exampleSentence, index) in exampleSentences" :key="index" :example-sentence="exampleSentence" />
                <Button @click="getRandomExampleSentences()" :disabled="!canLoadExampleSentences">Load more</Button>
              </AccordionContent>
            </AccordionPanel>
          </Accordion>
        </div>
      </ClientOnly>

      <Accordion v-if="response.data.mainReading.usedInMediaAmount > 0" value="0" lazy>
        <AccordionPanel value="1">
          <AccordionHeader>
            <div class="cursor-pointer">
              View the <b>{{ response.data.mainReading.usedInMediaAmount }}</b> media it appears in
            </div>
          </AccordionHeader>
          <AccordionContent>
            <MediaList :word="response.data" />
          </AccordionContent>
        </AccordionPanel>
      </Accordion>
    </template>
  </Card>
</template>

<style scoped>
  th {
    font-weight: normal;
  }
</style>
