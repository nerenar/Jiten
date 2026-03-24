<script setup lang="ts">
  import { useSrsStore } from '~/stores/srsStore';
  import { useToast } from 'primevue/usetoast';

  const props = defineProps<{ visible: boolean }>();
  const emit = defineEmits(['update:visible']);

  const localVisible = ref(props.visible);
  watch(() => props.visible, (v) => { localVisible.value = v; });
  watch(localVisible, (v) => { emit('update:visible', v); });

  const srsStore = useSrsStore();
  const toast = useToast();

  type Step = 'upload' | 'preview' | 'done';
  const step = ref<Step>('upload');
  const uploading = ref(false);
  const committing = ref(false);

  const selectedFile = ref<File | null>(null);
  const previewToken = ref('');
  const matched = ref<{ wordId: number; readingIndex: number; text: string; reading: string }[]>([]);
  const unmatched = ref<string[]>([]);
  const totalLines = ref(0);

  const deckName = ref('');
  const deckDescription = ref('');
  const excludedWordIds = ref(new Set<number>());

  function toggleExclude(wordId: number) {
    const s = new Set(excludedWordIds.value);
    if (s.has(wordId)) s.delete(wordId);
    else s.add(wordId);
    excludedWordIds.value = s;
  }

  const includedCount = computed(() => matched.value.length - excludedWordIds.value.size);

  function onFileSelect(event: any) {
    const files = event.target?.files || event.files;
    if (files && files.length > 0) {
      selectedFile.value = files[0];
    }
  }

  async function uploadAndPreview() {
    if (!selectedFile.value) return;
    uploading.value = true;
    try {
      const result = await srsStore.importPreview(selectedFile.value);
      previewToken.value = result.previewToken;
      matched.value = result.matched;
      unmatched.value = result.unmatched;
      totalLines.value = result.totalLines;

      if (!deckName.value) {
        deckName.value = selectedFile.value.name.replace(/\.[^.]+$/, '');
      }

      step.value = 'preview';
    } catch (error: any) {
      toast.add({ severity: 'error', summary: 'Upload failed', detail: String(error?.data || error?.message || 'Unknown error'), life: 5000 });
    } finally {
      uploading.value = false;
    }
  }

  async function commitImport() {
    if (!deckName.value.trim()) {
      toast.add({ severity: 'warn', summary: 'Name required', life: 3000 });
      return;
    }
    committing.value = true;
    try {
      const excluded = excludedWordIds.value.size > 0 ? [...excludedWordIds.value] : undefined;
      await srsStore.importCommit(previewToken.value, deckName.value, deckDescription.value || undefined, excluded);
      toast.add({ severity: 'success', summary: `Imported ${includedCount.value} words`, life: 3000 });
      step.value = 'done';
      setTimeout(() => { localVisible.value = false; resetForm(); }, 1500);
    } catch (error: any) {
      toast.add({ severity: 'error', summary: 'Import failed', detail: String(error?.data || error?.message || 'Unknown error'), life: 5000 });
    } finally {
      committing.value = false;
    }
  }

  function resetForm() {
    step.value = 'upload';
    selectedFile.value = null;
    previewToken.value = '';
    matched.value = [];
    unmatched.value = [];
    totalLines.value = 0;
    deckName.value = '';
    deckDescription.value = '';
    excludedWordIds.value = new Set();
  }
</script>

<template>
  <Dialog
    v-model:visible="localVisible"
    header="Import Word List"
    modal
    :style="{ width: '550px', maxWidth: '95vw' }"
    :pt="{ content: { class: 'p-4' } }"
    @hide="resetForm"
  >
    <!-- Step 1: Upload -->
    <div v-if="step === 'upload'">
      <p class="text-sm text-gray-500 mb-4">
        Upload a text file (.txt, .csv, .tsv) with Japanese words. One word per line for .txt files.
      </p>

      <div class="mb-4">
        <input
          type="file"
          accept=".txt,.csv,.tsv"
          class="block w-full text-sm text-gray-500
            file:mr-4 file:py-2 file:px-4
            file:rounded file:border-0
            file:text-sm file:font-semibold
            file:bg-purple-50 file:text-purple-700
            dark:file:bg-purple-900/30 dark:file:text-purple-300
            hover:file:bg-purple-100 dark:hover:file:bg-purple-800/40"
          @change="onFileSelect"
        />
      </div>

      <div class="mb-3">
        <label class="block text-sm font-medium mb-1">Deck Name</label>
        <InputText v-model="deckName" placeholder="Name for this word list" class="w-full" :maxlength="200" />
      </div>

      <div class="mb-4">
        <label class="block text-sm font-medium mb-1">Description <span class="text-gray-400">(optional)</span></label>
        <Textarea v-model="deckDescription" class="w-full" rows="2" :maxlength="2000" />
      </div>

      <Button
        label="Upload & Preview"
        class="w-full"
        :loading="uploading"
        :disabled="!selectedFile"
        @click="uploadAndPreview"
      />
    </div>

    <!-- Step 2: Preview -->
    <div v-if="step === 'preview'">
      <div class="flex items-center justify-between mb-4 pb-3 border-b border-gray-200 dark:border-gray-700">
        <Button icon="pi pi-arrow-left" severity="secondary" text size="small" @click="step = 'upload'" />
        <span class="text-sm text-gray-500">{{ totalLines }} lines parsed</span>
      </div>

      <div class="grid grid-cols-2 gap-4 mb-4">
        <div class="rounded-lg border border-green-200 dark:border-green-800 bg-green-50 dark:bg-green-900/20 p-3 text-center">
          <div class="text-2xl font-bold text-green-600 dark:text-green-400">{{ includedCount }}</div>
          <div class="text-xs text-green-600 dark:text-green-400">{{ excludedWordIds.size > 0 ? `Matched (${excludedWordIds.size} excluded)` : 'Matched' }}</div>
        </div>
        <div class="rounded-lg border border-red-200 dark:border-red-800 bg-red-50 dark:bg-red-900/20 p-3 text-center">
          <div class="text-2xl font-bold text-red-500 dark:text-red-400">{{ unmatched.length }}</div>
          <div class="text-xs text-red-500 dark:text-red-400">Unmatched</div>
        </div>
      </div>

      <div v-if="matched.length > 0" class="mb-3">
        <div class="text-sm font-medium mb-1">Matched words (first 50)</div>
        <div class="max-h-[200px] overflow-y-auto rounded border border-gray-200 dark:border-gray-700">
          <div
            v-for="(word, i) in matched.slice(0, 50)"
            :key="i"
            class="flex items-center justify-between px-3 py-1.5 text-sm even:bg-gray-50 dark:even:bg-gray-800/50 cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700/50"
            :class="{ 'opacity-40 line-through': excludedWordIds.has(word.wordId) }"
            @click="toggleExclude(word.wordId)"
          >
            <span>{{ word.text }}</span>
            <div class="flex items-center gap-2">
              <span class="text-gray-400">{{ word.reading }}</span>
              <Icon
                :name="excludedWordIds.has(word.wordId) ? 'material-symbols:add-circle-outline' : 'material-symbols:remove-circle-outline'"
                size="16"
                :class="excludedWordIds.has(word.wordId) ? 'text-green-500' : 'text-red-400'"
              />
            </div>
          </div>
          <div v-if="matched.length > 50" class="px-3 py-1.5 text-xs text-gray-400 text-center">
            ... and {{ matched.length - 50 }} more
          </div>
        </div>
        <div v-if="excludedWordIds.size > 0" class="text-xs text-gray-500 mt-1">
          {{ excludedWordIds.size }} excluded (click to re-include)
        </div>
      </div>

      <div v-if="unmatched.length > 0" class="mb-3">
        <div class="text-sm font-medium mb-1 text-red-500">Unmatched (first 10)</div>
        <div class="max-h-[100px] overflow-y-auto rounded border border-red-200 dark:border-red-800">
          <div
            v-for="(word, i) in unmatched.slice(0, 10)"
            :key="i"
            class="px-3 py-1 text-sm text-red-400"
          >
            {{ word }}
          </div>
        </div>
      </div>

      <div class="mb-3">
        <label class="block text-sm font-medium mb-1">Deck Name</label>
        <InputText v-model="deckName" class="w-full" :maxlength="200" />
      </div>

      <Button
        :label="`Import ${includedCount} words`"
        class="w-full"
        :loading="committing"
        :disabled="includedCount === 0 || !deckName.trim()"
        @click="commitImport"
      />
    </div>

    <!-- Step 3: Done -->
    <div v-if="step === 'done'" class="text-center py-8">
      <Icon name="material-symbols:check-circle" size="48" class="text-green-500 mb-4" />
      <div class="text-lg font-semibold">Import Complete</div>
      <div class="text-sm text-gray-500">{{ includedCount }} words imported to "{{ deckName }}"</div>
    </div>
  </Dialog>
</template>
