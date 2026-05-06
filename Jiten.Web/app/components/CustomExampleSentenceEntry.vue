<script setup lang="ts">
  import type { UserExampleSentenceDto } from '~/types';

  const props = defineProps<{
    sentence: UserExampleSentenceDto;
  }>();

  const formattedText = computed(() => parseCustomSentenceHtml(props.sentence.text));
</script>

<template>
  <div class="flex flex-col">
    <blockquote class="relative inline-block border-l-4 border-yellow-500 pl-5 pr-3 py-3 bg-gray-50 dark:bg-gray-900 rounded-r shadow-sm overflow-hidden">
      <div v-html="formattedText" class="md:text-lg text-sm" />
    </blockquote>
    <div v-if="sentence.source" class="flex items-center mb-2">
      <span class="text-xs italic mr-2 ml-4">Source:</span>
      <span class="text-xs">{{ sentence.source }}</span>
    </div>
  </div>
</template>
