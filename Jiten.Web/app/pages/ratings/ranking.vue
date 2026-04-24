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

const expandedGroups = ref(new Set<number>());

const displayGroups = computed(() => {
  if (!activeSection.value) return [];
  return [...activeSection.value.groups].reverse();
});

const tierQuality = computed(() => {
  if (!activeSection.value) return null;
  const groups = activeSection.value.groups.length;
  const ranked = activeSection.value.groups.reduce((sum, g) => sum + g.decks.length, 0);
  if (ranked < 2) return null;

  const ideal = Math.min(Math.max(Math.ceil(ranked / 5), 3), 10);
  const progress = Math.min(Math.round((groups / ideal) * 100), 100);
  const remaining = ideal - groups;

  let label: string;
  if (groups >= ideal) label = 'Good granularity';
  else label = `Add ${remaining} more rank${remaining > 1 ? 's' : ''} for better accuracy`;

  return { progress, label };
});

function rankColor(index: number, total: number): string {
  if (total <= 1) return 'hsl(210, 50%, 50%)';
  const t = index / (total - 1);
  const hue = t * 130;
  return `hsl(${hue}, 65%, 45%)`;
}

let ghost: HTMLElement | null = null;
let offsetX = 0;
let offsetY = 0;
let dragRaf = 0;

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
  cancelAnimationFrame(dragRaf);

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

  if (ghost) cancelDrag();

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
  clone.style.willChange = 'transform';
  clone.style.transform = 'scale(1.02)';
  document.body.appendChild(clone);
  ghost = clone;

  document.addEventListener('pointermove', onPointerMove);
  document.addEventListener('pointerup', onPointerUp);
  document.addEventListener('pointercancel', onPointerUp);
  window.addEventListener('blur', cancelDrag);
}

function updateDropTarget(ev: PointerEvent) {
  if (ghost) ghost.style.display = 'none';
  const el = document.elementFromPoint(ev.clientX, ev.clientY) as HTMLElement | null;
  if (ghost) ghost.style.display = '';
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
  const x = ev.clientX;
  const y = ev.clientY;
  cancelAnimationFrame(dragRaf);
  dragRaf = requestAnimationFrame(() => {
    if (!ghost) return;
    ghost.style.left = `${x - offsetX}px`;
    ghost.style.top = `${y - offsetY}px`;
  });
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
    const totalGroups = activeSection.value.groups.length;
    await applyMove({
      deckId,
      mode: DifficultyRankingMoveMode.Insert,
      insertIndex: totalGroups - dragOver.value.index,
    });
  }
}

async function mergeWithRank(deckId: number, targetGroupId: number) {
  await applyMove({ deckId, mode: DifficultyRankingMoveMode.Merge, targetGroupId });
}

async function insertNewRank(deckId: number, displayGapIndex: number) {
  if (!activeSection.value) return;
  const totalGroups = activeSection.value.groups.length;
  await applyMove({ deckId, mode: DifficultyRankingMoveMode.Insert, insertIndex: totalGroups - displayGapIndex });
}

async function onPointerUp() {
  try {
    await handleDrop();
  } finally {
    cancelDrag();
  }
}

onMounted(loadRankings);
onUnmounted(cancelDrag);
</script>

<template>
  <div class="p-2 md:p-4 overflow-hidden">
    <div class="mb-6">
      <div class="flex items-center gap-2">
        <Button icon="pi pi-arrow-left" class="p-button-text" @click="navigateTo('/ratings')" />
        <h1 class="text-2xl font-bold">Rank Difficulties</h1>
      </div>
      <p class="text-muted-color text-sm mt-1">
        Drag media to rank them. Drop on a group if they're about the same difficulty, or between groups to create a new rank.
      </p>
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

      <div v-if="tierQuality" class="mb-3 flex items-center gap-3">
        <ProgressBar :value="tierQuality.progress" :showValue="false" style="height: 6px; width: 120px" />
        <span class="text-xs" :class="tierQuality.progress >= 100 ? 'text-green-500' : 'text-muted-color'">{{ tierQuality.label }}</span>
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
                <i class="pi pi-bars drag-handle" />
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
          <div v-if="displayGroups.length > 0" class="flex items-center gap-2 mb-2 text-xs font-semibold uppercase tracking-wide" :style="{ color: rankColor(0, displayGroups.length) }">
            <i class="pi pi-arrow-up text-[0.6rem]" /> Hardest
          </div>
          <div
            v-if="displayGroups.length === 0"
            class="empty-drop text-muted-color"
            :class="{ 'is-active': dragOver?.kind === 'gap' && dragOver.index === 0 }"
            :data-drop-gap="0"
          >
            Drop here to create the first rank.
          </div>

          <div v-else class="flex flex-col" :class="{ 'is-dragging': draggingDeck }">
            <div
              v-for="(group, index) in displayGroups"
              :key="group.id"
            >
              <div
                class="drop-gap"
                :class="{ 'is-active': dragOver?.kind === 'gap' && dragOver.index === index }"
                :data-drop-gap="index"
              >
                <div class="drop-gap-line" />
                <span class="drop-gap-label">Drop to create a new rank</span>
                <div class="drop-gap-line" />
              </div>

              <Card
                class="rank-group min-w-0"
                :class="{ 'is-active': dragOver?.kind === 'group' && dragOver.groupId === group.id }"
                :style="{ borderLeft: `3px solid ${rankColor(index, displayGroups.length)}` }"
                :data-drop-group="group.id"
              >
                <template #content>
                  <div class="flex items-center justify-between mb-2">
                    <div class="text-xs uppercase tracking-wide font-semibold" :style="{ color: rankColor(index, displayGroups.length) }">Rank {{ index + 1 }}</div>
                    <Transition name="fade-label">
                      <div
                        v-if="draggingDeck && dragOver?.kind === 'group' && dragOver.groupId === group.id"
                        class="drop-tie-label"
                      >
                        Drop to tie
                      </div>
                      <div v-else class="text-xs text-muted-color">{{ group.decks.length > 1 ? `${group.decks.length} tied` : '' }}</div>
                    </Transition>
                  </div>
                  <div class="flex flex-col gap-2">
                    <div
                      v-for="deck in (expandedGroups.has(group.id) ? group.decks : group.decks.slice(0, 3))"
                      :key="deck.id"
                      class="deck-pill"
                      @pointerdown="onPointerDown(deck, group.id, $event)"
                    >
                      <i class="pi pi-bars drag-handle" />
                      <img :src="deck.coverUrl || '/img/nocover.jpg'" :alt="deckTitle(deck)" class="h-10 w-7 rounded object-cover" />
                      <div class="flex-1 min-w-0">
                        <div class="truncate text-sm font-medium">{{ deckTitle(deck) }}</div>
                      </div>
                      <Tooltip :content="`↑↑ New rank above\n↑ Move to rank above\n↓ Move to rank below\n↓↓ New rank below`" block>
                        <div class="rank-actions" @pointerdown.stop>
                          <button class="rank-btn" :disabled="isUpdating || group.decks.length === 1" @click.stop="insertNewRank(deck.id, index)">
                            <i class="pi pi-angle-double-up" />
                          </button>
                          <button class="rank-btn" :disabled="isUpdating || index === 0" @click.stop="mergeWithRank(deck.id, displayGroups[index - 1].id)">
                            <i class="pi pi-angle-up" />
                          </button>
                          <button class="rank-btn" :disabled="isUpdating || index === displayGroups.length - 1" @click.stop="mergeWithRank(deck.id, displayGroups[index + 1].id)">
                            <i class="pi pi-angle-down" />
                          </button>
                          <button class="rank-btn" :disabled="isUpdating || group.decks.length === 1" @click.stop="insertNewRank(deck.id, index + 1)">
                            <i class="pi pi-angle-double-down" />
                          </button>
                        </div>
                      </Tooltip>
                    </div>
                    <button
                      v-if="group.decks.length > 3"
                      class="expand-toggle"
                      @click="expandedGroups.has(group.id) ? expandedGroups.delete(group.id) : expandedGroups.add(group.id)"
                    >
                      {{ expandedGroups.has(group.id) ? 'Show less' : `+${group.decks.length - 3} more` }}
                    </button>
                  </div>
                </template>
              </Card>
            </div>

            <div
              class="drop-gap"
              :class="{ 'is-active': dragOver?.kind === 'gap' && dragOver.index === displayGroups.length }"
              :data-drop-gap="displayGroups.length"
            >
              <div class="drop-gap-line" />
              <span class="drop-gap-label">Drop to create a new rank</span>
              <div class="drop-gap-line" />
            </div>
          </div>

          <div v-if="displayGroups.length > 0" class="flex items-center gap-2 mt-2 text-xs font-semibold uppercase tracking-wide" :style="{ color: rankColor(displayGroups.length - 1, displayGroups.length) }">
            <i class="pi pi-arrow-down text-[0.6rem]" /> Easiest
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
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
  user-select: none;
  touch-action: none;
}

.deck-pill:hover {
  border-color: var(--primary-300);
  background: var(--surface-50);
}

.rank-actions {
  display: flex;
  gap: 2px;
  shrink: 0;
}

.rank-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 1.5rem;
  height: 1.5rem;
  border: none;
  border-radius: 0.25rem;
  background: transparent;
  color: var(--surface-400);
  font-size: 0.75rem;
  cursor: pointer;
  transition: color 0.15s, background-color 0.15s;
  touch-action: manipulation;
}

.rank-btn:hover:not(:disabled) {
  color: var(--primary-500);
  background: var(--primary-50);
}

.rank-btn:active:not(:disabled) {
  background: var(--primary-100);
}

.rank-btn:disabled {
  opacity: 0.25;
  cursor: default;
}

.expand-toggle {
  background: none;
  border: 1px dashed var(--surface-300);
  border-radius: 0.375rem;
  padding: 0.25rem 0.5rem;
  font-size: 0.75rem;
  color: var(--primary-500);
  cursor: pointer;
  text-align: center;
  transition: border-color 0.15s, background-color 0.15s;
}

.expand-toggle:hover {
  border-color: var(--primary-400);
  background: var(--primary-50);
}

.drag-handle {
  font-size: 0.75rem;
  color: var(--surface-300);
  transition: color 0.15s;
}

.deck-pill:hover .drag-handle {
  color: var(--surface-500);
}

.drop-tie-label {
  font-size: 0.6875rem;
  font-weight: 600;
  color: var(--primary-500);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.fade-label-enter-active,
.fade-label-leave-active {
  transition: opacity 0.15s ease;
}

.fade-label-enter-from,
.fade-label-leave-to {
  opacity: 0;
}

.rank-group.is-active {
  outline: 2px solid var(--primary-400);
  outline-offset: -1px;
  box-shadow: 0 0 0 4px rgba(59, 130, 246, 0.12);
}

:deep(.rank-group.is-active .p-card-body) {
  position: relative;
}

.drop-gap {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  height: 8px;
  transition: all 0.2s ease;
  opacity: 0;
  pointer-events: none;
}

.drop-gap-line {
  flex: 1;
  height: 2px;
  border-radius: 1px;
  background: var(--primary-300);
}

.drop-gap-label {
  font-size: 0.6875rem;
  font-weight: 600;
  color: var(--primary-400);
  white-space: nowrap;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.is-dragging .drop-gap {
  opacity: 0.5;
  height: 28px;
  padding: 4px 8px;
  pointer-events: auto;
}

.is-dragging .drop-gap:hover,
.is-dragging .drop-gap.is-active {
  opacity: 1;
  height: 36px;
}

.is-dragging .drop-gap.is-active .drop-gap-line {
  height: 3px;
  background: var(--primary-500);
  box-shadow: 0 0 8px rgba(59, 130, 246, 0.3);
}

.is-dragging .drop-gap.is-active .drop-gap-label {
  color: var(--primary-500);
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

.rank-group {
  border-radius: 0.5rem;
  overflow: hidden;
}
</style>
