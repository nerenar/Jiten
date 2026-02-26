<script setup lang="ts">
defineProps<{
  hasUpvoted: boolean;
  upvoteCount: number;
  compact?: boolean;
}>();

defineEmits<{
  toggle: [];
}>();
</script>

<template>
  <div v-if="compact" class="flex flex-col items-center gap-1 shrink-0">
    <Button
      icon="pi pi-chevron-up"
      :severity="hasUpvoted ? 'primary' : 'secondary'"
      :outlined="!hasUpvoted"
      size="small"
      rounded
      v-tooltip.top="hasUpvoted ? 'Remove upvote' : 'Upvote'"
      @click="$emit('toggle')"
    />
    <span class="text-sm font-semibold">{{ upvoteCount }}</span>
  </div>
  <Button
    v-else
    icon="pi pi-chevron-up"
    :label="String(upvoteCount)"
    :severity="hasUpvoted ? 'primary' : 'secondary'"
    :outlined="!hasUpvoted"
    @click="$emit('toggle')"
  />
</template>
