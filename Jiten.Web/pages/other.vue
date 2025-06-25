<script setup lang="ts">
  import { useApiFetch } from '~/composables/useApiFetch';
  import Button from 'primevue/button';
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
    <div>A global frequency list of all the words</div>
    <Button @click="downloadFile">Download Frequency List</Button>

    <div class="flex space-x-4">
      <NuxtLink to="/media-updates" no-rel>View a log of new media updates</NuxtLink>
    </div>

    <VocabularyImport />

    <div v-if="status === 'success'" class="pt-2">
      <div class="text-2xl font-bold">Global Stats</div>
      <b>{{ response.totalMojis?.toLocaleString() }}</b> characters in <b>{{ response.totalMedia?.toLocaleString() }}</b> media

      <div v-for="[mediaType, amount] in Object.entries(response.mediaByType)" :key="mediaType">
        <div class="text-sm">
          {{ getMediaTypeText(MediaType[mediaType]) }}: <b>{{ amount?.toLocaleString() }}</b>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped></style>
