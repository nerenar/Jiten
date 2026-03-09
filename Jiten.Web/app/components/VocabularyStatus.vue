<script setup lang="ts">
  import Button from 'primevue/button';
  import Popover from 'primevue/popover';
  import { KnownState, type Word } from '~/types';
  import { useAuthStore } from '~/stores/authStore';

  const { $api } = useNuxtApp();
  const auth = useAuthStore();

  const props = defineProps<{
    word: Word;
    knownStatesOverride?: KnownState[];
  }>();

  const knownStates = ref([...(props.knownStatesOverride ?? props.word.knownStates ?? [])]);
  const op = ref();

  watch([() => props.knownStatesOverride, () => props.word.knownStates], ([override, wordStates]) => {
    knownStates.value = [...(override ?? wordStates ?? [])];
  });

  const wordPath = computed(() => `${props.word.wordId}/${props.word.mainReading.readingIndex}`);

  const isBlacklisted = computed(() => knownStates.value.includes(KnownState.Blacklisted));

  const masterWord = async () => {
    op.value?.hide();
    await $api<boolean>(`user/vocabulary/add/${wordPath.value}`, { method: 'POST' });
    knownStates.value = [KnownState.Mastered];
  };

  const blacklistWord = async () => {
    op.value?.hide();
    await $api<boolean>(`user/vocabulary/blacklist/${wordPath.value}`, { method: 'POST' });
    knownStates.value = [KnownState.Blacklisted];
  };

  const onPlusClick = (e: MouseEvent) => {
    if (e.shiftKey) masterWord();
    else if (e.ctrlKey) blacklistWord();
    else op.value?.toggle(e);
  };

  const removeWord = async () => {
    await $api<boolean>(`user/vocabulary/remove/${wordPath.value}`, { method: 'POST' });
    knownStates.value = [KnownState.New];
  };
</script>

<template>
  <ClientOnly>
    <span class="inline-flex items-center gap-1">
      <template v-if="auth.isAuthenticated">
        <template v-if="knownStates.includes(KnownState.Mature)">
          <span class="text-green-600 dark:text-green-300">Mature</span>
          <Button icon="pi pi-minus" size="small" text severity="danger" @click="removeWord" />
          <span aria-hidden="true">|</span>
        </template>
        <template v-else-if="knownStates.includes(KnownState.Mastered)">
          <span class="text-green-600 dark:text-green-300">Mastered</span>
          <Button icon="pi pi-minus" size="small" text severity="danger" @click="removeWord" />
          <span aria-hidden="true">|</span>
        </template>
        <template v-else-if="knownStates.includes(KnownState.Young)">
          <span class="text-yellow-600 dark:text-yellow-300">Young</span>
          <Button icon="pi pi-minus" size="small" text severity="danger" @click="removeWord" />
          <span aria-hidden="true">|</span>
        </template>
        <template v-else-if="isBlacklisted">
          <span class="text-gray-600 dark:text-gray-300">Blacklisted</span>
          <Button icon="pi pi-minus" size="small" text severity="danger" @click="removeWord" />
          <span aria-hidden="true">|</span>
        </template>
        <template v-else>
          <Tooltip content="Shift+Click: Master&#10;Ctrl+Click: Blacklist">
            <Button icon="pi pi-plus" size="small" text severity="success" @click="onPlusClick" />
          </Tooltip>
          <span aria-hidden="true">|</span>
        </template>
        <Popover ref="op" :pt="{ content: { class: 'p-1' } }">
          <div class="flex flex-col">
            <button class="flex items-center gap-2 rounded-md px-3 py-1.5 text-sm text-green-600 hover:bg-green-50 dark:hover:bg-green-900/20 cursor-pointer" @click="masterWord">
              <i class="pi pi-check w-4 text-center" /><span>Master</span>
            </button>
            <button class="flex items-center gap-2 rounded-md px-3 py-1.5 text-sm text-surface-600 dark:text-surface-400 hover:bg-surface-100 dark:hover:bg-surface-700 cursor-pointer" @click="blacklistWord">
              <i class="pi pi-ban w-4 text-center" /><span>Blacklist</span>
            </button>
          </div>
        </Popover>
      </template>
    </span>
    <template #fallback>
      <span class="inline-flex items-center gap-1" aria-hidden="true"></span>
    </template>
  </ClientOnly>
</template>
