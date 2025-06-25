<script setup lang="ts">
  import { defineProps, defineEmits } from 'vue';
  import { type DeckCoverage } from '~/types';
  import Dialog from 'primevue/dialog';

  const props = defineProps<{
    visible: boolean;
    deck: Deck;
    coverage: DeckCoverage | null;
  }>();

  const emit = defineEmits(['update:visible']);

  const localVisible = ref(props.visible);

  watch(
    () => props.visible,
    (newVal) => {
      localVisible.value = newVal;
    }
  );

  watch(localVisible, (newVal) => {
    emit('update:visible', newVal);
  });


  const closeDialog = () => {
    emit('update:visible', false);
  };
</script>

<template>
  <Dialog
    v-model:visible="localVisible"
    modal
    header="Media Coverage"
    :style="{ width: '80%', maxWidth: '600px' }"
    :dismissableMask="true"
    @hide="closeDialog"
  >
    <div class="p-4">
      <div class="mb-6">
        <h3 class="text-xl font-semibold mb-2">{{ localiseTitle(deck) }}</h3>
        <p class="text-gray-600 dark:text-gray-300">This shows how much of the media content you already know based on your vocabulary.</p>
      </div>

      <div class="flex flex-col gap-6">
        <div class="flex flex-col">
          <h4 class="text-lg font-medium mb-2">Word Coverage</h4>
          <div class="relative h-8 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden mb-2">
            <div class="absolute top-0 left-0 h-full bg-primary-500 rounded-full" :style="{ width: `${props.coverage?.knownWordPercentage || 0}%` }"></div>
          </div>
          <div class="text-sm text-gray-600 dark:text-gray-300">
            {{ props.coverage?.knownWordsOccurrences.toLocaleString() }} / {{ props.coverage?.totalWordCount.toLocaleString() }} words
          </div>
          <div class="text-lg font-bold">{{ props.coverage?.knownWordPercentage.toFixed(2) }}%</div>
        </div>

        <div class="flex flex-col">
          <h4 class="text-lg font-medium mb-2">Unique Word Coverage</h4>
          <div class="relative h-8 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden mb-2">
            <div class="absolute top-0 left-0 h-full bg-green-500 rounded-full" :style="{ width: `${props.coverage?.knownUniqueWordPercentage || 0}%` }"></div>
          </div>
          <div class="text-sm text-gray-600 dark:text-gray-300">
            {{ props.coverage?.knownUniqueWordCount.toLocaleString() }} / {{ props.coverage?.uniqueWordCount.toLocaleString() }} unique words
          </div>
          <div class="text-lg font-bold">{{ props.coverage?.knownUniqueWordPercentage.toFixed(2) }}%</div>
        </div>
      </div>
    </div>
  </Dialog>
</template>

<style scoped></style>
