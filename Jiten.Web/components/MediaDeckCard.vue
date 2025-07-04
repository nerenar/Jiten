<script setup lang="ts">
  import { type Deck, MediaType, type DeckCoverage } from '~/types';
  import Card from 'primevue/card';
  import { getChildrenCountText, getMediaTypeText } from '~/utils/mediaTypeMapper';
  import { getLinkTypeText } from '~/utils/linkTypeMapper';
  import { useJitenStore } from '~/stores/jitenStore';
  import CoverageDialog from '~/components/CoverageDialog.vue';
  import { formatDateAsYyyyMmDd } from '~/utils/formatDateAsYyyyMmDd';

  const props = defineProps<{
    deck: Deck;
    isCompact?: boolean;
    hideControl?: boolean;
  }>();

  const showDownloadDialog = ref(false);
  const showCoverageDialog = ref(false);
  const isLoadingCoverage = ref(false);
  const coverageData = ref<DeckCoverage | null>(null);
  const isDescriptionExpanded = ref(false);

  const store = useJitenStore();
  const { $api } = useNuxtApp();

  const displayAdminFunctions = computed(() => store.displayAdminFunctions);
  const readingSpeed = computed(() => store.readingSpeed);
  const readingDuration = computed(() => Math.round(props.deck.characterCount / readingSpeed.value));

  const sortedLinks = computed(() => {
    return [...props.deck.links].sort((a, b) => {
      const textA = getLinkTypeText(Number(a.linkType));
      const textB = getLinkTypeText(Number(b.linkType));
      return textA.localeCompare(textB);
    });
  });

  const fetchCoverage = async () => {
    isLoadingCoverage.value = true;
    try {
      const wordIds = store.getKnownWordIds();

      const bodyPayload = wordIds || [];

      const data = await $api<DeckCoverage>(`media-deck/${props.deck.deckId}/coverage`, {
        method: 'POST',
        body: JSON.stringify(bodyPayload),
        headers: {
          'Content-Type': 'application/json',
        },
      });
      coverageData.value = data;
      showCoverageDialog.value = true;
    } catch (error) {
      console.error('Error fetching coverage data:', error);
    } finally {
      isLoadingCoverage.value = false;
    }
  };

  const toggleDescription = () => {
    isDescriptionExpanded.value = !isDescriptionExpanded.value;
  };
</script>

<template>
  <Card class="p-2">
    <template #title>{{ localiseTitle(deck) }}</template>
    <template v-if="!isCompact" #subtitle>{{ getMediaTypeText(deck.mediaType) }}</template>
    <template #content>
      <div class="flex-gap-6">
        <div class="flex-1">
          <div class="flex flex-col md:flex-row gap-x-8 gap-y-2">
            <div v-if="!isCompact" class="text-left text-sm md:text-center">
              <img :src="deck.coverName == 'nocover.jpg' ? '/img/nocover.jpg' : deck.coverName" :alt="deck.originalTitle" class="h-48 w-34 min-w-34" />
              {{formatDateAsYyyyMmDd(new Date(deck.releaseDate)).replace(/-/g,'/')}}
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
                    <span v-if="deck.difficulty == 0" v-tooltip="'1/6'" class="ml-8 tabular-nums text-green-700 dark:text-green-300"> Beginner </span>
                    <span v-else-if="deck.difficulty == 1" v-tooltip="'2/6'" class="ml-8 tabular-nums text-green-500 dark:text-green-200"> Easy </span>
                    <span v-else-if="deck.difficulty == 2" v-tooltip="'3/6'" class="ml-8 tabular-nums text-yellow-600 dark:text-yellow-300"> Moderate </span>
                    <span v-else-if="deck.difficulty == 3" v-tooltip="'4/6'" class="ml-8 tabular-nums text-amber-600 dark:text-amber-300"> Hard </span>
                    <span v-else-if="deck.difficulty == 4" v-tooltip="'5/6'" class="ml-8 tabular-nums text-orange-600 dark:text-orange-300"> Very hard </span>
                    <span v-else-if="deck.difficulty == 5" v-tooltip="'6/6'" class="ml-8 tabular-nums text-red-600 dark:text-red-300"> Expert </span>
                  </div>
                </div>

                <div class="w-full md:w-64">
                  <div v-if="deck.dialoguePercentage != 0 && deck.dialoguePercentage != 100" class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Dialogue</span>
                    <span class="ml-8 tabular-nums">{{ deck.dialoguePercentage.toFixed(1) }}%</span>
                  </div>

                  <div v-if="deck.childrenDeckCount != 0" class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">{{ getChildrenCountText(deck.mediaType) }}</span>
                    <span class="ml-8 tabular-nums">{{ deck.childrenDeckCount.toLocaleString() }}</span>
                  </div>

                  <div
                    v-if="
                      deck.mediaType == MediaType.Novel ||
                      deck.mediaType == MediaType.NonFiction ||
                      deck.mediaType == MediaType.VisualNovel ||
                      deck.mediaType == MediaType.WebNovel
                    "
                    v-tooltip="'Based on your reading speed in the settings:\n ' + readingSpeed + ' characters per hour.'"
                    class="flex justify-between mb-2"
                  >
                    <span class="text-gray-600 dark:text-gray-300">Duration <i class="pi pi-info-circle cursor-pointer text-primary-500" /></span>
                    <span class="ml-8 tabular-nums">{{ readingDuration > 0 ? readingDuration : '<1' }} h</span>
                  </div>

                  <div v-if="deck.selectedWordOccurrences != 0" class="flex justify-between mb-2">
                    <span class="text-gray-600 dark:text-gray-300">Appears (times)</span>
                    <span class="ml-8 tabular-nums font-bold">{{ deck.selectedWordOccurrences.toLocaleString() }}</span>
                  </div>
                </div>
              </div>

              <div class="mt-2">
                <div v-if="deck.description" class="description-container" :class="{ expanded: isDescriptionExpanded }">
                  <p class="whitespace-pre-line mb-0 text-sm">{{ deck.description }}</p>
                  <a v-if="deck.description.length > 50" href="#" class="text-primary-500 hover:text-primary-700 text-sm" @click.prevent="toggleDescription">
                    {{ isDescriptionExpanded ? 'View less' : 'View more' }}
                  </a>
                </div>
              </div>

              <div class="mt-4 flex flex-col md:flex-row gap-4">
                <a v-for="link in sortedLinks" :key="link.url" :href="link.url" target="_blank">{{ getLinkTypeText(Number(link.linkType)) }}</a>
              </div>
              <div v-if="!hideControl" class="mt-4">
                <div class="flex flex-col md:flex-row gap-4">
                  <Button as="router-link" :to="`/decks/media/${deck.deckId}/detail`" label="View details" class="" />
                  <Button as="router-link" :to="`/decks/media/${deck.deckId}/vocabulary`" label="View vocabulary" class="" />
                  <Button label="Download deck" class="" @click="showDownloadDialog = true" />
                  <Button v-if="!isCompact" label="Coverage" class="" @click="fetchCoverage" />
                  <Button v-if="!isCompact && displayAdminFunctions" as="router-link" :to="`/dashboard/media/${deck.deckId}`" label="Edit" class="" />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
  </Card>

  <MediaDeckDownloadDialog :deck="deck" :visible="showDownloadDialog" @update:visible="showDownloadDialog = $event" />
  <CoverageDialog :visible="showCoverageDialog" :coverage="coverageData" :deck="deck" @update:visible="showCoverageDialog = $event" />

  <!-- Loading overlay -->
  <div v-if="isLoadingCoverage" class="fixed inset-0 flex items-center justify-center bg-black bg-opacity-50 z-50">
    <div class="text-center">
      <i class="pi pi-spin pi-spinner text-white text-5xl" />
      <div class="text-white mt-4 text-xl">Getting your coverage, this could take a few seconds...</div>
    </div>
  </div>
</template>

<style scoped>
  .description-container:not(.expanded) p {
    display: -webkit-box;
    line-clamp: 2;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  @media (max-width: 768px) {
    .description-container:not(.expanded) p {
      line-clamp: 4;
      -webkit-line-clamp: 4;
    }
  }

  .description-container.expanded p {
    white-space: pre-line;
  }
</style>
