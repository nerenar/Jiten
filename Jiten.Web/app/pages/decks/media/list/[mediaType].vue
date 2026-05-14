<script setup lang="ts">
  import { useRoute } from '#vue-router';
  import type { Deck } from '~/types';
  import { getMediaTypeText } from '~/utils/mediaTypeMapper';

  const route = useRoute();
  const mediaType = Number(route.params.mediaType);
  const url = computed(() => `media-deck/get-media-decks-by-type/${mediaType}`);

  const localiseTitle = useLocaliseTitle();

  const { data: response, status, error } = await useApiFetch<Deck>(url.value, {});
</script>

<template>
  <Card v-if="response">
    <template #title> List of decks for {{ getMediaTypeText(mediaType) }}</template>
    <template #content>
      <div v-if="!response?.length" class="flex flex-col items-center justify-center py-16">
        <i class="pi pi-search text-4xl text-primary-500 mb-4" />
        <p class="text-lg font-medium text-primary-700 dark:text-primary-300">No decks found</p>
        <p class="text-sm text-surface-400">No decks available for this media type</p>
      </div>
      <ul v-else>
        <li v-for="deck in response" :key="deck.deckId">
          <NuxtLink :to="`/decks/media/${deck.deckId}/detail`" target="_blank">{{ localiseTitle(deck) }}</NuxtLink>
        </li>
      </ul>
    </template>
  </Card>
</template>

<style scoped></style>
