<script setup lang="ts">
  import { ref, computed } from 'vue';
  import { YankiConnect } from 'yanki-connect';
  import { useToast } from 'primevue/usetoast';

  const emit = defineEmits<{
    importComplete: [];
  }>();

  const { $api } = useNuxtApp();
  const toast = useToast();

  let currentStep = ref(0);

  let client: YankiConnect;
  let decks: Record<string, number> = {};
  let deckEntries: Array<[string, number]> = [];
  let cantConnect = ref(false);
  let cardsIds: number[] = [];

  const isLoading = ref(false);

  let reviewByCard: Map<number, Array<{ Rating: number; ReviewDateTime: Date; ReviewDuration: number }>> = new Map();
  let selectedFieldName = '';
  let supportsFieldsFilter = false;

  const cardsInfoFields = ['cardId', 'due', 'queue', 'type', 'interval', 'factor', 'reps', 'lapses', 'mod', 'flags', 'fields', 'modelName', 'deckName'];

  async function ankiInvoke(action: string, params: Record<string, any> = {}): Promise<any> {
    const res = await fetch('http://127.0.0.1:8765', {
      method: 'POST',
      body: JSON.stringify({ action, version: 6, params }),
    });
    const json = await res.json();
    if (json.error) throw new Error(json.error);
    return json.result;
  }

  async function fetchCardsInfo(cards: number[]): Promise<any[]> {
    if (supportsFieldsFilter) {
      return ankiInvoke('cardsInfo', { cards, fields: cardsInfoFields });
    }
    return client.card.cardsInfo({ cards }) as Promise<any[]>;
  }

  const fetchProgress = ref(0);
  const uploadProgress = ref(0);
  const importPhase = ref<'fetch' | 'upload'>('fetch');
  const importResults = ref({ imported: 0, updated: 0, skipped: 0, reviewLogs: 0 });

  const selectedDeck = ref<number>(0);
  const selectedField = ref<number>(0);
  const fields = ref<Array<[string, { order: number; value: string }]>>([]);
  const overwriteExisting = ref(false);
  const parseWords = ref(false);
  const importReviewHistory = ref(true);

  const fieldsOptions = computed(() =>
    (fields.value || []).map((entry, idx) => ({
      label: entry[0] + (entry[1].value ? ` (${entry[1].value.substring(0, 20)})` : ''),
      value: idx,
    }))
  );

  const Connect = async () => {
    try {
      client = new YankiConnect();
      decks = await client.deck.deckNamesAndIds();
      deckEntries = Object.entries(decks);
      cantConnect.value = false;

      try {
        await ankiInvoke('cardsInfo', { cards: [], fields: cardsInfoFields });
        supportsFieldsFilter = true;
      } catch {
        supportsFieldsFilter = false;
      }

      await NextStep();
    } catch (e) {
      cantConnect.value = true;
      console.log(e);
    }
  };

  const PreviousStep = () => {
    currentStep.value -= 2;
    NextStep();
  };

  // Helper to build a single card payload from Anki card info
  const buildCardPayload = (card: any, fieldName: string) => {
    if (card.queue === -1) return null; // Skip suspended

    const field = card.fields[fieldName];
    const word = field?.value?.trim() || '';
    if (!word) return null;

    const reviews = reviewByCard.get(card.cardId) ?? [];

    // Convert Anki state to FSRS state
    let state: number;
    if (card.queue === 0) state = 0;
    else if (card.queue === 1 || card.queue === 3) state = 1; // Learning
    else state = 2; // Review

    const stability = card.interval > 0 ? card.interval : 0;
    const difficulty = Math.max(1, Math.min(10, 10 - (card.factor - 1300) / 170.0));

    const lastReview = reviews.length > 0 ? reviews[0].ReviewDateTime : null;

    let due: Date;
    if (card.queue === 0) {
      due = new Date();
    } else if (card.queue === 1 || card.queue === 3) {
      due = new Date(card.due * 1000);
    } else {
      if (lastReview) {
        due = new Date(lastReview.getTime() + card.interval * 86400000);
      } else {
        due = new Date(card.mod * 1000 + card.interval * 86400000);
      }
    }

    return {
      Card: {
        Word: word,
        Stability: stability,
        Difficulty: difficulty,
        Reps: card.reps,
        Lapses: card.lapses,
        Due: due.toISOString(),
        State: state,
        LastReview: lastReview?.toISOString(),
      },
      ReviewLogs: reviews.map((r) => ({
        Rating: r.Rating,
        ReviewDateTime: r.ReviewDateTime.toISOString(),
        ReviewDuration: r.ReviewDuration,
      })),
    };
  };

  const NextStep = async () => {
    currentStep.value++;

    if (currentStep.value == 2) {
      if (selectedDeck.value == null) {
        currentStep.value--;
        return;
      }

      isLoading.value = true;
      cardsIds = await client.card.findCards({ query: `did:${selectedDeck.value}` });
      const previewCards = await fetchCardsInfo([cardsIds[0]]);
      selectedField.value = 0;
      if (previewCards && previewCards.length > 0) {
        fields.value = Object.entries(previewCards[0].fields || {});
      } else {
        fields.value = [];
      }
      isLoading.value = false;
    }

    if (currentStep.value == 3) {
      // Step 3 is now instant - just store the field name
      const fieldEntry = fields.value[selectedField.value];
      selectedFieldName = fieldEntry ? fieldEntry[0] : '';
      if (!selectedFieldName) {
        console.warn('No field selected for mapping');
        currentStep.value--;
        return;
      }
      // No API calls - all heavy work is deferred to Step 4
    }

    if (currentStep.value == 4) {
      isLoading.value = true;
      fetchProgress.value = 0;
      uploadProgress.value = 0;
      importPhase.value = 'fetch';
      importResults.value = { imported: 0, updated: 0, skipped: 0, reviewLogs: 0 };

      const allSkippedWords: string[] = [];
      let skippedCountNoReviews = 0;

      try {
        // Fetch reviews only if requested
        reviewByCard = new Map();
        if (importReviewHistory.value) {
          const deckName = deckEntries.find(([_, id]) => id === selectedDeck.value)?.[0];
          if (deckName) {
            const reviews = await client.statistic.cardReviews({
              deck: deckName,
              startID: 1,
            });

            // Build map incrementally
            for (const [reviewTime, cardID, _usn, buttonPressed, _newInterval, _previousInterval, _newFactor, reviewDuration, _reviewType] of reviews) {
              const existing = reviewByCard.get(cardID) ?? [];
              existing.push({
                Rating: buttonPressed,
                ReviewDateTime: new Date(reviewTime),
                ReviewDuration: reviewDuration,
              });
              reviewByCard.set(cardID, existing);
            }

            // Clear raw array immediately to help GC
            reviews.length = 0;

            // Sort and limit to 10 most recent reviews per card
            for (const [key, arr] of reviewByCard.entries()) {
              arr.sort((a, b) => b.ReviewDateTime.getTime() - a.ReviewDateTime.getTime());
              reviewByCard.set(key, arr.slice(0, 10));
            }
          }
        }

        const ankiChunkSize = supportsFieldsFilter ? 2000 : 500;
        const chunks: number[][] = [];
        for (let i = 0; i < cardsIds.length; i += ankiChunkSize) {
          chunks.push(cardsIds.slice(i, i + ankiChunkSize));
        }

        const aggregateResult = (result: any) => {
          if (!result) return;
          importResults.value = {
            imported: importResults.value.imported + (result.imported || 0),
            updated: importResults.value.updated + (result.updated || 0),
            skipped: importResults.value.skipped + (result.skipped || 0),
            reviewLogs: importResults.value.reviewLogs + (result.reviewLogs || 0),
          };
          if (result.skippedWords) allSkippedWords.push(...result.skippedWords);
          skippedCountNoReviews += result.skippedCountNoReviews || 0;
        };

        if (supportsFieldsFilter) {
          // Optimized path: fetch all in parallel, dedup, upload in parallel
          let completedFetches = 0;
          const allChunkCards = await Promise.all(
            chunks.map(async (chunkIds) => {
              const cards = await fetchCardsInfo(chunkIds);
              completedFetches++;
              fetchProgress.value = Math.round((completedFetches / chunks.length) * 100);
              return cards;
            }),
          );

          const seenWords = new Set<string>();
          const allPayloads: any[] = [];
          for (const chunkCards of allChunkCards) {
            for (const card of chunkCards || []) {
              const payload = buildCardPayload(card, selectedFieldName);
              if (!payload) continue;
              if (seenWords.has(payload.Card.Word)) continue;
              seenWords.add(payload.Card.Word);
              allPayloads.push(payload);
            }
          }

          const apiChunkSize = 2000;
          const apiChunks: any[][] = [];
          for (let i = 0; i < allPayloads.length; i += apiChunkSize) {
            apiChunks.push(allPayloads.slice(i, i + apiChunkSize));
          }

          importPhase.value = 'upload';
          let completedUploads = 0;
          const apiResults = await Promise.all(
            apiChunks.map(async (chunkPayload) => {
              const result = await $api<any>('user/vocabulary/import-from-anki', {
                method: 'POST',
                body: JSON.stringify({
                  cards: chunkPayload,
                  overwrite: overwriteExisting.value,
                  parseWords: parseWords.value,
                }),
                headers: { 'Content-Type': 'application/json' },
              });
              completedUploads++;
              uploadProgress.value = Math.round((completedUploads / apiChunks.length) * 100);
              return result;
            }),
          );
          for (const result of apiResults) aggregateResult(result);
        } else {
          // Standard path: sequential fetch + upload to keep memory low
          for (let i = 0; i < chunks.length; i++) {
            const chunkCards = await fetchCardsInfo(chunks[i]);
            fetchProgress.value = Math.round(((i + 1) / chunks.length) * 100);

            const chunkPayload: any[] = [];
            for (const card of chunkCards || []) {
              const payload = buildCardPayload(card, selectedFieldName);
              if (payload) chunkPayload.push(payload);
            }

            if (chunkPayload.length === 0) continue;

            importPhase.value = 'upload';
            const result = await $api<any>('user/vocabulary/import-from-anki', {
              method: 'POST',
              body: JSON.stringify({
                cards: chunkPayload,
                overwrite: overwriteExisting.value,
                parseWords: parseWords.value,
              }),
              headers: { 'Content-Type': 'application/json' },
            });
            uploadProgress.value = Math.round(((i + 1) / chunks.length) * 100);
            aggregateResult(result);
            importPhase.value = 'fetch';
          }
        }

        // Show final results
        const r = importResults.value;
        let message = '';
        if (r.imported > 0) {
          message += `Imported ${r.imported} new card${r.imported === 1 ? '' : 's'}`;
        }
        if (r.updated > 0) {
          if (message) message += ', ';
          message += `updated ${r.updated} existing card${r.updated === 1 ? '' : 's'}`;
        }
        if (r.reviewLogs) {
          message += ` with ${r.reviewLogs} review log${r.reviewLogs === 1 ? '' : 's'}`;
        }
        if (r.skipped > 0) {
          if (message) message += '. ';
          message += `${r.skipped} card${r.skipped === 1 ? '' : 's'} skipped`;
        }
        if (skippedCountNoReviews > 0) {
          if (message) message += '. ';
          message += `${skippedCountNoReviews} card${skippedCountNoReviews === 1 ? '' : 's'} skipped (no reviews)`;
        }
        if (!message) {
          message = 'No cards were imported';
        } else {
          message += '.';
        }

        toast.add({
          severity: 'success',
          summary: 'Anki Data Imported',
          detail: message,
          life: 6000,
        });

        if (allSkippedWords.length > 0) {
          console.log('Skipped words (not parsed):', allSkippedWords);
          toast.add({
            severity: 'warn',
            summary: 'Some words not found',
            detail: `${allSkippedWords.length} words could not be parsed or were not in the dictionary. Check console for list.`,
            life: 10000,
          });
        }

        // Notify parent to refresh vocabulary counts
        emit('importComplete');

        // Clear review map to free memory
        reviewByCard = new Map();
      } catch (error) {
        console.error('Error importing Anki data:', error);
        toast.add({ severity: 'error', detail: extractApiError(error, 'Failed to import data.'), life: 5000 });
      } finally {
        isLoading.value = false;
        currentStep.value = 1;
      }
    }
  };
</script>

<template>
  <Card>
    <template #title>AnkiConnect</template>
    <template #content>
      <div v-if="cantConnect" class="text-red-800 dark:text-red-400">
        <p>Couldn't connect to Anki.</p>
        <p>
          Make sure you have the <a href="https://ankiweb.net/shared/info/2055492159" rel="nofollow" target="_blank">Anki Connect plugin</a> installed and
          enabled.
        </p>
        <p>Make sure Anki is running</p>
        <p>
          Go to Anki > Tools > Add-ons > AnkiConnect > Config and add the following line to webCorsOriginList, "https://jiten.moe" so it looks like the
          following screenshot:
        </p>
        <p>
          If you use Brave, please disable Brave Shields for this website. You can do so by clicking on the shield icon at the right of the URL bar.
        </p>
        <img src="/assets/img/ankiconnect.jpg" alt="Anki Connect Config" class="w-full" />
      </div>
      <div v-if="currentStep == 0">
        <p>
          Add words directly from Anki using the <a href="https://ankiweb.net/shared/info/2055492159" rel="nofollow" target="_blank">Anki Connect plugin</a>.
        </p>
        <div class="p-4">
          <Button label="Connect to Anki" @click="Connect()" />
        </div>
      </div>

      <div v-if="currentStep == 1 && deckEntries.length > 0">
        <p>Select a deck to add words from.</p>
        <Select v-model="selectedDeck" :options="deckEntries" optionLabel="0" optionValue="1" placeholder="Select a deck" class="w-full" />
        <div class="flex flex-row gap-2 p-4">
          <Button label="Next" :disabled="!selectedDeck" @click="NextStep()" />
        </div>
      </div>
      <div v-if="currentStep == 2">
        <p>
          Selected deck: <b>{{ deckEntries.find((d) => d[1] === selectedDeck)?.[0] || '—' }}</b>
        </p>
        <div v-if="isLoading">
          <ProgressSpinner style="width: 50px; height: 50px" stroke-width="8px" animation-duration=".5s" />
          <p>Loading your deck...</p>
        </div>
        <div v-else>
          <p>Select the correct field containing the words WITHOUT furigana</p>
          <Select v-model="selectedField" :options="fieldsOptions" optionLabel="label" optionValue="value" placeholder="Select a field" class="w-full" />
          <div class="flex flex-row gap-2 p-4">
            <Button label="Back" :disabled="!selectedDeck" @click="PreviousStep()" />
            <Button label="Next" @click="NextStep()" />
          </div>
        </div>
      </div>
      <div v-if="currentStep == 3">
        <p>
          This will import up to <b>{{ cardsIds.length }} cards</b>.
        </p>
        <p class="text-sm text-surface-500 mb-4">
          Suspended and empty cards will be skipped during import.
        </p>
        <div class="flex flex-col gap-3 p-4">
          <div class="flex items-center gap-2">
            <Checkbox v-model="importReviewHistory" inputId="importReviewHistory" :binary="true" />
            <label for="importReviewHistory" class="cursor-pointer">
              Import review history (uncheck for large decks with memory issues)
            </label>
          </div>
          <div class="flex items-center gap-2">
            <Checkbox v-model="overwriteExisting" inputId="overwrite" :binary="true" />
            <label for="overwrite" class="cursor-pointer">
              Overwrite existing cards (replace cards you already have with Anki versions, even if they are more recent)
            </label>
          </div>
          <div class="flex items-center gap-2">
            <Checkbox v-model="parseWords" inputId="parseWords" :binary="true" />
            <label for="parseWords" class="cursor-pointer">
              Parse words instead of importing them directly (only use if you have conjugated verbs instead of the dictionary form, less accurate)
            </label>
          </div>
          <div class="flex flex-row gap-2">
            <Button label="Back" :disabled="!selectedDeck" @click="PreviousStep()" />
            <Button label="Import" :disabled="!selectedDeck" @click="NextStep()" />
          </div>
        </div>
      </div>
      <div v-if="currentStep == 4">
        <ProgressSpinner style="width: 50px; height: 50px" stroke-width="8px" animation-duration=".5s" />
        <p v-if="importPhase === 'fetch'" class="font-semibold">Fetching cards from Anki... {{ fetchProgress }}%</p>
        <p v-else class="font-semibold">Uploading to server... {{ uploadProgress }}%</p>
        <p class="text-sm text-surface-500">
          Imported: {{ importResults.imported }} |
          Updated: {{ importResults.updated }} |
          Skipped: {{ importResults.skipped }}
        </p>
      </div>
    </template>
  </Card>
</template>

<style scoped></style>
