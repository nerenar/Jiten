<script setup lang="ts">
  import { useApiFetch } from '~/composables/useApiFetch';
  import Button from 'primevue/button';
  import Card from 'primevue/card';
  import { type GlobalStats, MediaType, type Word } from '~/types';

  const frequencyListUrl = 'frequency-list/get-global-frequency-list';
  const { $api } = useNuxtApp();

  const downloadFile = async () => {
    try {
      const response = await $api(frequencyListUrl);
      if (response) {
        const data = typeof response === 'string' ? response : JSON.stringify(response);
        const blob = new Blob([data], { type: 'application/octet-stream' });
        const blobUrl = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = blobUrl;
        link.setAttribute('download', 'frequency_list.csv');
        document.body.appendChild(link);
        link.click();
        link.remove();
      } else {
        console.error('Error downloading file:');
      }
    } catch (err) {
      console.error('Error:', err);
    }
  };

  const globalStatsUrl = 'stats/get-global-stats';
  const { data: response, status, error } = await useApiFetch<GlobalStats>(globalStatsUrl);
</script>

<template>
  <div>
    <Card class="mb-4">
      <template #title>Frequency List</template>
      <template #content>
        <div class="mb-3">A global frequency list of all the words</div>
        <Button class="p-button-primary" @click="downloadFile">
          <Icon name="material-symbols-light:download" size="1.5em" />
          Download Frequency List
        </Button>
      </template>
    </Card>

    <Card class="mb-4">
      <template #title>Tools</template>
      <template #content>
        <div class="flex flex-col sm:flex-row gap-3">
          <Button as="router-link" to="/media-updates" severity="info">
            <Icon name="material-symbols-light:breaking-news-alt-1-outline" class="mr-2" />
            View Media Updates
          </Button>
          <Button as="router-link" to="/parse-deck" severity="info">
            <Icon name="material-symbols-light:cards-star-outline" class="mr-2" />
            Create Custom Deck
          </Button>
        </div>
      </template>
    </Card>

    <VocabularyImport />

    <Card v-if="status === 'success'" class="mt-4">
      <template #title>
        <div class="flex items-center">
          <Icon name="material-symbols-light:bar-chart" class="mr-2 text-primary" size="1.5em" />
          Global Stats
        </div>
      </template>
      <template #content>
        <div class="mb-3">
          <b>{{ response.totalMojis?.toLocaleString() }}</b> characters in <b>{{ response.totalMedia?.toLocaleString() }}</b> media
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div v-for="[mediaType, amount] in Object.entries(response.mediaByType)" :key="mediaType" class="p-2 border rounded-md">
            <div class="font-medium">
              {{ getMediaTypeText(MediaType[mediaType]) }}
            </div>
            <div class="text-lg font-bold text-primary-600">
              {{ amount?.toLocaleString() }}
            </div>
          </div>
        </div>
      </template>
    </Card>
  </div>
</template>

<style scoped></style>
