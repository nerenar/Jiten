<script setup lang="ts">
  import { useSrsStore } from '~/stores/srsStore';
  import { useToast } from 'primevue/usetoast';

  const props = defineProps<{ inline?: boolean }>();

  const srsStore = useSrsStore();
  const toast = useToast();

  const form = reactive({ ...srsStore.studySettings });
  const saving = ref(false);
  const loaded = ref(false);

  onMounted(async () => {
    await srsStore.fetchSettings();
    Object.assign(form, srsStore.studySettings);
    loaded.value = true;
  });

  const gradingOptions = [
    { label: '4 buttons', value: 4 },
    { label: '2 buttons', value: 2 },
  ];

  const interleavingOptions = [
    { label: 'Mixed', value: 'Mixed' },
    { label: 'New first', value: 'NewFirst' },
    { label: 'Reviews first', value: 'ReviewsFirst' },
  ];

  const newCardGatheringOptions = [
    { label: 'Top deck', value: 'TopDeck' },
    { label: 'All decks equally', value: 'RoundRobin' },
    { label: 'Cross-deck frequency', value: 'CrossDeckFrequency' },
  ];

  const reviewFromOptions = [
    { label: 'All tracked', value: 'AllTracked' },
    { label: 'Study decks only', value: 'StudyDecksOnly' },
  ];

  const exampleSentenceOptions = [
    { label: 'Hidden', value: 'Hidden' },
    { label: 'Front', value: 'Front' },
    { label: 'Back', value: 'Back' },
  ];

  async function save() {
    saving.value = true;
    try {
      await srsStore.updateSettings({ ...form });
      toast.add({ severity: 'success', summary: 'Study settings saved', life: 2000 });
    } catch {
      toast.add({ severity: 'error', summary: 'Failed to save settings', life: 3000 });
    } finally {
      saving.value = false;
    }
  }

  const CardWrapper = defineComponent({
    props: { card: Boolean },
    setup(wrapperProps, { slots }) {
      return () => {
        if (!wrapperProps.card) return slots.default?.();
        return h(resolveComponent('Card'), null, {
          title: () => h('h3', { class: 'text-lg font-semibold' }, 'SRS Study'),
          content: () => slots.default?.(),
        });
      };
    },
  });
</script>

<template>
  <CardWrapper :card="!props.inline">
    <div v-if="!loaded" class="flex justify-center py-4">
      <ProgressSpinner style="width: 24px; height: 24px" />
    </div>
    <div v-else class="flex flex-col gap-4">
      <h3 class="text-sm font-semibold text-surface-500 uppercase tracking-wide">Session limits</h3>
      <div :class="props.inline ? 'flex flex-col gap-4' : 'grid grid-cols-1 md:grid-cols-3 gap-4'">
        <div class="min-w-0">
          <label class="block text-sm font-medium mb-1">
            New cards per day
            <Tooltip content="Maximum number of new words introduced each day. Set to 0 to pause learning new words while still reviewing." placement="top">
              <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
            </Tooltip>
          </label>
          <InputNumber v-model="form.newCardsPerDay" :min="0" :max="9999" :show-buttons="!props.inline" class="w-full [&_input]:w-full" />
        </div>
        <div class="min-w-0">
          <label class="block text-sm font-medium mb-1">
            Max reviews per day
            <Tooltip content="Maximum number of review cards shown each day. Reviews that exceed this limit carry over to the next day." placement="top">
              <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
            </Tooltip>
          </label>
          <InputNumber v-model="form.maxReviewsPerDay" :min="0" :max="9999" :show-buttons="!props.inline" class="w-full [&_input]:w-full" />
        </div>
        <div class="min-w-0">
          <label class="block text-sm font-medium mb-1">
            Card batch size
            <Tooltip content="Number of cards loaded at a time during a study session. Smaller batches keep sessions focused, larger batches reduce loading pauses." placement="top">
              <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
            </Tooltip>
          </label>
          <InputNumber v-model="form.batchSize" :min="1" :max="999" :show-buttons="!props.inline" class="w-full [&_input]:w-full" />
        </div>
      </div>

      <div class="flex items-center gap-2">
        <ToggleSwitch v-model="form.countFailedReviews" input-id="countFailedReviews" />
        <label for="countFailedReviews" class="text-sm cursor-pointer">
          Count failed reviews toward daily limit
          <Tooltip content="When enabled, every review counts toward your daily limit, including repeated reviews of cards you got wrong. When disabled, only unique cards count, so failing a card multiple times won't eat into your daily budget." placement="top">
            <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
          </Tooltip>
        </label>
      </div>

      <Divider />

      <h3 class="text-sm font-semibold text-surface-500 uppercase tracking-wide">Scheduling</h3>
      <div>
        <label class="block text-sm font-medium mb-1">
          Card interleaving
          <Tooltip content="Controls how new cards and reviews are mixed.<br>**Mixed** — shuffles new and review cards together.<br>**New first** — shows all new cards before reviews.<br>**Reviews first** — clears your review backlog before introducing new cards." placement="top">
            <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
          </Tooltip>
        </label>
        <SelectButton v-model="form.interleaving" :options="interleavingOptions" option-label="label" option-value="value" :allow-empty="false" class="flex-wrap" />
      </div>

      <div>
        <label class="block text-sm font-medium mb-1">
          New card gathering
          <Tooltip content="How new cards are picked when you have multiple decks.<br>**Top deck** — draws all new cards from your highest-priority deck first before moving to the next.<br>**All decks equally** — rotates between decks so you get new cards from each one.<br>**Cross-deck frequency** — draw words by total occurrence count across all your study decks, picking the words you'll see the most first." placement="top">
            <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
          </Tooltip>
        </label>
        <SelectButton v-model="form.newCardGathering" :options="newCardGatheringOptions" option-label="label" option-value="value" :allow-empty="false" class="flex-wrap" />
      </div>

      <div>
        <label class="block text-sm font-medium mb-1">
          Review cards from
          <Tooltip content="Which words to include in your reviews.<br>**All tracked** — reviews every word you've ever studied, even if it's no longer in an active deck.<br>**Study decks only** — only reviews words that belong to your current study decks." placement="top">
            <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
          </Tooltip>
        </label>
        <SelectButton v-model="form.reviewFrom" :options="reviewFromOptions" option-label="label" option-value="value" :allow-empty="false" class="flex-wrap" />
      </div>

      <Divider />

      <h3 class="text-sm font-semibold text-surface-500 uppercase tracking-wide">Card appearance</h3>
      <div>
        <label class="block text-sm font-medium mb-2">Card front</label>
        <div class="flex flex-col gap-2">
          <div class="flex items-center gap-2">
            <ToggleSwitch v-model="form.showCardStatus" input-id="showCardStatus" />
            <label for="showCardStatus" class="text-sm cursor-pointer">
              Show card status
              <Tooltip content="Display the card status (New, Review, Again) at the top of the card." placement="right">
                <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
              </Tooltip>
            </label>
          </div>
          <div class="flex items-center gap-2">
            <ToggleSwitch v-model="form.showFuriganaOnFront" input-id="showFuriganaOnFront" />
            <label for="showFuriganaOnFront" class="text-sm cursor-pointer">
              Show furigana
              <Tooltip content="Display furigana (reading hints) above kanji on the card front." placement="right">
                <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
              </Tooltip>
            </label>
          </div>
          <div v-if="form.showFuriganaOnFront" class="flex items-center gap-2 ml-6">
            <ToggleSwitch v-model="form.furiganaOnFrontNewOnly" input-id="furiganaOnFrontNewOnly" />
            <label for="furiganaOnFrontNewOnly" class="text-sm cursor-pointer">
              New cards only
              <Tooltip content="Only show furigana on cards you haven't seen before. Review cards will show plain kanji." placement="right">
                <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
              </Tooltip>
            </label>
          </div>
        </div>
      </div>

      <div>
        <label class="block text-sm font-medium mb-2">Card back</label>
        <div class="flex flex-col gap-2">
          <div class="flex items-center gap-2">
            <ToggleSwitch v-model="form.showPitchAccent" input-id="showPitchAccent" />
            <label for="showPitchAccent" class="text-sm cursor-pointer">
              Pitch accent
              <Tooltip content="Show the pitch accent pattern on the card back." placement="right">
                <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
              </Tooltip>
            </label>
          </div>
          <div>
            <label class="text-sm mb-1 block">
              Example sentence
              <Tooltip content="Show an example sentence from the media where the word appears.<br>**Hidden** — no sentence shown.<br>**Front** — sentence visible before you flip the card (sentence card).<br>**Back** — sentence shown only after you flip." placement="right">
                <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
              </Tooltip>
            </label>
            <SelectButton v-model="form.exampleSentencePosition" :options="exampleSentenceOptions" option-label="label" option-value="value" :allow-empty="false" />
            <div v-if="form.exampleSentencePosition !== 'Hidden'" class="flex items-center gap-2 mt-2">
              <ToggleSwitch v-model="form.blurExampleSentence" input-id="blurExampleSentence" />
              <label for="blurExampleSentence" class="text-sm cursor-pointer">
                Blur until clicked
                <Tooltip content="Example sentence is blurred by default. Click it to reveal." placement="right">
                  <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
                </Tooltip>
              </label>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <ToggleSwitch v-model="form.showFrequencyRank" input-id="showFrequencyRank" />
            <label for="showFrequencyRank" class="text-sm cursor-pointer">
              Frequency rank
              <Tooltip content="Show how common the word is in Japanese, based on overall word frequency data." placement="right">
                <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
              </Tooltip>
            </label>
          </div>
          <div class="flex items-center gap-2">
            <ToggleSwitch v-model="form.showKanjiBreakdown" input-id="showKanjiBreakdown" />
            <label for="showKanjiBreakdown" class="text-sm cursor-pointer">
              Kanji breakdown
              <Tooltip content="Show the individual kanji that make up the word along with their usual meaning." placement="right">
                <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
              </Tooltip>
            </label>
          </div>
        </div>
      </div>

      <Divider />

      <h3 class="text-sm font-semibold text-surface-500 uppercase tracking-wide">Study session UI</h3>
      <div>
        <label class="block text-sm font-medium mb-1">
          Grading buttons
          <Tooltip content="**4 buttons** — Again, Hard, Good, Easy — gives finer control over scheduling.<br>**2 buttons** — Forgot and Remembered — simpler and faster to grade." placement="top">
            <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
          </Tooltip>
        </label>
        <SelectButton v-model="form.gradingButtons" :options="gradingOptions" option-label="label" option-value="value" :allow-empty="false" />
      </div>

      <div class="flex flex-col gap-2">
        <div class="flex items-center gap-2">
          <ToggleSwitch v-model="form.showNextInterval" input-id="showNextInterval" />
          <label for="showNextInterval" class="text-sm cursor-pointer">
            Show next interval on buttons
            <Tooltip content="Display the next review interval (e.g. '4d', '2w') on each grade button." placement="right">
              <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
            </Tooltip>
          </label>
        </div>
        <div class="flex items-center gap-2">
          <ToggleSwitch v-model="form.showKeybinds" input-id="showKeybinds" />
          <label for="showKeybinds" class="text-sm cursor-pointer">
            Show keyboard shortcuts
            <Tooltip content="Display keyboard shortcut hints (1, 2, 3, 4) on the grade buttons." placement="right">
              <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
            </Tooltip>
          </label>
        </div>
        <div class="flex items-center gap-2">
          <ToggleSwitch v-model="form.showElapsedTime" input-id="showElapsedTime" />
          <label for="showElapsedTime" class="text-sm cursor-pointer">
            Show elapsed time
            <Tooltip content="Display a timer showing how long you've spent in the study session." placement="right">
              <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
            </Tooltip>
          </label>
        </div>
        <div class="flex items-center gap-2">
          <ToggleSwitch v-model="form.enableSwipeGesture" input-id="enableSwipeGesture" />
          <label for="enableSwipeGesture" class="text-sm cursor-pointer">
            Swipe to grade
            <Tooltip content="Swipe the card left (Again) or right (Good) to grade. Works with both mouse and touch." placement="right">
              <i class="pi pi-info-circle text-xs text-surface-400 ml-1 cursor-help" />
            </Tooltip>
          </label>
        </div>
      </div>

      <Button label="Save" :loading="saving" class="w-full md:w-auto" @click="save" />
    </div>
  </CardWrapper>
</template>
