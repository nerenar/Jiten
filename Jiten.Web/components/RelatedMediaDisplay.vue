<script setup lang="ts">
  import { ref, computed, watch, onMounted, onBeforeUnmount, nextTick } from 'vue';
  import Tag from 'primevue/tag';
  import { type DeckRelationship, DeckRelationshipType } from '~/types';

  interface Props {
    relationships: DeckRelationship[];
    minVisibleItems?: number;
    buttonBuffer?: number;
    gapSize?: number;
  }

  const props = withDefaults(defineProps<Props>(), {
    minVisibleItems: 1,
    buttonBuffer: 75,
    gapSize: 6,
  });

  const relationshipSortOrder: DeckRelationshipType[] = [
    DeckRelationshipType.Prequel,
    DeckRelationshipType.Sequel,
    DeckRelationshipType.Adaptation,
    DeckRelationshipType.Alternative,
    DeckRelationshipType.Fandisc,
    DeckRelationshipType.Spinoff,
    DeckRelationshipType.SideStory,
    DeckRelationshipType.HasFandisc,
    DeckRelationshipType.HasSpinoff,
    DeckRelationshipType.HasSideStory,
    DeckRelationshipType.SourceMaterial,
  ];

  const relationshipTypeLabels: Record<DeckRelationshipType, string> = {
    [DeckRelationshipType.Sequel]: 'Sequel',
    [DeckRelationshipType.Fandisc]: 'Source',
    [DeckRelationshipType.Spinoff]: 'Source',
    [DeckRelationshipType.SideStory]: 'Source',
    [DeckRelationshipType.Adaptation]: 'Adaptation',
    [DeckRelationshipType.Alternative]: 'Alternative',
    [DeckRelationshipType.Prequel]: 'Prequel',
    [DeckRelationshipType.HasFandisc]: 'Fandisc',
    [DeckRelationshipType.HasSpinoff]: 'Spinoff',
    [DeckRelationshipType.HasSideStory]: 'Side Story',
    [DeckRelationshipType.SourceMaterial]: 'Source',
  };

  function getRelationshipTypeLabel(type: DeckRelationshipType): string {
    return relationshipTypeLabels[type] ?? 'Unknown';
  }

  const expanded = ref(false);
  const containerRef = ref<HTMLElement | null>(null);
  const labelRef = ref<HTMLElement | null>(null);
  const itemRefs = ref<HTMLElement[]>([]);
  const visibleCount = ref<number>(Infinity);
  const isCalculating = ref(false);

  const sortedRelationships = computed(() => {
    return [...props.relationships].sort((a, b) => {
      const indexA = relationshipSortOrder.indexOf(a.relationshipType);
      const indexB = relationshipSortOrder.indexOf(b.relationshipType);
      const orderA = indexA === -1 ? Infinity : indexA;
      const orderB = indexB === -1 ? Infinity : indexB;
      return orderA - orderB;
    });
  });

  const hasOverflow = computed(() => !expanded.value && sortedRelationships.value.length > visibleCount.value);
  const hiddenCount = computed(() => Math.max(0, sortedRelationships.value.length - visibleCount.value));

  const calculateVisibleCount = async () => {
    if (!containerRef.value || sortedRelationships.value.length === 0 || expanded.value || isCalculating.value) {
      return;
    }

    isCalculating.value = true;
    visibleCount.value = sortedRelationships.value.length;
    await nextTick();

    if (!containerRef.value || !labelRef.value) {
      isCalculating.value = false;
      return;
    }

    // Use getBoundingClientRect for sub-pixel accuracy
    const containerWidth = containerRef.value.getBoundingClientRect().width;
    const labelWidth = labelRef.value.getBoundingClientRect().width;

    // Tailwind mr-1 is 4px.
    // We start with the label and the initial gap.
    let accumulatedWidth = labelWidth + 4;
    let count = 0;

    for (let i = 0; i < itemRefs.value.length; i++) {
      const el = itemRefs.value[i];
      if (!el) continue;

      const itemWidth = el.getBoundingClientRect().width;

      // Only add the gap if it's not the very first item
      // (Flex gap applies between items)
      const currentGap = i === 0 ? 0 : props.gapSize;

      // Check if item fits PLUS the " +N more" button buffer
      // We only need the buffer if we aren't on the very last item
      const isLastItem = i === sortedRelationships.value.length - 1;
      const neededBuffer = isLastItem ? 0 : props.buttonBuffer;

      if (accumulatedWidth + currentGap + itemWidth + neededBuffer <= containerWidth) {
        accumulatedWidth += (currentGap + itemWidth);
        count++;
      } else {
        break;
      }
    }

    visibleCount.value = Math.max(props.minVisibleItems, count);
    isCalculating.value = false;
  };


  let resizeObserver: ResizeObserver | null = null;

  onMounted(() => {
    calculateVisibleCount();

    if (containerRef.value) {
      resizeObserver = new ResizeObserver(() => {
        window.requestAnimationFrame(() => {
          calculateVisibleCount();
        });
      });
      resizeObserver.observe(containerRef.value);
    }
  });

  onBeforeUnmount(() => {
    if (resizeObserver && containerRef.value) {
      resizeObserver.disconnect();
    }
  });

  watch(
    () => props.relationships,
    () => {
      expanded.value = false;
      itemRefs.value = [];
      nextTick(calculateVisibleCount);
    },
    { deep: true }
  );

  const toggleExpanded = () => {
    expanded.value = !expanded.value;
    if (!expanded.value) {
      calculateVisibleCount();
    } else {
      visibleCount.value = sortedRelationships.value.length;
    }
  };
</script>

<template>
  <div v-if="sortedRelationships.length > 0" ref="containerRef" class="flex flex-wrap gap-1.5 items-center w-full relative">
    <span ref="labelRef" class="text-xs font-semibold text-gray-500 uppercase tracking-wider mr-1 shrink-0"> Related </span>

    <NuxtLink
      v-for="(rel, index) in sortedRelationships"
      :key="`${rel.targetDeckId}-${rel.relationshipType}`"
      :ref="
        (el) => {
          if (el) itemRefs[index] = el as HTMLElement;
        }
      "
      :to="`/decks/media/${rel.targetDeckId}/detail`"
      :class="{ hidden: !expanded && index >= visibleCount }"
      class="px-2 py-0.5 bg-surface-100 dark:bg-surface-900/50 text-surface-700 dark:text-surface-200 rounded-full text-xs hover:bg-surface-200 dark:hover:bg-surface-800/50 transition-colors"
    >
      <span class="font-medium">{{ getRelationshipTypeLabel(rel.relationshipType) }}:</span>
      <span class="ml-1">{{ localiseTitle(rel.targetDeck) }}</span>
    </NuxtLink>

    <Tag
      v-if="hasOverflow || expanded"
      class="cursor-pointer hover:!bg-gray-200 dark:hover:!bg-gray-700 transition-colors text-xs !py-0.5 !px-2"
      severity="secondary"
      rounded
      @click="toggleExpanded"
    >
      <span class="flex items-center gap-1">
        {{ expanded ? 'Less' : `+${hiddenCount}` }}
        <i :class="['pi text-[10px]', expanded ? 'pi-chevron-up' : 'pi-chevron-down']"></i>
      </span>
    </Tag>
  </div>
</template>
