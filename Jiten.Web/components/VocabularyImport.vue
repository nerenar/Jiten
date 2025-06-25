<script setup lang="ts">
  import type { DeckCoverage } from '~/types';
  import { useConfirm } from 'primevue/useconfirm';
  import ConfirmDialog from 'primevue/confirmdialog';
  import Badge from 'primevue/badge';
  import Card from 'primevue/card';

  import { useJitenStore } from '~/stores/jitenStore';
  import { useToast } from 'primevue/usetoast';

  const toast = useToast();

  const frequencyRange = defineModel<number[]>('frequencyRange');
  frequencyRange.value = [0, 100];

  const store = useJitenStore();
  const { $api } = useNuxtApp();
  const confirm = useConfirm();

  let knownWordIdsAmount = ref(0);
  knownWordIdsAmount.value = store.getKnownWordIds().length;

  function clearKnownWords() {
    confirm.require({
      message: 'Are you sure you want to clear all known words? This action cannot be undone.',
      header: 'Clear Known Words',
      icon: 'pi pi-exclamation-triangle',
      acceptClass: 'p-button-danger',
      rejectClass: 'p-button-secondary',
      accept: () => {
        store.setKnownWordIds([]);
        knownWordIdsAmount.value = 0;
        toast.add({ severity: 'success', summary: 'Known words cleared', detail: 'All known words have been cleared.', life: 5000 });
      },
      reject: () => {},
    });
  }

  const vidRegex = /"vid"\s*:\s*(\d+)/g;
  const isLoading = ref(false);

  async function handleJpdbFileSelect(event) {
    const file = event.files?.[0];
    if (!file) {
      toast.add({ severity: 'error', summary: 'Error', detail: 'No file selected.', life: 5000 });
      return;
    }

    // Ensure it's a JSON file
    if (file.type !== 'application/json') {
      toast.add({ severity: 'error', summary: 'Error', detail: 'Please upload a JSON file.', life: 5000 });
      return;
    }

    const reader = new FileReader();

    reader.onload = async (e) => {
      try {
        const text = e.target?.result as string;
        const vids = Array.from(text.matchAll(vidRegex), (m) => Number(m[1]));

        if (vids.length > 0) {
          store.addKnownWordIds(vids);
          toast.add({ severity: 'success', summary: 'Added words', detail: `Found ${vids.length} word IDs.`, life: 5000 });
          await nextTick();
          knownWordIdsAmount.value = store.getKnownWordIds().length;
          console.log(`Extracted VIDs: ${vids.length}`, vids);
        } else {
          toast.add({ severity: 'info', summary: 'No words added', detail: 'No "vid" entries found in the file.', life: 5000 });
        }
      } catch (error) {
        console.error('Error processing file:', error);
        toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to process file. Invalid JSON or data format.', life: 5000 });
      }
    };

    reader.onerror = () => {
      toast.add({ severity: 'error', summary: 'Error', detail: 'Error reading file.', life: 5000 });
    };

    reader.readAsText(file);
  }

  async function handleAnkiFileSelect(event) {
    const file = event.files?.[0];
    if (!file) {
      toast.add({ severity: 'error', summary: 'Error', detail: 'No file selected.', life: 5000 });
      return;
    }

    // Ensure it's a TXT file
    if (file.type !== 'text/plain') {
      toast.add({ severity: 'error', summary: 'Error', detail: 'Please upload a TXT file.', life: 5000 });
      return;
    }

    try {
      // Show loading indicator
      isLoading.value = true;
      toast.add({ severity: 'info', summary: 'Processing Anki file...', detail: 'Please wait...', life: 5000 });

      // Create FormData to send the file
      const formData = new FormData();
      formData.append('file', file);

      // Send the file to the API
      const wordIds = await $api<number[]>('vocabulary/vocabulary-from-anki-txt', {
        method: 'POST',
        body: formData,
      });

      // Add the word IDs to the store
      if (wordIds && wordIds.length > 0) {
        store.addKnownWordIds(wordIds);
        toast.add({ severity: 'success', detail: `Found ${wordIds.length} word IDs from Anki file.`,  life: 5000 });
        await nextTick();
        knownWordIdsAmount.value = store.getKnownWordIds().length;
        console.log(`Extracted word IDs from Anki: ${wordIds.length}`, wordIds);
      } else {
        toast.add({ severity: 'info', detail: 'No word IDs found in the Anki file.',  life: 5000 });
      }
    } catch (error) {
      console.error('Error processing Anki file:', error);
      toast.add({ severity: 'error', detail: 'Failed to process Anki file.',  life: 5000 });
    } finally {
      isLoading.value = false;
    }
  }

  const updateMinFrequency = (value: number) => {
    if (frequencyRange.value) {
      frequencyRange.value = [value, frequencyRange.value[1]];
    }
  };

  const updateMaxFrequency = (value: number) => {
    if (frequencyRange.value) {
      frequencyRange.value = [frequencyRange.value[0], value];
    }
  };

  async function getVocabularyByFrequency() {
    const data = await $api<number[]>(`vocabulary/vocabulary-list-frequency/${frequencyRange.value[0]}/${frequencyRange.value[1]}`);
    toast.add({ severity: 'success', detail: `Added ${data.length} words by frequency range.`, life: 5000 });
    store.addKnownWordIds(data);
    await nextTick();
    knownWordIdsAmount.value = store.getKnownWordIds().length;
  }

  function downloadKnownWordIds() {
    // Get the current list of known word IDs
    const wordIds = store.getKnownWordIds();

    // Create a text content with one ID per line
    const content = wordIds.join('\n');

    // Create a Blob with the content
    const blob = new Blob([content], { type: 'text/plain' });

    // Create a URL for the Blob
    const url = URL.createObjectURL(blob);

    // Create a temporary anchor element to trigger the download
    const a = document.createElement('a');
    a.href = url;
    a.download = 'jiten-known-word-ids.txt';

    // Append the anchor to the document, click it, and remove it
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    // Release the URL object
    URL.revokeObjectURL(url);

    toast.add({ severity: 'success',  detail: `Exported ${wordIds.length} word IDs to text file.`, life: 5000 });

  }

  function handleWordIdsFileSelect(event) {
    const file = event.files?.[0];
    if (!file) {
      toast.add({ severity: 'error', summary: 'Error', detail: 'No file selected.', life: 5000 });
      return;
    }

    // Ensure it's a TXT file
    if (file.type !== 'text/plain') {
      toast.add({ severity: 'error', summary: 'Error', detail: 'Please upload a TXT file.', life: 5000 });
      return;
    }

    const reader = new FileReader();

    reader.onload = async (e) => {
      try {
        const text = e.target?.result as string;

        // Split the text by newlines and convert each line to a number
        const wordIds = text
          .split('\n')
          .map((line) => line.trim())
          .filter((line) => line !== '')
          .map((line) => parseInt(line, 10))
          .filter((id) => !isNaN(id));

        if (wordIds.length > 0) {
          store.addKnownWordIds(wordIds);
          toast.add({ severity: 'success', detail: `Imported ${wordIds.length} word IDs.`, life: 5000 });
          await nextTick();
          knownWordIdsAmount.value = store.getKnownWordIds().length;
          console.log(`Imported word IDs: ${wordIds.length}`, wordIds);
        } else {
          toast.add({ severity: 'info', detail: 'No valid word IDs found in the file.', life: 5000 });
        }
      } catch (error) {
        console.error('Error processing file:', error);
        toast.add({ severity: 'error', detail: 'Failed to process file. Invalid data format.', life: 5000 });
      }
    };

    reader.onerror = () => {
      toast.add({ severity: 'error', detail: 'Error reading file.', life: 5000 });
    };

    reader.readAsText(file);
  }
</script>

<template>
  <div class="p-4">
    <ConfirmDialog />
    <Toast />

    <Card class="mb-4">
      <template #title>
        <div class="flex justify-between items-center">
          <h2 class="text-xl font-bold">Vocabulary Management</h2>
        </div>
      </template>
      <template #subtitle>
        <p class="text-gray-600 dark:text-gray-300">
          You currently have <span class="font-extrabold text-primary-500">{{ knownWordIdsAmount }}</span> known words saved in your browser's local storage.
        </p>
      </template>
      <template #content>
        <p class="mb-3">
          You can upload a list of known words to calculate coverage and exclude them from downloads. Your data is processed temporarily on the server and
          remains stored only in your local storage.
        </p>
        <p class="mb-3 text-sm text-gray-500 dark:text-gray-400">
          Note: If you change browser or delete its data, your known words might be lost. You can export them to keep them safe.
        </p>

        <div class="flex">
          <Button @click="clearKnownWords" severity="danger" icon="pi pi-trash" label="Clear All Known Words" />
        </div>
      </template>
    </Card>

    <Card class="mb-4">
      <template #title>
        <h3 class="text-lg font-semibold">Export/Import Known Word IDs</h3>
      </template>
      <template #content>
        <p class="mb-3">
          You can export your known word IDs to a text file and import them later. This is useful for backing up your data or transferring it to another device.
        </p>

        <div class="flex flex-col md:flex-row gap-3 mb-3">
          <Button @click="downloadKnownWordIds" icon="pi pi-download" label="Export Word IDs" class="w-full md:w-auto" />

          <FileUpload
            mode="basic"
            name="wordIdsFile"
            accept=".txt"
            :customUpload="true"
            @select="handleWordIdsFileSelect"
            :auto="true"
            :chooseLabel="'Import Word IDs'"
            class="w-full md:w-auto"
          />
        </div>
      </template>
    </Card>

    <Card class="mb-4">
      <template #title>
        <h3 class="text-lg font-semibold">Upload JPDB Word List (JSON)</h3>
      </template>
      <template #content>
        <p class="mb-2">You can find the file in the settings > Data Export > button Export reviews (.json)</p>
        <p class="mb-3 text-sm text-amber-600 dark:text-amber-400">
          Warning: This will not add blacklisted words. If you blacklisted common words, please use the "Add words between global frequency" option below.
        </p>

        <FileUpload
          mode="basic"
          name="jpdbFile"
          accept=".json"
          :customUpload="true"
          @select="handleJpdbFileSelect"
          :auto="true"
          :chooseLabel="'Select reviews.json File'"
          class="mb-3"
        />
      </template>
    </Card>

    <Card class="mb-4">
      <template #title>
        <h3 class="text-lg font-semibold">Import from Anki Deck</h3>
      </template>
      <template #content>
        <p class="mb-2">Export your deck as Export format: Notes in Plain Text (.txt) and untick all the boxes.</p>
        <p class="mb-3 text-sm text-amber-600 dark:text-amber-400">
          Warning: This will mark ALL words contained in the deck as known. You will have to remove the lines you don't want manually before uploading your
          file. The words to add need to be the first word on each line. Limited to 50000 words.
        </p>

        <FileUpload
          mode="basic"
          name="ankiFile"
          accept=".txt"
          :customUpload="true"
          @select="handleAnkiFileSelect"
          :auto="true"
          :chooseLabel="'Select anki .txt File'"
          class="mb-3"
        />
      </template>
    </Card>

    <Card class="mb-4">
      <template #title>
        <h3 class="text-lg font-semibold">Add Words by Frequency Range</h3>
      </template>
      <template #content>
        <!-- TODO: INVESTIGATE WHY THIS DOESNT RENDER THE SAME IN PROD -->
        <div class="flex flex-col gap-4">
          <div class="flex flex-row flex-wrap gap-2 items-center" style="align-self: center; width:80%">
            <InputNumber
              :model-value="frequencyRange?.[0] ?? 0"
              show-buttons
              fluid
              size="small"
              class="max-w-20 flex-shrink-0"
              style="left:0.25em;"
              @update:model-value="updateMinFrequency"
            />
            <Slider v-model="frequencyRange" range :min="0" :max="50000" class="flex-grow mx-2 flex-basis-auto" />
            <InputNumber
              :model-value="frequencyRange?.[1] ?? 0"
              show-buttons
              fluid
              size="small"
              class="max-w-20 flex-shrink-0"
              style="left:1em;"
              @update:model-value="updateMaxFrequency"
            />
          </div>
          <Button @click="getVocabularyByFrequency" icon="pi pi-plus" label="Add Words by Frequency" class="w-full md:w-auto" />
        </div>
      </template>
    </Card>

    <!-- Loading overlay -->
    <div v-if="isLoading" class="loading-overlay">
      <i class="pi pi-spin pi-spinner" style="font-size: 2rem"></i>
      <p>Processing Anki file...</p>
    </div>
  </div>
</template>

<style scoped>
  .loading-overlay {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgba(0, 0, 0, 0.8);
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    z-index: 9999;
    color: white;
  }

  .loading-overlay i {
    margin-bottom: 1rem;
  }
</style>
