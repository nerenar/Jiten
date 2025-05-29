<script setup lang="ts">
  import type { Deck } from '~/types';
  import Card from 'primevue/card';
  import { getChildrenCountText, getMediaTypeText } from '~/utils/mediaTypeMapper';
  import { getLinkTypeText } from '~/utils/linkTypeMapper';
  import { useJitenStore } from '~/stores/jitenStore';

  const props = defineProps<{
    deck: Deck;
    isCompact?: boolean;
  }>();

  const showDownloadDialog = ref(false);

  const store = useJitenStore();

  const displayAdminFunctions = computed(() => store.displayAdminFunctions);

  const sortedLinks = computed(() => {
    return [...props.deck.links].sort((a, b) => {
      const textA = getLinkTypeText(Number(a.linkType));
      const textB = getLinkTypeText(Number(b.linkType));
      return textA.localeCompare(textB);
    });
  });
</script>

<template>
  <Card class="p-2">
    <template #title>{{ localiseTitle(deck) }}</template>
    <template #subtitle v-if="!isCompact">{{ getMediaTypeText(deck.mediaType) }}</template>
    <template #content>
      <div class="flex-gap-6">
        <div class="flex-1">
          <div class="flex flex-col md:flex-row gap-x-8 gap-y-2">
            <div v-if="!isCompact">
              <img :src="deck.coverName == 'nocover.jpg' ? '/img/nocover.jpg' : deck.coverName" :alt="deck.originalTitle" class="h-48 w-34 min-w-34" />
            </div>
            <div>
              <div class="flex flex-col gap-x-8 gap-y-2" :class="isCompact ? '' : 'md:flex-row'">
                <div class="w-full md:w-64">
                  <div class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Character count</span>
                    <span class="ml-8 tabular-nums">{{ deck.characterCount.toLocaleString() }}</span>
                  </div>
                  <div class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Word count</span>
                    <span class="ml-8 tabular-nums">{{ deck.wordCount.toLocaleString() }}</span>
                  </div>
                  <div class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Unique words</span>
                    <span class="ml-8 tabular-nums">{{ deck.uniqueWordCount.toLocaleString() }}</span>
                  </div>
                  <div class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Unique words used once</span>
                    <span class="ml-8 tabular-nums">{{ deck.uniqueWordUsedOnceCount.toLocaleString() }}</span>
                  </div>
                </div>

                <div class="w-full md:w-64">
                  <div class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Unique kanji</span>
                    <span class="ml-8 tabular-nums">{{ deck.uniqueKanjiCount.toLocaleString() }}</span>
                  </div>
                  <div class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Unique kanji used once</span>
                    <span class="ml-8 tabular-nums">{{ deck.uniqueKanjiUsedOnceCount.toLocaleString() }}</span>
                  </div>
                  <div v-if="deck.averageSentenceLength !== 0" class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Average sentence length</span>
                    <span class="ml-8 tabular-nums">{{ deck.averageSentenceLength.toFixed(1) }}</span>
                  </div>
                  <div v-if="deck.difficulty != -1" class="flex justify-between mb-2">
                    <span
                      v-tooltip="
                        'This is a work in progress.\nIf you find scores that are way higher or lower than they should be, please report them so the algorithm can be refined further.'
                      "
                      class="text-gray-600 dark:text-gray-300"
                    >
                      Difficulty
                      <span class="text-purple-500 text-xs align-super"> beta </span>
                    </span>
                    <span v-if="deck.difficulty == 0" class="ml-8 tabular-nums text-green-700 dark:text-green-300"> Beginner </span>
                    <span v-else-if="deck.difficulty == 1" class="ml-8 tabular-nums text-green-500 dark:text-green-200"> Easy </span>
                    <span v-else-if="deck.difficulty == 2" class="ml-8 tabular-nums text-yellow-600 dark:text-yellow-300"> Moderate </span>
                    <span v-else-if="deck.difficulty == 3" class="ml-8 tabular-nums text-amber-600 dark:text-amber-300"> Challenging </span>
                    <span v-else-if="deck.difficulty == 4" class="ml-8 tabular-nums text-orange-600 dark:text-orange-300"> Advanced </span>
                    <span v-else-if="deck.difficulty == 5" class="ml-8 tabular-nums text-red-600 dark:text-red-300"> Expert </span>
                  </div>
                </div>

                <div class="w-full md:w-50">
                  <div v-if="deck.dialoguePercentage != 0 && deck.dialoguePercentage != 100" class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Dialogue (%)</span>
                    <span class="ml-8 tabular-nums">{{ deck.dialoguePercentage.toFixed(1) }}</span>
                  </div>

                  <div v-if="deck.childrenDeckCount != 0" class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">{{ getChildrenCountText(deck.mediaType) }}</span>
                    <span class="ml-8 tabular-nums">{{ deck.childrenDeckCount.toLocaleString() }}</span>
                  </div>

                  <div v-if="deck.selectedWordOccurrences != 0" class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Appears (times)</span>
                    <span class="ml-8 tabular-nums font-bold">{{ deck.selectedWordOccurrences.toLocaleString() }}</span>
                  </div>
                </div>
              </div>

              <div class="mt-4 flex flex-col md:flex-row gap-4">
                <a v-for="link in sortedLinks" :key="link.url" :href="link.url" target="_blank">{{ getLinkTypeText(Number(link.linkType)) }}</a>
              </div>
              <div class="mt-4">
                <div class="flex flex-col md:flex-row gap-4">
                  <Button as="router-link" :to="`/decks/media/${deck.deckId}/detail`" label="View details" class="" />
                  <Button as="router-link" :to="`/decks/media/${deck.deckId}/vocabulary`" label="View vocabulary" class="" />
                  <Button @click="showDownloadDialog = true" label="Download deck" class="" />
                  <Button v-if="displayAdminFunctions" as="router-link" :to="`/dashboard/media/${deck.deckId}`" label="Edit" class="" />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
  </Card>

  <MediaDeckDownloadDialog :deck="deck" :visible="showDownloadDialog" @update:visible="showDownloadDialog = $event" />
</template>

<style scoped></style>
