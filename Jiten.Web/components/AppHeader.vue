<script setup lang="ts">
  import Button from 'primevue/button';

  import { useJitenStore } from '~/stores/jitenStore';

  const store = useJitenStore();
  const tokenCookie = useCookie('token');

  onMounted(() => {
    if (store.darkMode) {
      document.documentElement.classList.add('dark-mode');
    }
  });

  function toggleDarkMode() {
    document.documentElement.classList.toggle('dark-mode');
    store.darkMode = !store.darkMode;
  }

  const settings = ref();

  const titleLanguageOptions = ref([
    { label: 'Japanese', value: 0 },
    { label: 'Romaji', value: 1 },
    { label: 'English', value: 2 },
  ]);

  const titleLanguage = computed({
    get: () => store.titleLanguage,
    set: (value) => (store.titleLanguage = value),
  });

  const displayFurigana = computed({
    get: () => store.displayFurigana,
    set: (value) => (store.displayFurigana = value),
  });

  const displayAllNsfw = computed({
    get: () => store.displayAllNsfw,
    set: (value) => (store.displayAllNsfw = value),
  });

  const hideVocabularyDefinitions = computed({
    get: () => store.hideVocabularyDefinitions,
    set: (value) => (store.hideVocabularyDefinitions = value),
  })

  const isAdmin = computed(() => {
    return tokenCookie.value !== null && tokenCookie.value !== undefined && tokenCookie.value !== '';
  });

  const displayAdminFunctions = computed({
    get: () => store.displayAdminFunctions,
    set: (value) => (store.displayAdminFunctions = value),
  });

  const readingSpeed = computed({
    get: () => store.readingSpeed,
    set: (value) => (store.readingSpeed = value),
  });

  const isOverSettings = ref(false);
  const isSettingsInteracted = ref(false);

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

  const toggleSettings = (event: boolean) => {
    settings.value.toggle(event);
  };

  const showSettings = (event: boolean) => {
    settings.value.show(event);
  };
</script>

<template>
  <header>
    <div class="bg-indigo-900">
      <div class="flex justify-between items-center mb-6 mx-auto p-4 max-w-6xl">
        <NuxtLink to="/" class="!no-underline">
          <h1 class="text-2xl font-bold text-white">Jiten <span class="text-red-600 text-xs align-super">beta</span></h1>
        </NuxtLink>
        <nav class="space-x-6">
          <nuxt-link to="/" class="!text-white">Home</nuxt-link>
          <nuxt-link to="/decks/media" class="!text-white">Media</nuxt-link>
          <nuxt-link to="/other" class="!text-white">Other</nuxt-link>
          <nuxt-link to="/faq" class="!text-white">FAQ</nuxt-link>
          <nuxt-link v-if="store.displayAdminFunctions" to="/Dashboard" class="!text-white">Dashboard</nuxt-link>
          <Button
            type="button"
            label="Share"
            severity="secondary"
            @mouseover="showSettings($event)"
            @mouseleave="onSettingsMouseLeave"
            @click="toggleSettings($event)"
          >
            <Icon name="material-symbols-light:settings" />
          </Button>

          <Button label="Toggle Dark Mode" severity="secondary" @click="toggleDarkMode()">
            <Icon name="line-md:light-dark" />
          </Button>
        </nav>
      </div>
    </div>
  </header>

  <Popover ref="settings" @mouseenter="onSettingsMouseEnter" @mouseleave="onSettingsMouseLeave">
    <div class="flex flex-col gap-2">
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

      <Divider class="!m-2" />

      <div class="flex flex-col gap-4">
        <label for="readingSpeed" class="text-sm font-medium">Reading Speed (chars/hour)</label>
        <div class="w-full">
          <InputNumber v-model="readingSpeed" show-buttons :min="100" :max="100000" :step="100" size="small" class="w-full" />
        </div>
        <div class="w-full">
          <Slider v-model="readingSpeed" :min="100" :max="100000" :step="100" class="w-full" />
        </div>
      </div>

      <Divider class="!m-2" />

      <div class="flex items-center gap-2">
        <Checkbox v-model="displayFurigana" input-id="displayFurigana" name="furigana" :binary="true" />
        <label for="displayFurigana">Display Furigana</label>
      </div>

      <div class="flex items-center gap-2">
        <Checkbox v-model="hideVocabularyDefinitions" input-id="hideVocabularyDefinitions" name="hideVocabularyDefinitions" :binary="true" />
        <label for="displayFurigana">Hide Vocabulary Definitions</label>
      </div>

      <div class="flex items-center gap-2">
        <Checkbox v-model="displayAllNsfw" input-id="displayAllNsfw" name="nsfw" :binary="true" />
        <label for="displayAllNsfw">Unblur all NSFW sentences</label>
      </div>

      <div v-if="isAdmin" class="flex items-center gap-2">
        <Checkbox v-model="displayAdminFunctions" input-id="displayAdminFunctions" name="adminFunctions" :binary="true" />
        <label for="displayAdminFunctions">Display admin functions</label>
      </div>
    </div>
  </Popover>
</template>

<style scoped></style>
