<script setup lang="ts">
  import type { Deck } from '~/types';
  import { getMediaTypeText } from '~/utils/mediaTypeMapper';
  import { localiseTitle } from '~/utils/localiseTitle';
  import MediaDeckDownloadDialog from '~/components/MediaDeckDownloadDialog.vue';

  const props = defineProps<{
    deck: Deck;
  }>();

  const showDownloadDialog = ref(false);
</script>

<template>
  <Card :pt="{ body: { style: 'padding: 0.5rem' } }">
    <template #content>
      <div class="flex flex-row items-center">
        <!-- Title and Media Type -->
        <div class="flex-grow">
          <div class="font-bold">{{ localiseTitle(deck).substring(0,50) }}</div>
          <div class="text-xs text-gray-500">{{ getMediaTypeText(deck.mediaType) }}</div>
        </div>

        <!-- Key Stats -->
        <div class="flex gap-3 mx-3">
          <div class="flex flex-col items-center">
            <div class="text-xs text-gray-600 dark:text-gray-300">Characters</div>
            <div class="font-medium tabular-nums">{{ deck.characterCount.toLocaleString() }}</div>
          </div>

          <div class="flex flex-col items-center">
            <div class="text-xs text-gray-600 dark:text-gray-300">Words</div>
            <div class="font-medium tabular-nums">{{ deck.wordCount.toLocaleString() }}</div>
          </div>

          <div class="flex flex-col items-center">
            <div class="text-xs text-gray-600 dark:text-gray-300">Uniq Words</div>
            <div class="font-medium tabular-nums">{{ deck.uniqueWordCount.toLocaleString() }}</div>
          </div>

          <div class="flex flex-col items-center">
            <div class="text-xs text-gray-600 dark:text-gray-300">Kanji</div>
            <div class="font-medium tabular-nums">{{ deck.uniqueKanjiCount.toLocaleString() }}</div>
          </div>

          <div v-if="deck.averageSentenceLength !== 0" class="flex flex-col items-center">
            <div class="text-xs text-gray-600 dark:text-gray-300">Avg sentence</div>
            <div class="font-medium tabular-nums">{{ deck.averageSentenceLength.toFixed(1) }}</div>
          </div>

          <div v-if="deck.difficulty != 0" class="flex flex-col items-center">
            <div class="text-xs text-gray-600 dark:text-gray-300">Difficulty</div>
            <div v-if="deck.difficulty == 0" class="tabular-nums text-green-700 dark:text-green-300"> ★☆☆☆☆☆ </div>
            <div v-else-if="deck.difficulty == 1" class="font-medium tabular-nums text-green-500"> ★★☆☆☆☆ </div>
            <div v-else-if="deck.difficulty == 2" class="font-medium tabular-nums text-yellow-600"> ★★★☆☆☆ </div>
            <div v-else-if="deck.difficulty == 3" class="font-medium tabular-nums text-amber-600"> ★★★★☆☆ </div>
            <div v-else-if="deck.difficulty == 4" class="font-medium tabular-nums text-orange-600"> ★★★★★☆ </div>
            <div v-else-if="deck.difficulty == 5" class="font-medium tabular-nums text-red-600"> ★★★★★★ </div>
          </div>
        </div>

        <!-- Action Buttons -->
        <div class="flex gap-0.5">
          <Button
            v-tooltip="'View details'"
            as="router-link"
            :to="`/decks/media/${deck.deckId}/detail`"
            size="small"
            class="p-button-sm"
          >
            <Icon name="material-symbols:info-outline" size="1.5em" />
          </Button>
          <Button
            v-tooltip="'View vocabulary'"
            as="router-link"
            :to="`/decks/media/${deck.deckId}/vocabulary`"
            size="small"
            class="p-button-sm"
          >
            <Icon name="material-symbols:menu-book-outline" size="1.5em" />
          </Button>
          <Button v-tooltip="'Download deck'" size="small" class="p-button-sm" @click="showDownloadDialog = true">
            <Icon name="material-symbols:download" size="1.5em" />
          </Button>
        </div>
      </div>
    </template>
  </Card>

  <MediaDeckDownloadDialog :deck="deck" :visible="showDownloadDialog" @update:visible="showDownloadDialog = $event" />
</template>

<style scoped></style>
