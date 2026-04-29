<script setup lang="ts">
  import { posCategories } from '~/utils/posCategories';
  import type { TagState } from '~/components/TriStateTag.vue';
  import ScrollPanel from 'primevue/scrollpanel';

  const emit = defineEmits<{
    reset: [];
  }>();

  const includePos = defineModel<string[]>('includePos', { required: true });
  const excludePos = defineModel<string[]>('excludePos', { required: true });
  const hideKanaOnly = defineModel<boolean>('hideKanaOnly', { required: true });

  const popover = ref();
  const searchQuery = ref('');

  const filteredCategories = computed(() => {
    if (!searchQuery.value) return posCategories;
    const query = searchQuery.value.toLowerCase();
    return posCategories
      .map((cat) => ({
        ...cat,
        tags: cat.tags.filter(
          (tag) => tag.label.toLowerCase().includes(query) || tag.value.toLowerCase().includes(query),
        ),
      }))
      .filter((cat) => cat.tags.length > 0);
  });

  const activeFilterCount = computed(() => {
    let count = includePos.value.length + excludePos.value.length;
    if (hideKanaOnly.value) count++;
    return count;
  });

  const getTagState = (tagValue: string): TagState => {
    if (includePos.value.includes(tagValue)) return 'include';
    if (excludePos.value.includes(tagValue)) return 'exclude';
    return 'neutral';
  };

  const updateTagState = (tagValue: string, state: TagState) => {
    if (state === 'include') {
      if (!includePos.value.includes(tagValue)) {
        includePos.value.push(tagValue);
      }
      excludePos.value = excludePos.value.filter((t) => t !== tagValue);
    } else if (state === 'exclude') {
      includePos.value = includePos.value.filter((t) => t !== tagValue);
      if (!excludePos.value.includes(tagValue)) {
        excludePos.value.push(tagValue);
      }
    } else {
      includePos.value = includePos.value.filter((t) => t !== tagValue);
      excludePos.value = excludePos.value.filter((t) => t !== tagValue);
    }
  };

  const handleReset = () => {
    searchQuery.value = '';
    emit('reset');
  };

  const toggle = (event: Event) => {
    popover.value.toggle(event);
  };

  defineExpose({ toggle });
</script>

<template>
  <div class="relative">
    <Button @click="toggle($event)">
      <Icon name="material-symbols:filter-list" size="1.25em" />
      Filters
    </Button>
    <Badge v-if="activeFilterCount > 0" :value="activeFilterCount" severity="warn" class="absolute -top-2 -right-2 pointer-events-none" />
  </div>

  <Popover ref="popover" class="w-full max-w-lg">
    <div class="flex flex-col gap-3 p-3 min-w-[280px]">
      <div class="flex items-center gap-2">
        <Checkbox v-model="hideKanaOnly" class="flex-shrink-0" inputId="hideKanaOnly" binary />
        <label for="hideKanaOnly" class="text-sm font-medium text-gray-600 dark:text-gray-300">Hide kana-only words</label>
      </div>

      <div class="border-t border-gray-200 dark:border-gray-700" />

      <div class="text-sm font-semibold text-gray-700 dark:text-gray-200">Parts of Speech</div>

      <IconField class="w-full">
        <InputIcon>
          <Icon name="material-symbols:search-rounded" />
        </InputIcon>
        <InputText v-model="searchQuery" type="text" placeholder="Search POS tags..." class="w-full" />
        <InputIcon v-if="searchQuery" class="cursor-pointer" @click="searchQuery = ''">
          <Icon name="material-symbols:close" />
        </InputIcon>
      </IconField>

      <ScrollPanel class="w-full" style="max-height: 60vh">
        <Accordion multiple>
          <AccordionPanel v-for="category in filteredCategories" :key="category.key" :value="category.key">
            <AccordionHeader>{{ category.label }}</AccordionHeader>
            <AccordionContent>
              <div class="flex flex-wrap gap-2 p-1">
                <TriStateTag
                  v-for="tag in category.tags"
                  :key="tag.value"
                  :label="tag.label"
                  :state="getTagState(tag.value)"
                  @update:state="(state) => updateTagState(tag.value, state)"
                />
              </div>
            </AccordionContent>
          </AccordionPanel>
        </Accordion>
      </ScrollPanel>

      <div class="flex justify-end pt-3 border-t border-gray-200 dark:border-gray-700">
        <Button severity="danger" size="small" @click="handleReset">
          <Icon name="material-symbols:refresh" class="mr-1" />
          Reset Filters
        </Button>
      </div>
    </div>
  </Popover>
</template>
