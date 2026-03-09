<script setup lang="ts">
  import { type DeckVocabularyList, SortOrder } from '~/types';
  import { useAuthStore } from '~/stores/authStore';
  import { debounce } from 'perfect-debounce';

  const route = useRoute();
  const router = useRouter();

  const auth = useAuthStore();
  const localiseTitle = useLocaliseTitle();

  const id = route.params.id;

  const offset = computed(() => (route.query.offset ? Number(route.query.offset) : 0));
  const url = computed(() => `media-deck/${id}/vocabulary`);

  const sortByOptions = ref([
    { label: 'Chronological', value: 'chrono' },
    { label: 'Deck Frequency', value: 'deckFreq' },
    { label: 'Global Frequency', value: 'globalFreq' },
  ]);

  const sortDescending = ref(route.query.sortOrder === String(SortOrder.Descending));
  const sortBy = ref(route.query.sortBy?.toString() || sortByOptions.value[0].value);
  const display = ref(route.query.display?.toString() || 'all');
  const search = ref(route.query.search?.toString() || '');
  const debouncedSearch = ref(search.value);

  const sortOrder = computed(() => sortDescending.value ? SortOrder.Descending : SortOrder.Ascending);

  watch(sortDescending, () => {
    router.replace({
      query: { ...route.query, sortOrder: sortOrder.value },
    });
  });

  watch(sortBy, (newValue) => {
    router.replace({
      query: { ...route.query, sortBy: newValue },
    });
  });

  watch(display, (newValue) => {
    router.replace({
      query: { ...route.query, display: newValue },
    });
  });

  const updateSearch = debounce((val: string) => {
    debouncedSearch.value = val;
    router.replace({ query: { ...route.query, search: val || undefined, offset: undefined } });
  }, 300);
  watch(search, updateSearch);

  const {
    data: response,
    status,
    error,
  } = await useApiFetchPaginated<DeckVocabularyList>(url.value, {
    query: {
      offset: offset,
      sortBy: sortBy,
      sortOrder: sortOrder,
      displayFilter: display,
      search: debouncedSearch,
    },
    watch: [offset, debouncedSearch],
  });

  const { start, end, totalItems, previousLink, nextLink } = usePagination(response);

  const deckId = computed(() => {
    return response.value?.data?.deck?.deckId;
  });

  const title = computed(() => {
    if (!response.value?.data) {
      return '';
    }

    let title = '';
    if (response.value?.data.parentDeck != null) title += localiseTitle(response.value?.data.parentDeck) + ' - ';

    title += localiseTitle(response.value?.data.deck);

    return title;
  });

  useHead(() => {
    return {
      title: `${title.value} - Vocabulary`,
      meta: [
        {
          name: 'description',
          content: `Vocabulary list for ${title.value}`,
        },
      ],
    };
  });

</script>

<template>
  <div class="flex flex-col gap-2">
    <div>
      Vocabulary for
      <NuxtLink :to="`/decks/media/${deckId}/detail`">
        {{ title }}
      </NuxtLink>
    </div>
    <VocabularyFilters
      v-model:sort-by="sortBy"
      v-model:sort-descending="sortDescending"
      v-model:display-filter="display"
      v-model:search="search"
      :sort-by-options="sortByOptions"
      :show-display-filter="auth.isAuthenticated"
    />
    <PaginationControls :previous-link="previousLink" :next-link="nextLink" :start="start" :end="end" :total-items="totalItems" item-label="words" />
    <VocabularyList :words="response?.data?.words ?? []" :status="status" :error="error">
      <template #error="{ error: err }">
        <div>Error: {{ err }}</div>
      </template>
    </VocabularyList>
    <PaginationControls :previous-link="previousLink" :next-link="nextLink" :start="start" :end="end" :total-items="totalItems" :show-summary="false" :scroll-to-top-on-next="true" />
  </div>
</template>

<style scoped></style>
