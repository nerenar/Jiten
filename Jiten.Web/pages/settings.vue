<script setup lang="ts">
  import SettingsVocabularyImport from '~/components/SettingsVocabularyImport.vue';
  import SettingsCoverage from '~/components/SettingsCoverage.vue';

  definePageMeta({
    middleware: ['auth'],
  });
</script>

<template>
  <div class="container mx-auto p-2 md:p-4">
    <SettingsCoverage class="mb-4" />
    <SettingsVocabularyImport />
  </div>
</template>

<style scoped></style>
