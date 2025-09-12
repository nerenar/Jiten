<script async setup lang="ts">
  import Button from 'primevue/button';
  import { KnownState, type Word } from '~/types';
  import { useAuthStore } from '~/stores/authStore';

  const { $api } = useNuxtApp();
  const auth = useAuthStore();

  const props = defineProps<{
    word: Word;
  }>();

  const toggleWordKnown = async () => {
    if (props.word.knownState == KnownState.Known) {
      await $api<boolean>(`user/vocabulary/remove/${props.word.wordId}/${props.word.mainReading.readingIndex}`, {
        method: 'POST',
      });

      props.word.knownState = KnownState.Unknown;
    } else {
      await $api<boolean>(`user/vocabulary/add/${props.word.wordId}/${props.word.mainReading.readingIndex}`, {
        method: 'POST',
      });

      props.word.knownState = KnownState.Known;
    }
  };
</script>

<template>
  <ClientOnly>
    <span class="inline-flex items-center gap-1">
      <template v-if="auth.isAuthenticated">
        <template v-if="word.knownState == KnownState.Known">
          <span class="text-green-600 dark:text-green-300">Known</span>
          <Button icon="pi pi-minus" size="small" text severity="danger" @click="toggleWordKnown" />
          <span aria-hidden="true">|</span>
        </template>
        <template v-else>
          <Button icon="pi pi-plus" size="small" text severity="success" @click="toggleWordKnown" />
          <span aria-hidden="true">|</span>
        </template>
      </template>
    </span>
    <template #fallback>
      <span class="inline-flex items-center gap-1" aria-hidden="true"></span>
    </template>
  </ClientOnly>
</template>
