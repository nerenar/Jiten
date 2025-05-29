<script setup lang="ts">
  import { ref } from 'vue';
  import Card from 'primevue/card';
  import Button from 'primevue/button';
  import Select from 'primevue/select';
  import ConfirmDialog from 'primevue/confirmdialog';
  import Toast from 'primevue/toast';
  import { useConfirm } from 'primevue/useconfirm';
  import { useToast } from 'primevue/usetoast';
  import { MediaType } from '~/types/enums';
  import { getMediaTypeText } from '~/utils/mediaTypeMapper';

  useHead({
    title: 'Meta Administration - Jiten',
  });

  definePageMeta({
    middleware: ['auth'],
  });

  const { $api } = useNuxtApp();
  const toast = useToast();

  const confirm = useConfirm();
  const selectedMediaType = ref<MediaType | null>(null);
  const isLoading = ref({
    reparse: false,
    frequencies: false,
    difficulties: false,
  });

  const mediaTypes = Object.values(MediaType)
    .filter((value) => typeof value === 'number')
    .map((value) => ({
      value: value as MediaType,
      label: getMediaTypeText(value as MediaType),
    }));

  const confirmReparse = () => {
    if (!selectedMediaType.value) {
      return;
    }

    confirm.require({
      message: `Are you sure you want to reparse all media of type "${getMediaTypeText(selectedMediaType.value as MediaType)}"? This operation may take a long time.`,
      header: 'Confirmation',
      icon: 'pi pi-exclamation-triangle',
      acceptClass: 'p-button-primary',
      rejectClass: 'p-button-secondary',
      accept: () => reparseMedia(),
      reject: () => {},
    });
  };

  const reparseMedia = async () => {
    if (!selectedMediaType.value) {
      return;
    }

    try {
      isLoading.value.reparse = true;
      const data = await $api(`/admin/reparse-media-by-type/${selectedMediaType.value}`, {
        method: 'POST',
      });

      toast.add({
        severity: 'success',
        summary: 'Success',
        detail: `Reparsing ${data.count} media items of type ${getMediaTypeText(selectedMediaType.value as MediaType)}`,
        life: 5000,
      });
    } catch (error) {
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Failed to reparse media',
        life: 5000,
      });
      console.error('Error reparsing media:', error);
    } finally {
      isLoading.value.reparse = false;
    }
  };

  const confirmRecomputeFrequencies = () => {
    confirm.require({
      message: 'Are you sure you want to recompute all word frequencies? This operation may take a long time.',
      header: 'Confirmation',
      icon: 'pi pi-exclamation-triangle',
      acceptClass: 'p-button-primary',
      rejectClass: 'p-button-secondary',
      accept: () => recomputeFrequencies(),
      reject: () => {},
    });
  };

  const recomputeFrequencies = async () => {
    try {
      isLoading.value.frequencies = true;
      const data = await $api('/admin/recompute-frequencies', {
        method: 'POST',
      });

      toast.add({
        severity: 'success',
        summary: 'Success',
        detail: 'Recomputing frequencies job has been queued',
        life: 5000,
      });
    } catch (error) {
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Failed to recompute frequencies',
        life: 5000,
      });
      console.error('Error recomputing frequencies:', error);
    } finally {
      isLoading.value.frequencies = false;
    }
  };

  const confirmRecomputeDifficulties = () => {
    confirm.require({
      message: 'Are you sure you want to recompute all media difficulties? This operation may take a long time.',
      header: 'Confirmation',
      icon: 'pi pi-exclamation-triangle',
      acceptClass: 'p-button-primary',
      rejectClass: 'p-button-secondary',
      accept: () => recomputeDifficulties(),
      reject: () => {},
    });
  };

  const recomputeDifficulties = async () => {
    try {
      isLoading.value.difficulties = true;
      const data = await $api('/admin/recompute-difficulties', {
        method: 'POST',
      });

      toast.add({
        severity: 'success',
        summary: 'Success',
        detail: 'Recomputing difficulties job has been queued',
        life: 5000,
      });
    } catch (error) {
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Failed to recompute difficulties',
        life: 5000,
      });
      console.error('Error recomputing difficulties:', error);
    } finally {
      isLoading.value.difficulties = false;
    }
  };
</script>

<template>
  <div class="container mx-auto p-4">
    <Toast />
    <ConfirmDialog />

    <div class="flex items-center mb-6">
      <Button
        icon="pi pi-arrow-left"
        class="p-button-text mr-2"
        @click="navigateTo('/dashboard')"
      />
      <h1 class="text-3xl font-bold">Data Management</h1>
    </div>

    <div class="grid grid-cols-1 gap-6">
      <Card class="shadow-md">
        <template #title>Reparse Media</template>
        <template #content>
          <p class="mb-4">
            Reparse all media of the selected type
          </p>
          <div class="mb-4">
            <label for="mediaType" class="block text-sm font-medium mb-1">Media Type</label>
            <Select
              id="mediaType"
              v-model="selectedMediaType"
              :options="mediaTypes"
              option-label="label"
              option-value="value"
              placeholder="Select Media Type"
              class="w-full"
            />
          </div>

          <div class="flex justify-center">
            <Button
              label="Reparse All Media of This Type"
              icon="pi pi-refresh"
              class="p-button-warning"
              :disabled="!selectedMediaType || isLoading.reparse"
              :loading="isLoading.reparse"
              @click="confirmReparse"
            />
          </div>
        </template>
      </Card>

      <Card class="shadow-md">
        <template #title>Recompute Frequencies</template>
        <template #content>
          <p class="mb-4">
            Recompute all vocabulary frequencies.
          </p>

          <div class="flex justify-center">
            <Button
              label="Recompute Frequencies"
              icon="pi pi-chart-bar"
              class="p-button-warning"
              :disabled="isLoading.frequencies"
              :loading="isLoading.frequencies"
              @click="confirmRecomputeFrequencies"
            />
          </div>
        </template>
      </Card>

<!--      <Card class="shadow-md">-->
<!--        <template #title>Recompute Difficulties</template>-->
<!--        <template #content>-->
<!--          <p class="mb-4">-->
<!--            Recompute all media difficulties.-->
<!--          </p>-->

<!--          <div class="flex justify-center">-->
<!--            <Button-->
<!--              label="Recompute Difficulties"-->
<!--              icon="pi pi-chart-line"-->
<!--              class="p-button-warning"-->
<!--              :disabled="isLoading.difficulties"-->
<!--              :loading="isLoading.difficulties"-->
<!--              @click="confirmRecomputeDifficulties"-->
<!--            />-->
<!--          </div>-->
<!--        </template>-->
<!--      </Card>-->
    </div>
  </div>
</template>

<style scoped></style>
