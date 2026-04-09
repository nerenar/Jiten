<script setup lang="ts">
  import { storeToRefs } from 'pinia';
  import { useJitenStore } from '~/stores/jitenStore';
  import { useAuthStore } from '~/stores/authStore';

  const store = useJitenStore();
  const {
    titleLanguage,
    displayFurigana,
    displayAllNsfw,
    hideVocabularyDefinitions,
    hideCoverageBorders,
    hideGenres,
    hideTags,
    hideRelations,
    hideDescriptions,
    quickMasterVocabulary,
    displayAdminFunctions,
    readingSpeed,
    difficultyDisplayStyle,
    difficultyValueDisplayStyle,
    ttsVoice,
  } = storeToRefs(store);
  const auth = useAuthStore();

  const settings = ref();
  const isOverSettings = ref(false);
  const isSettingsInteracted = ref(false);

  const titleLanguageOptions = ref([
    { label: 'Japanese', value: 0 },
    { label: 'Romaji', value: 1 },
    { label: 'English', value: 2 },
  ]);

  const ttsVoiceOptions = ref([
    { label: 'Female', value: 'female' },
    { label: 'Male', value: 'male' },
    { label: 'ASMR', value: 'asmr' },
    { label: 'System', value: 'system' },
  ]);

  const difficultyDisplayStyleOptions = ref([
    { label: 'Name only', value: 0 },
    { label: 'Name and value', value: 1 },
    { label: 'Value only', value: 2 },
  ]);

  const difficultyValueDisplayStyleOptions = ref([
    { label: '0 to 5', value: 1 },
    { label: 'Percentage', value: 2 },
  ]);

  const onSettingsMouseEnter = () => {
    isOverSettings.value = true;
  };

  const onSettingsMouseLeave = () => {
    isOverSettings.value = false;
    setTimeout(() => {
      if (!isOverSettings.value && !isSettingsInteracted.value) {
        settings.value.hide();
      }
    }, 750);
  };

  const toggle = (event: boolean) => {
    settings.value.toggle(event);
  };

  const show = (event: boolean) => {
    settings.value.show(event);
  };

  const hide = () => {
    settings.value.hide();
  };

  defineExpose({ toggle, show, hide });
</script>

<template>
  <Popover ref="settings" @mouseenter="onSettingsMouseEnter" @mouseleave="onSettingsMouseLeave" :pt="{ root: { class: 'w-[90vw] max-w-sm md:w-auto' }, content: { class: 'p-3 md:p-4 max-h-[80vh] overflow-y-auto' } }">
    <div class="flex flex-col gap-2">
      <div class="flex justify-between items-center mb-2 md:hidden">
        <span class="font-semibold text-base">Settings</span>
        <Button icon="pi pi-times" text rounded size="small" @click="settings.hide()" aria-label="Close settings" />
      </div>
      <FloatLabel variant="on" class="">
        <Select
          v-model="titleLanguage"
          :options="titleLanguageOptions"
          option-label="label"
          option-value="value"
          placeholder="Titles Language"
          input-id="titleLanguage"
          @show="isSettingsInteracted = true"
          @hide="isSettingsInteracted = false"
        />
        <label for="titleLanguage">Titles Language</label>
      </FloatLabel>

      <FloatLabel variant="on">
        <Select
          v-model="ttsVoice"
          :options="ttsVoiceOptions"
          option-label="label"
          option-value="value"
          placeholder="TTS Voice"
          input-id="ttsVoice"
          @show="isSettingsInteracted = true"
          @hide="isSettingsInteracted = false"
        />
        <label for="ttsVoice">TTS Voice</label>
      </FloatLabel>

      <Divider class="!my-1 md:!my-2 !mx-2" />

      <div class="flex flex-col gap-2 md:gap-4">
        <label for="readingSpeed" class="text-sm font-medium">Reading Speed (chars/hour)</label>
        <div class="w-full">
          <InputNumber v-model="readingSpeed" show-buttons :min="100" :max="100000" :step="100" size="small" class="w-full" fluid />
        </div>
        <div class="w-full px-1">
          <Slider v-model="readingSpeed" :min="100" :max="100000" :step="100" class="w-full" />
        </div>
      </div>

      <Divider class="!my-1 md:!my-2 !mx-2" />

      <div class="flex items-center gap-2 py-1">
        <Checkbox v-model="displayFurigana" input-id="displayFurigana" name="furigana" :binary="true" />
        <label for="displayFurigana" class="text-sm cursor-pointer">Display Furigana</label>
      </div>

      <div class="flex items-center gap-2 py-1">
        <Checkbox v-model="hideVocabularyDefinitions" input-id="hideVocabularyDefinitions" name="hideVocabularyDefinitions" :binary="true" />
        <label for="hideVocabularyDefinitions" class="text-sm cursor-pointer">Hide Vocabulary Definitions</label>
      </div>

      <div v-if="auth.isAuthenticated" class="flex items-center gap-2 py-1">
        <Checkbox v-model="quickMasterVocabulary" input-id="quickMasterVocabulary" name="quickMasterVocabulary" :binary="true" />
        <label for="quickMasterVocabulary" class="text-sm cursor-pointer">Master in 1 click</label>
      </div>

      <div class="flex items-center gap-2 py-1">
        <Checkbox v-model="displayAllNsfw" input-id="displayAllNsfw" name="nsfw" :binary="true" />
        <label for="displayAllNsfw" class="text-sm cursor-pointer">Unblur all NSFW sentences</label>
      </div>

      <div v-if="auth.isAuthenticated" class="flex items-center gap-2 py-1">
        <Checkbox v-model="hideCoverageBorders" input-id="hideCoverageBorders" name="hideCoverageBorders" :binary="true" />
        <label for="hideCoverageBorders" class="text-sm cursor-pointer">Hide coverage borders</label>
      </div>

      <div class="flex items-center gap-2 py-1">
        <Checkbox v-model="hideGenres" input-id="hideGenres" name="hideGenres" :binary="true" />
        <label for="hideGenres" class="text-sm cursor-pointer">Hide genres</label>
      </div>

      <div class="flex items-center gap-2 py-1">
        <Checkbox v-model="hideTags" input-id="hideTags" name="hideTags" :binary="true" />
        <label for="hideTags" class="text-sm cursor-pointer">Hide tags</label>
      </div>

      <div class="flex items-center gap-2 py-1">
        <Checkbox v-model="hideRelations" input-id="hideRelations" name="hideRelations" :binary="true" />
        <label for="hideRelations" class="text-sm cursor-pointer">Hide relations</label>
      </div>

      <div class="flex items-center gap-2 py-1">
        <Checkbox v-model="hideDescriptions" input-id="hideDescriptions" name="hideDescriptions" :binary="true" />
        <label for="hideDescriptions" class="text-sm cursor-pointer">Hide descriptions</label>
      </div>

      <div v-if="auth.isAuthenticated && auth.isAdmin" class="flex items-center gap-2 py-1">
        <Checkbox v-model="displayAdminFunctions" input-id="displayAdminFunctions" name="adminFunctions" :binary="true" />
        <label for="displayAdminFunctions" class="text-sm cursor-pointer">Display admin functions</label>
      </div>

      <Divider class="!my-1 md:!my-2 !mx-2" />

      <FloatLabel variant="on" class="">
        <Select
          v-model="difficultyDisplayStyle"
          :options="difficultyDisplayStyleOptions"
          option-label="label"
          option-value="value"
          placeholder="Difficulty Style"
          input-id="difficultyDisplayStyle"
          @show="isSettingsInteracted = true"
          @hide="isSettingsInteracted = false"
        />
        <label for="difficultyDisplayStyle">Difficulty Style</label>
      </FloatLabel>

      <FloatLabel variant="on" class="">
        <Select
          v-model="difficultyValueDisplayStyle"
          :options="difficultyValueDisplayStyleOptions"
          option-label="label"
          option-value="value"
          placeholder="Difficulty Value Style"
          input-id="difficultyValueDisplayStyle"
          @show="isSettingsInteracted = true"
          @hide="isSettingsInteracted = false"
        />
        <label for="difficultyValueDisplayStyle">Difficulty Value Style</label>
      </FloatLabel>
    </div>
  </Popover>
</template>
