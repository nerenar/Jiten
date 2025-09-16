<script setup lang="ts">
  import { ref, computed, watch, onBeforeUnmount, onMounted } from 'vue'; // Added onMounted
  import Card from 'primevue/card';
  import Button from 'primevue/button';
  import FileUpload from 'primevue/fileupload';
  import InputText from 'primevue/inputtext';
  import Dialog from 'primevue/dialog';
  import Toast from 'primevue/toast';
  import DataTable from 'primevue/datatable';
  import Column from 'primevue/column';
  import { useToast } from 'primevue/usetoast';
  import { LinkType } from '~/types';
  import type { DeckDetail, Link, MediaType } from '~/types';
  import { getMediaTypeText, getChildrenCountText } from '~/utils/mediaTypeMapper';
  import { getLinkTypeText } from '~/utils/linkTypeMapper';

  const route = useRoute();
  const mediaId = route.params.id;

  useHead({
    title: 'Edit Media - Admin Dashboard - Jiten',
  });

  definePageMeta({
    middleware: ['auth'],
  });

  const selectedMediaType = ref<MediaType | null>(null);
  const toast = useToast();
  const { $api } = useNuxtApp();

  function showToast(severity: 'success' | 'info' | 'warn' | 'error', summary: string, detail: string = '') {
    toast.add({ severity, summary, detail, life: 3000 });
  }

  const selectedFile = ref<File | null>(null);
  const originalTitle = ref('');
  const romajiTitle = ref('');
  const englishTitle = ref('');
  const releaseDate = ref<Date>();
  const description = ref('');
  const difficultyOverride = ref(0);

  const coverImage = ref<File | null>(null);
  const coverImageUrl = ref<string | null>(null);
  const coverImageObjectUrl = ref<string | null>(null);

  const links = ref<Link[]>([]);
  const showAddLinkDialog = ref(false);
  const showEditLinkDialog = ref(false);
  const newLink = ref<{ url: string; linkType: LinkType }>({
    url: '',
    linkType: LinkType.Web,
  });
  const editingLink = ref<{ index: number; url: string; linkType: LinkType } | null>(null);

  const newSubdeckUploaderRef = ref<InstanceType<typeof FileUpload> | null>(null);

  const availableLinkTypes = computed(() => {
    return Object.values(LinkType)
      .filter((value) => typeof value === 'number')
      .map((value) => ({
        value: value as LinkType,
        label: getLinkTypeText(value as LinkType),
      }));
  });

  const subdecks = ref<
    Array<{
      id: number;
      originalTitle: string;
      file: File | null;
      mediaSubdeckId?: number; // Added to track existing subdeck IDs
      difficultyOverride: number;
    }>
  >([]);
  let nextSubdeckId = 1;

  const subdeckDefaultName = computed(() => {
    if (!selectedMediaType.value) return '';

    const baseText = getChildrenCountText(selectedMediaType.value);
    const singularText = baseText.endsWith('s') ? baseText.slice(0, -1) : baseText;
    return singularText;
  });

  watch(coverImage, (newFile) => {
    if (coverImageObjectUrl.value) {
      URL.revokeObjectURL(coverImageObjectUrl.value);
      coverImageObjectUrl.value = null;
    }
    if (newFile && typeof window !== 'undefined') {
      coverImageObjectUrl.value = URL.createObjectURL(newFile);
    }
  });

  onBeforeUnmount(() => {
    if (coverImageObjectUrl.value) {
      URL.revokeObjectURL(coverImageObjectUrl.value);
      coverImageObjectUrl.value = null;
    }
  });

  const { data: response, status, error } = await useApiFetch<DeckDetail>(`admin/deck/${mediaId}`);

  watchEffect(() => {
    if (error.value) {
      throw new Error(error.value.message || 'Failed to fetch deck data');
    }

    if (response.value) {
      const mainDeck = response.value.mainDeck;

      selectedMediaType.value = mainDeck.mediaType;
      originalTitle.value = mainDeck.originalTitle || '';
      romajiTitle.value = mainDeck.romajiTitle || '';
      englishTitle.value = mainDeck.englishTitle || '';
      description.value = mainDeck.description || '';
      releaseDate.value = new Date(mainDeck.releaseDate) || new Date();
      difficultyOverride.value = mainDeck.difficultyOverride || 0;

      if (mainDeck.coverName) {
        coverImageUrl.value = `${mainDeck.coverName}`;
      }

      links.value = mainDeck.links || [];

      if (response.value.subDecks && response.value.subDecks.length > 0) {
        subdecks.value = response.value.subDecks.map((subdeck, index) => ({
          id: nextSubdeckId++,
          originalTitle: subdeck.originalTitle || `${subdeckDefaultName.value} ${index + 1}`,
          file: null,
          mediaSubdeckId: subdeck.deckId,
          difficultyOverride: subdeck.difficultyOverride || -1,
        }));
      }

      selectedFile.value = new File([], 'dummy.file');
    }
  });

  function handleCoverImageUpload(event: { files: File[] }) {
    if (event.files && event.files.length > 0) {
      const file = event.files[0];
      coverImage.value = file;
      coverImageUrl.value = null;
    }
  }

  function handleSubdeckFileUpload(event: { files: File[] }, subdeckId: number) {
    if (event.files && event.files.length > 0) {
      for (const file of event.files) {
        const subdeck = subdecks.value.find((sd) => sd.id === subdeckId);
        if (subdeck) {
          subdeck.file = file;
        }
      }
    }
  }

  function handleNewSubdeckFileUpload(event: { files: File[] }) {
    if (event.files && event.files.length > 0) {
      for (const file of event.files) {
        const newSubdeckNumber = subdecks.value.length + 1;
        subdecks.value.push({
          id: nextSubdeckId++,
          originalTitle: `${subdeckDefaultName.value} ${newSubdeckNumber}`,
          file: file,
          difficultyOverride: -1,
        });
      }
      // Explicitly clear the FileUpload component's selection
      if (newSubdeckUploaderRef.value) {
        newSubdeckUploaderRef.value.clear();
      }
    }
  }

  function addSubdeck() {
    const newSubdeckNumber = subdecks.value.length + 1;
    subdecks.value.push({
      id: nextSubdeckId++,
      originalTitle: `${subdeckDefaultName.value} ${newSubdeckNumber}`,
      file: null,
      difficultyOverride: -1,
    });
  }

  function removeSubdeck(id: number) {
    const index = subdecks.value.findIndex((sd) => sd.id === id);
    if (index === -1) return;

    const subdeck = subdecks.value[index];
    if (subdeck.mediaSubdeckId) {
      if (!confirm('Are you sure you want to remove this subdeck? This action cannot be undone.')) {
        return;
      }
    }

    subdecks.value.splice(index, 1);
  }

  function openAddLinkDialog() {
    newLink.value = {
      url: '',
      linkType: LinkType.Web,
    };
    showAddLinkDialog.value = true;
  }

  function addLink() {
    if (!newLink.value.url.trim()) {
      showToast('warn', 'Validation Error', 'URL is required');
      return;
    }

    links.value.push({
      linkId: 0,
      url: newLink.value.url,
      linkType: newLink.value.linkType.toString(),
      deckId: parseInt(mediaId as string),
    });

    showAddLinkDialog.value = false;
  }

  function openEditLinkDialog(index: number) {
    const link = links.value[index];
    editingLink.value = {
      index,
      url: link.url,
      linkType: parseInt(link.linkType) as LinkType,
    };
    showEditLinkDialog.value = true;
  }

  function saveEditedLink() {
    if (!editingLink.value || !editingLink.value.url.trim()) {
      showToast('warn', 'Validation Error', 'URL is required');
      return;
    }

    const index = editingLink.value.index;
    links.value[index] = {
      ...links.value[index],
      url: editingLink.value.url,
      linkType: editingLink.value.linkType.toString(),
    };

    showEditLinkDialog.value = false;
    editingLink.value = null;
  }

  function removeLink(index: number) {
    links.value.splice(index, 1);
  }

  async function fetchMetadata() {
    try {
      const data = await $api('admin/fetch-metadata/' + mediaId, {
        method: 'POST',
      });

      toast.add({
        severity: 'success',
        summary: 'Success',
        detail: 'Fetching metadata has been queued',
        life: 5000,
      });
    } catch (error) {
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Failed to fetch metadata',
        life: 5000,
      });
      console.error('Error fetching metadata:', error);
    } finally {
    }
  }

  async function update(reparse: boolean = false) {
    if (!originalTitle.value.trim()) {
      showToast('warn', 'Validation Error', 'Original title is required');
      return;
    }

    if (!coverImage.value && !coverImageUrl.value) {
      showToast('warn', 'Validation Error', 'Cover image is required');
      return;
    }

    try {
      const formData = new FormData();
      formData.append('reparse', reparse.toString());
      formData.append('deckId', mediaId.toString());
      formData.append('mediaType', selectedMediaType.value?.toString() || '');
      formData.append('originalTitle', originalTitle.value);
      formData.append('romajiTitle', romajiTitle.value);
      formData.append('englishTitle', englishTitle.value);
      formData.append('releaseDate', formatDateAsYyyyMmDd(releaseDate.value));
      formData.append('description', description.value);
      formData.append('difficultyOverride', difficultyOverride.value);

      if (coverImage.value) {
        formData.append('coverImage', coverImage.value);
      } else if (coverImageUrl.value) {
        formData.append('coverImageUrl', coverImageUrl.value);
      }

      if (links.value && links.value.length > 0) {
        for (let i = 0; i < links.value.length; i++) {
          const link = links.value[i];
          formData.append(`links[${i}].url`, link.url);
          formData.append(`links[${i}].linkType`, link.linkType);
          if (link.linkId > 0) {
            formData.append(`links[${i}].linkId`, link.linkId.toString());
          }
        }
      }

      if (subdecks.value.length > 0) {
        for (let i = 0; i < subdecks.value.length; i++) {
          const subdeck = subdecks.value[i];
          formData.append(`subdecks[${i}].originalTitle`, subdeck.originalTitle);

          // Include the existing subdeck ID if available
          if (subdeck.mediaSubdeckId) {
            formData.append(`subdecks[${i}].deckId`, subdeck.mediaSubdeckId.toString());
          }

          formData.append(`subdecks[${i}].deckOrder`, (i + 1).toString());
          formData.append(`subdecks[${i}].difficultyOverride`, subdeck.difficultyOverride.toString());

          // Include the file if available
          if (subdeck.file) {
            formData.append(`subdecks[${i}].file`, subdeck.file);
          }
        }
      }

      const data = await $api('admin/update-deck', {
        method: 'POST',
        body: formData,
      });

      showToast('success', 'Success', 'Media updated successfully!');
    } catch (error) {
      console.error('Error updating media:', error);
      showToast('error', 'Update Error', 'An error occurred while updating. Please try again.');
    }
  }
</script>

<template>
  <div>
    <Toast />
    <div class="container mx-auto p-4">
      <div class="flex items-center mb-6">
        <Button icon="pi pi-arrow-left" class="p-button-text mr-2" @click="navigateTo('/dashboard')" />
        <h1 class="text-3xl font-bold">Edit Media</h1>
        <div class="ml-auto">
          <Button @click="navigateTo(`/decks/media/${mediaId}/detail`)">
            <Icon name="ic:baseline-remove-red-eye" />
            View Deck
          </Button>
        </div>
      </div>

      <!-- Loading indicator -->
      <div v-if="status == 'pending'" class="flex justify-center items-center h-64">
        <div class="text-center">
          <div class="spinner-border text-primary" role="status">
            <span class="sr-only">Loading...</span>
          </div>
          <p class="mt-2">Loading media data...</p>
        </div>
      </div>

      <!-- File Upload Screen -->
      <div v-else class="mt-6">
        <div class="flex items-center mb-4">
          <h2 class="text-xl font-semibold">Edit {{ getMediaTypeText(selectedMediaType!) }}</h2>
        </div>

        <!-- File details card -->
        <Card class="mb-6">
          <template #title>Media Details</template>
          <template #content>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <div class="mb-4">
                  <label class="block text-sm font-medium mb-1">Original Title</label>
                  <InputText v-model="originalTitle" class="w-full" />
                </div>
                <div class="mb-4">
                  <label class="block text-sm font-medium mb-1">Romaji Title</label>
                  <InputText v-model="romajiTitle" class="w-full" />
                </div>
                <div class="mb-4">
                  <label class="block text-sm font-medium mb-1">English Title</label>
                  <InputText v-model="englishTitle" class="w-full" />
                </div>
                <div class="mb-4">
                  <label class="block text-sm font-medium mb-1">Release Date</label>
                  <DatePicker v-model="releaseDate" class="w-full" />
                </div>
                <div class="mb-4">
                  <label class="block text-sm font-medium mb-1">Description</label>
                  <Textarea v-model="description" class="w-full" />
                </div>
                <div class="mb-4">
                  <label class="block text-sm font-medium mb-1">Difficulty Override</label>
                  <InputNumber v-model="difficultyOverride" class="w-full" />
                </div>
              </div>
              <div>
                <div class="mb-4">
                  <label class="block text-sm font-medium mb-1">Cover Image</label>
                  <!-- Show image preview if available (either from URL or local file) -->
                  <div v-if="coverImageUrl || coverImageObjectUrl" class="flex items-center mb-2">
                    <img :src="coverImageUrl || coverImageObjectUrl" alt="Cover Preview" class="h-48 w-auto mr-2 object-contain border" />
                  </div>
                  <FileUpload
                    mode="advanced"
                    accept="image/*"
                    :auto="true"
                    choose-label="Select Cover Image"
                    :multiple="false"
                    class="w-full cover-image-upload"
                    :custom-upload="true"
                    :show-upload-button="false"
                    :show-cancel-button="false"
                    drag-drop-text="Select Cover Image or Drag and Drop Here"
                    @select="handleCoverImageUpload"
                  />
                </div>
              </div>
            </div>

            <!-- Deck Information Table -->
            <div v-if="response && response.mainDeck" class="mt-6">
              <h3 class="text-lg font-medium mb-2">Deck Information</h3>
              <div class="p-4 border rounded mb-4">
                <div class="grid grid-cols-2 gap-4">
                  <div>
                    <p><strong>Media Type:</strong> {{ getMediaTypeText(response.mainDeck.mediaType) }}</p>
                    <p><strong>Deck ID:</strong> {{ response.mainDeck.deckId }}</p>
                    <p><strong>Word Count:</strong> {{ response.mainDeck.wordCount }}</p>
                    <p><strong>Unique Words:</strong> {{ response.mainDeck.uniqueWordCount }}</p>
                  </div>
                  <div>
                    <p><strong>Character Count:</strong> {{ response.mainDeck.characterCount }}</p>
                    <p><strong>Unique Kanji:</strong> {{ response.mainDeck.uniqueKanjiCount }}</p>
                    <p><strong>Difficulty:</strong> {{ response.mainDeck.difficultyRaw.toFixed(2) }}</p>
                    <p><strong>Avg. Sentence Length:</strong> {{ response.mainDeck.averageSentenceLength.toFixed(2) }}</p>
                  </div>
                </div>
              </div>
            </div>

            <!-- Links Section -->
            <div class="mt-6">
              <div class="flex justify-between items-center mb-2">
                <h3 class="text-lg font-medium">Links</h3>
                <Button @click="openAddLinkDialog">
                  <Icon name="material-symbols-light:add-circle-outline" size="1.5em" />
                  Add Link
                </Button>
              </div>

              <div v-if="links.length === 0" class="p-4 border rounded text-center text-gray-500">No links available. Click "Add Link" to add one.</div>

              <div v-else class="mb-4">
                <ul class="list-none p-0">
                  <li v-for="(link, index) in links" :key="index" class="flex justify-between items-center p-2 border-b">
                    <div>
                      <span class="font-medium">{{ getLinkTypeText(parseInt(link.linkType)) }}:</span>
                      <a :href="link.url" target="_blank" class="ml-2 text-blue-500 hover:underline">{{ link.url }}</a>
                    </div>
                    <div class="flex">
                      <Button class="p-button-text p-button-info" @click="openEditLinkDialog(index)">
                        <Icon name="material-symbols-light:edit" size="1.5em" />
                      </Button>
                      <Button class="p-button-text p-button-danger" @click="removeLink(index)">
                        <Icon name="material-symbols-light:delete" size="1.5em" />
                      </Button>
                    </div>
                  </li>
                </ul>
              </div>

              <!-- Add Link Dialog -->
              <Dialog v-model:visible="showAddLinkDialog" header="Add Link" :modal="true" class="w-full md:w-1/2">
                <div class="p-fluid">
                  <div class="mb-4">
                    <label class="block text-sm font-medium mb-1">Link Type</label>
                    <Select
                      v-model="newLink.linkType"
                      :options="availableLinkTypes"
                      option-label="label"
                      option-value="value"
                      placeholder="Select Link Type"
                      class="w-full"
                    />
                  </div>
                  <div class="mb-4">
                    <label class="block text-sm font-medium mb-1">URL</label>
                    <InputText v-model="newLink.url" placeholder="Enter URL" class="w-full" />
                  </div>
                </div>
                <template #footer>
                  <Button label="Cancel" icon="pi pi-times" class="p-button-text" @click="showAddLinkDialog = false" />
                  <Button label="Add" icon="pi pi-check" class="p-button-text" @click="addLink" />
                </template>
              </Dialog>

              <!-- Edit Link Dialog -->
              <Dialog v-model:visible="showEditLinkDialog" header="Edit Link" :modal="true" class="w-full md:w-1/2">
                <div v-if="editingLink" class="p-fluid">
                  <div class="mb-4">
                    <label class="block text-sm font-medium mb-1">Link Type</label>
                    <Dropdown
                      v-model="editingLink.linkType"
                      :options="availableLinkTypes"
                      option-label="label"
                      option-value="value"
                      placeholder="Select Link Type"
                      class="w-full"
                    />
                  </div>
                  <div class="mb-4">
                    <label class="block text-sm font-medium mb-1">URL</label>
                    <InputText v-model="editingLink.url" placeholder="Enter URL" class="w-full" />
                  </div>
                </div>
                <template #footer>
                  <Button label="Cancel" icon="pi pi-times" class="p-button-text" @click="showEditLinkDialog = false" />
                  <Button label="Save" icon="pi pi-check" class="p-button-text" @click="saveEditedLink" />
                </template>
              </Dialog>
            </div>
          </template>
        </Card>

        <!-- Subdecks section -->
        <div class="mt-6">
          <div class="flex justify-between items-center mb-4">
            <h3 class="text-lg font-medium">Subdecks</h3>
            <Button @click="addSubdeck">
              <Icon name="material-symbols-light:add-circle-outline" size="1.5em" />
              Add Subdeck
            </Button>
          </div>

          <div v-if="response && response.subDecks && response.subDecks.length > 0" class="mb-4">
            <DataTable :value="response.subDecks" class="p-datatable-sm">
              <Column field="deckId" header="ID" :sortable="true" />
              <Column field="originalTitle" header="Title" :sortable="true" />
              <Column field="characterCount" header="Chars" :sortable="true" />
              <Column field="wordCount" header="Words" :sortable="true" />
              <Column field="uniqueWordCount" header="Unique Words" :sortable="true" />
              <Column field="difficultyRaw" header="Difficulty" :sortable="true" />
              <Column header="Actions">
                <template #body="slotProps">
                  <Button class="p-button-text p-button-sm" @click="navigateTo(`/dashboard/media/${slotProps.data.deckId}`)">
                    <Icon name="material-symbols-light:edit" size="1.5em" />
                  </Button>
                </template>
              </Column>
            </DataTable>
          </div>

          <Card v-for="subdeck in subdecks" :key="subdeck.id" class="mb-4">
            <template #title>
              <div class="flex justify-between items-center">
                <div class="flex flex-row gap-2">
                  <div>
                    <label class="block text-sm font-medium mb-1">Title</label>
                    <InputText v-model="subdeck.originalTitle" class="w-64" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium mb-1">Difficulty Override</label>
                    <InputNumber v-model="subdeck.difficultyOverride" class="w-12" />
                  </div>
                </div>
                <Button class="p-button-danger p-button-text" icon-class="text-2xl" @click="removeSubdeck(subdeck.id)">
                  <Icon name="material-symbols-light:delete" size="1.5em" />
                </Button>
              </div>
            </template>
            <template #content>
              <div v-if="!subdeck.file && !subdeck.mediaSubdeckId">
                <FileUpload
                  mode="advanced"
                  :auto="true"
                  choose-label="Select File"
                  :multiple="false"
                  class="w-full subdeck-file-upload"
                  :custom-upload="true"
                  :show-upload-button="false"
                  :show-cancel-button="false"
                  drag-drop-text="Select File or Drag and Drop Here"
                  @select="(e) => handleSubdeckFileUpload(e, subdeck.id)"
                />
              </div>
              <div v-else-if="subdeck.file" class="flex items-center">
                <span class="text-sm text-gray-600">{{ subdeck.file.name }}</span>
              </div>
              <div v-else-if="subdeck.mediaSubdeckId" class="flex items-center">
                <FileUpload
                  mode="advanced"
                  :auto="true"
                  choose-label="Replace current file"
                  :multiple="false"
                  class="w-full subdeck-file-upload ml-4"
                  :custom-upload="true"
                  :show-upload-button="false"
                  :show-cancel-button="false"
                  @select="(e) => handleSubdeckFileUpload(e, subdeck.id)"
                />
              </div>
            </template>
          </Card>

          <Card class="mb-4">
            <template #title>
              <div class="flex justify-between items-center">
                <span>Add New Subdeck</span>
              </div>
            </template>
            <template #content>
              <FileUpload
                ref="newSubdeckUploaderRef"
                mode="advanced"
                :auto="true"
                choose-label="Select File to Add Subdeck"
                :multiple="true"
                class="w-full subdeck-file-upload"
                :custom-upload="true"
                :show-upload-button="false"
                :show-cancel-button="false"
                drag-drop-text="Drag and drop here to add a new subdeck"
                @select="handleNewSubdeckFileUpload"
              >
                <template #empty>
                  <div class="flex items-center justify-center flex-col">
                    <Icon name="material-symbols-light:arrow-upload-progress" class="!border-2 !rounded-full !p-8 !text-4xl !text-muted-color" />
                    <p class="mt-6 mb-0">Drag and drop file to here to upload.</p>
                  </div>
                </template>
              </FileUpload>
            </template>
          </Card>
        </div>

        <!-- Submit Button -->
        <div class="mt-6 flex justify-center gap-2">
          <Button label="Update" class="p-button-lg p-button-success" @click="update(false)">
            <Icon name="material-symbols-light:refresh" size="1.5em" />
            Update
          </Button>

          <Button label="Update" class="p-button-lg p-button-success" @click="update(true)">
            <Icon name="material-symbols-light:refresh" size="1.5em" />
            Update & Reparse
          </Button>

          <Button label="Update" class="p-button-lg p-button-success" @click="fetchMetadata()">
            <Icon name="material-symbols-light:refresh" size="1.5em" />
            Fetch missing metadata
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
  .p-fileupload.p-fileupload-advanced .p-fileupload-buttonbar {
    display: none;
  }

  .p-fileupload.p-fileupload-advanced .p-fileupload-content {
    margin-top: 0;
    padding-top: 20px;
    padding-bottom: 20px;
    min-height: 100px;
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    border: 2px dashed #ccc;
    border-radius: 4px;
    transition: all 0.3s ease;
    text-align: center;
  }

  .p-fileupload.p-fileupload-advanced .p-fileupload-content:hover {
    border-color: #6366f1;
    background-color: rgba(99, 102, 241, 0.05);
  }

  .p-fileupload.p-fileupload-advanced .p-fileupload-content.p-fileupload-highlight {
    border-color: #6366f1;
    background-color: rgba(99, 102, 241, 0.1);
    box-shadow: 0 0 10px rgba(99, 102, 241, 0.3);
  }

  .p-fileupload.p-fileupload-advanced .p-fileupload-content .p-messages-icon,
  .p-fileupload.p-fileupload-advanced .p-fileupload-content .p-icon,
  .p-fileupload.p-fileupload-advanced .p-fileupload-content .pi-upload {
    font-size: 2rem;
    margin-bottom: 0.5rem;
  }

  .p-fileupload.p-fileupload-advanced .p-fileupload-content > div > span[data-pc-section='dndmessage'] {
    font-weight: bold;
  }
</style>
