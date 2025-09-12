<script setup lang="ts">
  import { ref, computed } from 'vue';
  import { YankiConnect } from 'yanki-connect';
  import { useToast } from 'primevue/usetoast';

  const { $api } = useNuxtApp();
  const toast = useToast();

  let currentStep = ref(0);

  let client: YankiConnect;
  let decks: Record<string, number> = {};
  let deckEntries: Array<[string, number]> = [];
  let cantConnect = ref(false);
  let reviewsForKnown = ref(10);
  let cardsIds: number[] = [];

  const isLoading = ref(false);

  // Store only the minimal data we need for each card to reduce memory usage
  const cards = ref<Array<{ value: string; reps: number }>>([]);
  const cardsToKeep = computed(() => {
    return cards.value.filter((c) => c.reps >= reviewsForKnown.value);
  });

  const selectedDeck = ref<number>(0);
  const selectedField = ref<number>(0);
  const fields = ref<Array<[string, { order: number; value: string }]>>([]);

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
      // remove default deck, id = 1
      deckEntries = deckEntries.filter(([name, id]) => id !== 1);
      cantConnect.value = false;
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

  const NextStep = async () => {
    currentStep.value++;

    if (currentStep.value == 2) {
      if (selectedDeck.value == null) {
        currentStep.value--;
        return;
      }

      isLoading.value = true;
      cardsIds = await client.card.findCards({ query: `did:${selectedDeck.value}` });
      const previewCards = await client.card.cardsInfo({ cards: [cardsIds[0]] });
      selectedField.value = 0;
      if (previewCards && previewCards.length > 0) {
        fields.value = Object.entries(previewCards[0].fields || {});
      } else {
        fields.value = [];
      }
      cards.value = [];
      isLoading.value = false;
    }

    if (currentStep.value == 3) {
      isLoading.value = true;

      const fieldEntry = fields.value[selectedField.value];
      const fieldName = fieldEntry ? fieldEntry[0] : undefined;
      if (!fieldName) {
        console.warn('No field selected for mapping');
        return;
      }

      const batchSize = 500;
      cards.value = [];
      for (let i = 0; i < cardsIds.length; i += batchSize) {
        const batch = cardsIds.slice(i, i + batchSize);
        const cardsBatch = await client.card.cardsInfo({ cards: batch });
        const mapped = (cardsBatch || []).map((card: any) => {
          const field = (card.fields as Record<string, { order: number; value: string }>)[fieldName];
          const val = field ? field.value : '';
          return { value: val, reps: card.reps ?? 0 };
        });
        cards.value = cards.value.concat(mapped);
      }

      isLoading.value = false;
    }

    if (currentStep.value == 4) {
      isLoading.value = true;

      // We already stored only the selected field's value and reps, so just collect those values
      const lines = cardsToKeep.value.map((c) => (c.value ?? '').trim()).filter((v) => v.length > 0);
      const textContent = lines.join('\n');

      try{
      const file = new File([textContent], 'anki-words.txt', { type: 'text/plain' });

      const formData = new FormData();
      formData.append('file', file);

      const result = await $api<{ parsed: number; added: number; }>('user/vocabulary/import-from-anki-txt', {
        method: 'POST',
        body: formData,
      });

      if (result) {
        toast.add({
          severity: 'success',
          summary: 'Known words updated',
          detail: `Parsed ${result.parsed} words, added ${result.added} forms.`,
          life: 6000,
        });
      }
      } catch (error) {
        console.error('Error processing words:', error);
        toast.add({ severity: 'error', detail: 'Failed to process words.', life: 5000 });
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
        <div v-if="isLoading">
          <ProgressSpinner style="width: 50px; height: 50px" stroke-width="8px" animation-duration=".5s" />
          <p>Loading cards: {{ cards.length }}/{{ cardsIds.length }} ({{ ((cards.length / cardsIds.length) * 100).toFixed(0) }}%)...</p>
        </div>
        <div v-else>
          <p>Minimum amount of reviews for a word to be considered as known <InputNumber v-model="reviewsForKnown" show-buttons :min="0" /></p>
          <p>
            This will keep <b>{{ cardsToKeep.length }} words out of {{ cards.length }}</b
            >. All other words will stay unknown.
          </p>
          <div class="flex flex-row gap-2 p-4">
            <Button label="Back" :disabled="!selectedDeck" @click="PreviousStep()" />
            <Button label="Import" :disabled="!selectedDeck || cardsToKeep.length <= 0" @click="NextStep()" />
          </div>
        </div>
      </div>
      <div v-if="currentStep == 4">
        <ProgressSpinner style="width: 50px; height: 50px" stroke-width="8px" animation-duration=".5s" />
        <p>Adding to your known words...</p>
      </div>
    </template>
  </Card>
</template>

<style scoped></style>
