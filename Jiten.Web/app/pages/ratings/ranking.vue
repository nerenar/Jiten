<script setup lang="ts">
import type { DeckSummaryDto, DifficultyRankingSectionDto } from '~/types/types';
import { DifficultyRankingMoveMode, MediaTypeGroup, TitleLanguage } from '~/types';
import { getMediaTypeGroupText } from '~/utils/mediaTypeMapper';

definePageMeta({
  middleware: ['auth'],
});

useHead({ title: 'Rank Difficulties - Jiten' });

const { fetchRankings, moveRanking } = useDifficultyRankings();
const jitenStore = useJitenStore();

const sections = ref<DifficultyRankingSectionDto[]>([]);
const activeGroup = ref<MediaTypeGroup | null>(null);
const isLoading = ref(true);
const isUpdating = ref(false);

const draggingDeck = ref<DeckSummaryDto | null>(null);
const draggingFromGroupId = ref<number | null>(null);
const dragOver = ref<{ kind: 'group'; groupId: number } | { kind: 'gap'; index: number } | { kind: 'unranked' } | null>(null);

let ghost: HTMLElement | null = null;
let offsetX = 0;
let offsetY = 0;

function deckTitle(deck: DeckSummaryDto): string {
  if (jitenStore.titleLanguage === TitleLanguage.English)
    return deck.englishTitle ?? deck.romajiTitle ?? deck.title;
  if (jitenStore.titleLanguage === TitleLanguage.Romaji)
    return deck.romajiTitle ?? deck.title;
  return deck.title;
}

const activeSection = computed(() => {
  if (activeGroup.value === null) return null;
  return sections.value.find(s => s.group === activeGroup.value) ?? null;
});

function ensureActiveGroup() {
  if (sections.value.length === 0) {
    activeGroup.value = null;
    return;
  }
  if (activeGroup.value === null || !sections.value.some(s => s.group === activeGroup.value)) {
    activeGroup.value = sections.value[0].group;
  }
}

async function loadRankings() {
  isLoading.value = true;
  sections.value = await fetchRankings();
  ensureActiveGroup();
  isLoading.value = false;
}

function mergeSections(updated: DifficultyRankingSectionDto[]) {
  for (const section of updated) {
    const idx = sections.value.findIndex(s => s.group === section.group);
    if (idx >= 0) sections.value[idx] = section;
    else sections.value.push(section);
  }
  sections.value = sections.value.sort((a, b) => a.group - b.group);
  ensureActiveGroup();
}

function cancelDrag() {
  document.removeEventListener('pointermove', onPointerMove);
  document.removeEventListener('pointerup', onPointerUp);
  document.removeEventListener('pointercancel', onPointerUp);
  window.removeEventListener('blur', cancelDrag);

  if (ghost) {
    ghost.remove();
    ghost = null;
  }

  draggingDeck.value = null;
  draggingFromGroupId.value = null;
  dragOver.value = null;
}

function onPointerDown(deck: DeckSummaryDto, fromGroupId: number | null, ev: PointerEvent) {
  if (ev.button !== 0) return;
  ev.preventDefault();

  draggingDeck.value = deck;
  draggingFromGroupId.value = fromGroupId;

  const target = ev.currentTarget as HTMLElement | null;
  if (!target) return;

  const rect = target.getBoundingClientRect();
  offsetX = ev.clientX - rect.left;
  offsetY = ev.clientY - rect.top;

  const clone = target.cloneNode(true) as HTMLElement;
  clone.style.position = 'fixed';
  clone.style.left = `${rect.left}px`;
  clone.style.top = `${rect.top}px`;
  clone.style.width = `${rect.width}px`;
  clone.style.zIndex = '9999';
  clone.style.pointerEvents = 'none';
  clone.style.opacity = '0.9';
  clone.style.boxShadow = '0 10px 24px rgba(0,0,0,0.25)';
  clone.style.transform = 'scale(1.02)';
  document.body.appendChild(clone);
  ghost = clone;

  document.addEventListener('pointermove', onPointerMove);
  document.addEventListener('pointerup', onPointerUp);
  document.addEventListener('pointercancel', onPointerUp);
  window.addEventListener('blur', cancelDrag);
}

function updateDropTarget(ev: PointerEvent) {
  const el = document.elementFromPoint(ev.clientX, ev.clientY) as HTMLElement | null;
  if (!el) {
    dragOver.value = null;
    return;
  }

  const unrankedEl = el.closest('[data-drop-unranked]');
  if (unrankedEl) {
    dragOver.value = { kind: 'unranked' };
    return;
  }

  const groupEl = el.closest('[data-drop-group]') as HTMLElement | null;
  if (groupEl?.dataset.dropGroup) {
    dragOver.value = { kind: 'group', groupId: Number(groupEl.dataset.dropGroup) };
    return;
  }

  const gapEl = el.closest('[data-drop-gap]') as HTMLElement | null;
  if (gapEl?.dataset.dropGap) {
    dragOver.value = { kind: 'gap', index: Number(gapEl.dataset.dropGap) };
    return;
  }

  dragOver.value = null;
}

function onPointerMove(ev: PointerEvent) {
  if (!ghost) return;
  ev.preventDefault();
  ghost.style.left = `${ev.clientX - offsetX}px`;
  ghost.style.top = `${ev.clientY - offsetY}px`;
  updateDropTarget(ev);
}

async function applyMove(params: { deckId: number; mode: DifficultyRankingMoveMode; targetGroupId?: number; insertIndex?: number }) {
  if (isUpdating.value) return;
  isUpdating.value = true;
  const updated = await moveRanking(params);
  if (updated.length > 0) mergeSections(updated);
  isUpdating.value = false;
}

async function handleDrop() {
  if (!draggingDeck.value || !dragOver.value) return;
  if (!activeSection.value) return;

  const deckId = draggingDeck.value.id;

  if (dragOver.value.kind === 'unranked') {
    if (draggingFromGroupId.value == null) return;
    await applyMove({ deckId, mode: DifficultyRankingMoveMode.Unrank });
    return;
  }

  if (dragOver.value.kind === 'group') {
    if (dragOver.value.groupId === draggingFromGroupId.value) return;
    await applyMove({
      deckId,
      mode: DifficultyRankingMoveMode.Merge,
      targetGroupId: dragOver.value.groupId,
    });
    return;
  }

  if (dragOver.value.kind === 'gap') {
    await applyMove({
      deckId,
      mode: DifficultyRankingMoveMode.Insert,
      insertIndex: dragOver.value.index,
    });
  }
}

async function onPointerUp() {
  await handleDrop();
  cancelDrag();
}

onMounted(loadRankings);
onUnmounted(cancelDrag);
</script>

<template>
  <div class="p-2 md:p-4 overflow-hidden">
    <div class="flex flex-col md:flex-row items-start md:items-center justify-between mb-6 gap-3">
      <div>
        <h1 class="text-2xl font-bold">Rank Difficulties</h1>
        <p class="text-muted-color text-sm mt-1">
          Drag titles into order. Drop between groups to create a new rank, or stack to mark a tie.
        </p>
      </div>
      <NuxtLink to="/ratings" class="text-sm text-muted-color hover:text-primary-500">
        Back to comparisons
      </NuxtLink>
    </div>

    <div v-if="isLoading" class="flex justify-center py-12">
      <ProgressSpinner style="width: 50px; height: 50px" />
    </div>

    <div v-else-if="sections.length === 0" class="text-center text-muted-color py-12">
      Complete at least two titles to start ranking.
    </div>

    <div v-else>
      <div class="flex flex-wrap gap-2 mb-4">
        <Button
          v-for="section in sections"
          :key="section.group"
          :label="getMediaTypeGroupText(section.group)"
          size="small"
          :severity="section.group === activeGroup ? 'primary' : 'secondary'"
          @click="activeGroup = section.group"
        />
      </div>

      <div v-if="activeSection" class="grid grid-cols-1 lg:grid-cols-[minmax(0,340px)_1fr] gap-4">
        <!-- Unranked -->
        <Card class="min-w-0" data-drop-unranked :class="{ 'is-drop-active': dragOver?.kind === 'unranked' }">
          <template #content>
            <div class="flex items-center justify-between mb-3">
              <div class="font-semibold">Unranked</div>
              <div class="text-xs text-muted-color">{{ activeSection.unranked.length }}</div>
            </div>
            <div v-if="activeSection.unranked.length === 0" class="text-sm text-muted-color">
              Everything in this group is ranked.
            </div>
            <div v-else class="flex flex-col gap-2">
              <div
                v-for="deck in activeSection.unranked"
                :key="deck.id"
                class="deck-pill"
                @pointerdown="onPointerDown(deck, null, $event)"
              >
                <img :src="deck.coverUrl || '/img/nocover.jpg'" :alt="deckTitle(deck)" class="h-10 w-7 rounded object-cover" />
                <div class="flex-1 min-w-0">
                  <div class="truncate text-sm font-medium">{{ deckTitle(deck) }}</div>
                </div>
              </div>
            </div>
          </template>
        </Card>

        <!-- Ranked groups -->
        <div class="min-w-0">
          <div class="text-xs text-muted-color mb-2">Top is easiest, bottom is hardest.</div>
          <div
            v-if="activeSection.groups.length === 0"
            class="empty-drop text-muted-color"
            :class="{ 'is-active': dragOver?.kind === 'gap' && dragOver.index === 0 }"
            :data-drop-gap="0"
          >
            Drop here to create the first rank.
          </div>

          <div v-else class="flex flex-col gap-3">
            <div
              v-for="(group, index) in activeSection.groups"
              :key="group.id"
              class="flex flex-col gap-3"
            >
              <div
                class="drop-gap"
                :class="{ 'is-active': dragOver?.kind === 'gap' && dragOver.index === index }"
                :data-drop-gap="index"
              />

              <Card
                class="rank-group min-w-0"
                :class="{ 'is-active': dragOver?.kind === 'group' && dragOver.groupId === group.id }"
                :data-drop-group="group.id"
              >
                <template #content>
                  <div class="flex items-center justify-between mb-2">
                    <div class="text-xs uppercase tracking-wide text-muted-color">Rank {{ index + 1 }}</div>
                    <div class="text-xs text-muted-color">{{ group.decks.length }}</div>
                  </div>
                  <div class="rank-group-list flex flex-col gap-2">
                    <div
                      v-for="deck in group.decks"
                      :key="deck.id"
                      class="deck-pill"
                      @pointerdown="onPointerDown(deck, group.id, $event)"
                    >
                      <img :src="deck.coverUrl || '/img/nocover.jpg'" :alt="deckTitle(deck)" class="h-10 w-7 rounded object-cover" />
                      <div class="flex-1 min-w-0">
                        <div class="truncate text-sm font-medium">{{ deckTitle(deck) }}</div>
                      </div>
                    </div>
                  </div>
                </template>
              </Card>
            </div>

            <div
              class="drop-gap"
              :class="{ 'is-active': dragOver?.kind === 'gap' && dragOver.index === activeSection.groups.length }"
              :data-drop-gap="activeSection.groups.length"
            />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.rank-group-list {
  --rank-pill-height: 58px;
  max-height: calc(var(--rank-pill-height) * 3 + 1rem);
  overflow-y: auto;
  overscroll-behavior: contain;
  padding-right: 0.25rem;
  scrollbar-gutter: stable;
}

.rank-group-list::-webkit-scrollbar {
  width: 8px;
}

.rank-group-list::-webkit-scrollbar-thumb {
  background: var(--surface-300);
  border-radius: 999px;
}

.rank-group-list::-webkit-scrollbar-track {
  background: transparent;
}

.deck-pill {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  border: 1px solid var(--surface-200);
  border-radius: 0.5rem;
  background: var(--surface-0);
  cursor: grab;
  transition: border-color 0.2s ease, background-color 0.2s ease;
}

.deck-pill:hover {
  border-color: var(--primary-300);
  background: var(--surface-50);
}

.rank-group.is-active,
.deck-pill.is-active {
  border-color: var(--primary-400);
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.15);
}

.drop-gap {
  height: 12px;
  border-radius: 999px;
  background: transparent;
  border: 1px dashed transparent;
  transition: all 0.2s ease;
}

.drop-gap.is-active {
  height: 18px;
  border-color: var(--primary-400);
  background: rgba(59, 130, 246, 0.08);
}

.empty-drop {
  border: 1px dashed var(--surface-300);
  border-radius: 0.75rem;
  padding: 2rem;
  text-align: center;
}

.empty-drop.is-active {
  border-color: var(--primary-400);
  background: rgba(59, 130, 246, 0.08);
}

:deep(.is-drop-active .p-card-body) {
  border: 1px solid var(--primary-300);
  border-radius: 0.75rem;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.12);
}

:deep(.rank-group .p-card-body),
:deep(.rank-group .p-card-content) {
  padding: 0.75rem;
}
</style>
