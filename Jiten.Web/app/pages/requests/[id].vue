<script setup lang="ts">
import { RequestStatus, MediaType } from '~/types';
import type { MediaRequestDto, MediaRequestCommentDto, MediaRequestUploadAdminDto } from '~/types/types';
import { getMediaTypeText } from '~/utils/mediaTypeMapper';
import { getRequestStatusText, getRequestStatusSeverity } from '~/utils/requestStatusMapper';
import { getLinkTypeText } from '~/utils/linkTypeMapper';
import { stripEpubImages } from '~/utils/epubStripper';
import { useAuthStore } from '~/stores/authStore';
import { useJitenStore } from '~/stores/jitenStore';

definePageMeta({
  middleware: ['auth'],
});

const route = useRoute();
const router = useRouter();
const toast = useToast();
const authStore = useAuthStore();
const jitenStore = useJitenStore();

const requestId = computed(() => Number(route.params.id));
const {
  fetchRequest, toggleUpvote, subscribe, unsubscribe, fetchComments, addComment,
  deleteRequest, updateStatus, editRequest, deleteUpload, reviewUpload, getUploadDownloadUrl,
  error: apiError,
} = useMediaRequests();

const request = ref<MediaRequestDto | null>(null);
const comments = ref<MediaRequestCommentDto[]>([]);
const isLoading = ref(true);
const commentText = ref('');
const isSubmittingComment = ref(false);
const showDeleteDialog = ref(false);
const isDeleting = ref(false);

// File upload
const selectedFiles = ref<File[]>([]);
const isStrippingEpub = ref(false);
const allowedExtensions = ['.srt', '.ass', '.ssa', '.epub', '.zip', '.rar', '.7z', '.txt'];
const dragOver = ref(false);
const fileInputRef = ref<HTMLInputElement | null>(null);

// Admin fields
const displayAdminFunctions = computed(() => jitenStore.displayAdminFunctions);
const showAdminPanel = ref(false);
const adminNote = ref('');
const fulfilledDeckId = ref<number | null>(null);
const isUpdatingStatus = ref(false);
const reviewingUploadId = ref<number | null>(null);

// Admin edit fields
const editTitle = ref('');
const editMediaType = ref<MediaType>(MediaType.Anime);
const editExternalUrl = ref('');
const editDescription = ref('');
const isSavingEdit = ref(false);
const mediaTypeOptions = Object.entries(MediaType)
  .filter(([key]) => isNaN(Number(key)))
  .map(([key, value]) => ({ label: key, value: value as MediaType }));

// Delete upload confirmation
const showDeleteUploadDialog = ref(false);
const pendingDeleteUploadId = ref<number | null>(null);

useHead({
  title: computed(() => request.value ? `${request.value.title} - Requests - Jiten` : 'Request - Jiten'),
});

async function loadData() {
  isLoading.value = true;
  const [req, comms] = await Promise.all([
    fetchRequest(requestId.value),
    fetchComments(requestId.value),
  ]);
  request.value = req;
  comments.value = comms;
  if (req?.adminNote) adminNote.value = req.adminNote;
  if (req) {
    editTitle.value = req.title;
    editMediaType.value = req.mediaType;
    editExternalUrl.value = req.externalUrl || '';
    editDescription.value = req.description || '';
  }
  isLoading.value = false;
}

async function handleUpvote() {
  if (!request.value) return;
  const result = await toggleUpvote(request.value.id);
  if (result) {
    request.value.hasUserUpvoted = result.upvoted;
    request.value.upvoteCount = result.upvoteCount;
    if (result.upvoted) request.value.isSubscribed = true;
  }
}

async function handleSubscribe() {
  if (!request.value) return;
  if (request.value.isSubscribed) {
    const success = await unsubscribe(request.value.id);
    if (success) request.value.isSubscribed = false;
  } else {
    const success = await subscribe(request.value.id);
    if (success) request.value.isSubscribed = true;
  }
}

async function processFiles(files: File[]) {
  for (const file of files) {
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
    if (!allowedExtensions.includes(ext)) {
      toast.add({ severity: 'warn', summary: `File type ${ext} is not allowed`, life: 5000 });
      continue;
    }

    if (ext === '.epub') {
      isStrippingEpub.value = true;
      const stripped = await stripEpubImages(file);
      isStrippingEpub.value = false;
      selectedFiles.value.push(stripped);
    } else {
      selectedFiles.value.push(file);
    }
  }
}

async function handleFileSelect(event: Event) {
  const input = event.target as HTMLInputElement;
  if (!input.files) return;
  await processFiles(Array.from(input.files));
  input.value = '';
}

async function onDrop(event: DragEvent) {
  dragOver.value = false;
  const files = event.dataTransfer?.files;
  if (files) await processFiles(Array.from(files));
}

function removeFile(index: number) {
  selectedFiles.value.splice(index, 1);
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

const totalFileSize = computed(() => selectedFiles.value.reduce((sum, f) => sum + f.size, 0));
const hasContent = computed(() => commentText.value.trim().length > 0 || selectedFiles.value.length > 0);

async function handleAddComment() {
  if (!hasContent.value || !request.value) return;
  isSubmittingComment.value = true;
  const text = commentText.value.trim() || undefined;
  const files = selectedFiles.value.length > 0 ? selectedFiles.value : undefined;
  const success = await addComment(request.value.id, text, files);
  isSubmittingComment.value = false;
  if (success) {
    commentText.value = '';
    selectedFiles.value = [];
    comments.value = await fetchComments(request.value.id);
    toast.add({ severity: 'success', summary: 'Comment posted', life: 3000 });
  } else {
    const detail = extractApiError(apiError.value, 'Failed to post comment. Please try again.');
    toast.add({ severity: 'error', summary: 'Failed to post comment', detail, life: 6000 });
  }
}

async function handleDelete() {
  if (!request.value) return;
  isDeleting.value = true;
  const success = await deleteRequest(request.value.id);
  isDeleting.value = false;
  showDeleteDialog.value = false;
  if (success) {
    toast.add({ severity: 'success', summary: 'Request deleted', life: 3000 });
    router.push('/requests');
  } else {
    const detail = extractApiError(apiError.value, 'Failed to delete request. Please try again.');
    toast.add({ severity: 'error', summary: 'Failed to delete request', detail, life: 5000 });
  }
}

async function handleStatusChange(newStatus: RequestStatus) {
  if (!request.value) return;

  if (newStatus === RequestStatus.Completed && !fulfilledDeckId.value) {
    toast.add({ severity: 'warn', summary: 'Deck ID is required when completing a request', life: 5000 });
    return;
  }
  if (newStatus === RequestStatus.Rejected && !adminNote.value.trim()) {
    toast.add({ severity: 'warn', summary: 'A reason is required when rejecting a request', life: 5000 });
    return;
  }

  isUpdatingStatus.value = true;
  const success = await updateStatus(request.value.id, {
    status: newStatus,
    adminNote: adminNote.value.trim() || undefined,
    fulfilledDeckId: newStatus === RequestStatus.Completed ? fulfilledDeckId.value ?? undefined : undefined,
  });
  isUpdatingStatus.value = false;

  if (success) {
    toast.add({ severity: 'success', summary: `Status updated to ${getRequestStatusText(newStatus)}`, life: 3000 });
    await loadData();
  } else {
    const detail = extractApiError(apiError.value, 'Failed to update status. Please try again.');
    toast.add({ severity: 'error', summary: 'Failed to update status', detail, life: 6000 });
  }
}

// Admin upload handlers
async function handleAdminDownload(uploadId: number) {
  if (!request.value) return;
  const url = await getUploadDownloadUrl(request.value.id, uploadId);
  if (url) {
    window.open(url, '_blank');
  } else {
    toast.add({ severity: 'error', summary: 'Failed to get download URL', life: 5000 });
  }
}

function confirmDeleteUpload(uploadId: number) {
  pendingDeleteUploadId.value = uploadId;
  showDeleteUploadDialog.value = true;
}

async function handleAdminDeleteUpload(uploadId: number) {
  if (!request.value) return;
  const success = await deleteUpload(request.value.id, uploadId);
  showDeleteUploadDialog.value = false;
  pendingDeleteUploadId.value = null;
  if (success) {
    toast.add({ severity: 'success', summary: 'Upload deleted', life: 3000 });
    comments.value = await fetchComments(request.value.id);
  } else {
    const detail = extractApiError(apiError.value, 'Failed to delete upload. Please try again.');
    toast.add({ severity: 'error', summary: 'Failed to delete upload', detail, life: 6000 });
  }
}

async function handleSaveEdit() {
  if (!request.value) return;
  if (!editTitle.value.trim()) {
    toast.add({ severity: 'warn', summary: 'Title is required', life: 5000 });
    return;
  }
  isSavingEdit.value = true;
  const success = await editRequest(request.value.id, {
    title: editTitle.value.trim(),
    mediaType: editMediaType.value,
    externalUrl: editExternalUrl.value.trim() || undefined,
    description: editDescription.value.trim() || undefined,
  });
  isSavingEdit.value = false;
  if (success) {
    toast.add({ severity: 'success', summary: 'Request updated', life: 3000 });
    await loadData();
  } else {
    const detail = extractApiError(apiError.value, 'Failed to update request. Please try again.');
    toast.add({ severity: 'error', summary: 'Failed to update request', detail, life: 6000 });
  }
}

async function handleAdminReviewUpload(uploadId: number, reviewed: boolean) {
  if (!request.value || reviewingUploadId.value !== null) return;
  reviewingUploadId.value = uploadId;
  const success = await reviewUpload(request.value.id, uploadId, reviewed);
  if (success) {
    toast.add({ severity: 'success', summary: reviewed ? 'Marked as reviewed' : 'Unmarked', life: 3000 });
    const comment = comments.value.find(c => c.upload?.id === uploadId);
    if (comment?.upload) {
      (comment.upload as any).adminReviewed = reviewed;
    }
  } else {
    const detail = extractApiError(apiError.value, 'Failed to update review status.');
    toast.add({ severity: 'error', summary: 'Failed to update review status', detail, life: 5000 });
  }
  reviewingUploadId.value = null;
}

const commentsWithUploads = computed(() =>
  comments.value.filter(c => c.upload)
);

const canComment = computed(() =>
  request.value && (request.value.status === RequestStatus.Open || request.value.status === RequestStatus.InProgress)
);

const isTerminal = computed(() =>
  request.value && (request.value.status === RequestStatus.Completed || request.value.status === RequestStatus.Rejected)
);

function formatTimeAgo(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 30) return `${diffDays}d ago`;
  return new Date(dateString).toLocaleDateString();
}

function formatCompletedAt(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const diffDays = Math.floor((now.getTime() - date.getTime()) / 86400000);
  if (diffDays < 30) return `${diffDays || 1}d ago`;
  return `on ${date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined })}`;
}

onMounted(() => loadData());
</script>

<template>
  <div class="container mx-auto p-2 md:p-4 max-w-3xl">
    <div class="flex items-center mb-4">
      <NuxtLink to="/requests">
        <Button icon="pi pi-arrow-left" severity="secondary" text />
      </NuxtLink>
      <span class="ml-2 text-muted-color">Back to requests</span>
    </div>

    <div v-if="isLoading" class="flex justify-center py-12">
      <ProgressSpinner style="width: 50px; height: 50px" />
    </div>

    <div v-else-if="!request" class="text-center py-12 text-muted-color">
      <i class="pi pi-exclamation-circle text-4xl mb-3" />
      <p>Request not found.</p>
    </div>

    <div v-else class="flex flex-col gap-4">
      <!-- Request details card -->
      <Card class="shadow-md">
        <template #content>
          <div class="mb-4">
            <h1 class="text-2xl font-bold mb-2">{{ request.title }}</h1>
            <div class="flex items-center gap-2 flex-wrap">
              <Tag :value="getMediaTypeText(request.mediaType)" severity="secondary" />
              <Tag
                :value="getRequestStatusText(request.status)"
                :severity="getRequestStatusSeverity(request.status)"
              />
              <span class="text-sm text-muted-color">{{ formatTimeAgo(request.createdAt) }}</span>
              <span v-if="request.completedAt" class="text-sm text-muted-color flex items-center gap-1">
                <i class="pi pi-check-circle" />
                completed {{ formatCompletedAt(request.completedAt) }}
              </span>
              <span v-if="request.requesterName && authStore.isAdmin" class="text-sm text-muted-color">
                by <span class="font-medium">{{ request.requesterName }}</span>
              </span>
            </div>
          </div>

          <div v-if="request.externalUrl" class="mb-4">
            <a :href="request.externalUrl" target="_blank" rel="noopener noreferrer" class="text-primary hover:underline flex items-center gap-1">
              <i class="pi pi-external-link" />
              <span v-if="request.externalLinkType">{{ getLinkTypeText(request.externalLinkType) }}</span>
              <span v-else>External link</span>
            </a>
          </div>

          <p v-if="request.description" class="mb-4 text-muted-color">{{ request.description }}</p>

          <div v-if="request.adminNote && isTerminal" class="mb-4 p-3 rounded-lg border border-surface-200 dark:border-surface-700 bg-surface-50 dark:bg-surface-800">
            <p class="text-sm font-semibold mb-1">Admin note:</p>
            <p class="text-sm">{{ request.adminNote }}</p>
          </div>

          <div v-if="request.fulfilledDeckId" class="mb-4">
            <NuxtLink :to="`/decks/media/${request.fulfilledDeckId}/detail`" class="text-primary hover:underline flex items-center gap-1">
              <i class="pi pi-check-circle" />
              View deck: {{ request.fulfilledDeckTitle || `#${request.fulfilledDeckId}` }}
            </NuxtLink>
          </div>

          <div class="flex items-center gap-3">
            <UpvoteButton
              :has-upvoted="request.hasUserUpvoted"
              :upvote-count="request.upvoteCount"
              @toggle="handleUpvote"
            />
            <RequestSubscribeButton
              :is-subscribed="request.isSubscribed"
              @toggle="handleSubscribe"
            />
            <Button
              v-if="request.isOwnRequest"
              icon="pi pi-trash"
              label="Delete"
              severity="danger"
              outlined
              @click="showDeleteDialog = true"
            />
          </div>

          <div v-if="isTerminal" class="mt-4">
            <Button
              icon="pi pi-copy"
              label="Make a Similar Request"
              severity="secondary"
              outlined
              @click="router.push(`/requests/new?mediaType=${request!.mediaType}`)"
            />
          </div>
        </template>
      </Card>

      <!-- Admin panel -->
      <Card v-if="authStore.isAdmin && displayAdminFunctions" class="shadow-md">
        <template #content>
          <Button
            :icon="showAdminPanel ? 'pi pi-chevron-up' : 'pi pi-chevron-down'"
            label="Admin Panel"
            severity="secondary"
            text
            class="-mt-2 -ml-2"
            @click="showAdminPanel = !showAdminPanel"
          />
          <div v-if="showAdminPanel" class="mt-3 flex flex-col gap-3">
            <!-- Edit Request Details -->
            <div class="border-b border-surface-200 dark:border-surface-700 pb-3">
              <h3 class="font-semibold text-sm mb-2">Edit Request</h3>
              <div class="flex flex-col gap-2">
                <div class="flex flex-col gap-1">
                  <label class="text-xs font-semibold">Title</label>
                  <InputText v-model="editTitle" class="w-full" />
                </div>
                <div class="flex flex-col gap-1">
                  <label class="text-xs font-semibold">Media Type</label>
                  <Select
                    v-model="editMediaType"
                    :options="mediaTypeOptions"
                    option-label="label"
                    option-value="value"
                    class="w-full"
                  />
                </div>
                <div class="flex flex-col gap-1">
                  <label class="text-xs font-semibold">External URL</label>
                  <InputText v-model="editExternalUrl" class="w-full" placeholder="https://..." />
                </div>
                <div class="flex flex-col gap-1">
                  <label class="text-xs font-semibold">Description</label>
                  <Textarea v-model="editDescription" rows="2" class="w-full" />
                </div>
                <Button
                  label="Save Changes"
                  icon="pi pi-save"
                  severity="info"
                  :loading="isSavingEdit"
                  class="w-fit"
                  @click="handleSaveEdit"
                />
              </div>
            </div>

            <div class="flex flex-col gap-2">
              <label class="font-semibold text-sm">Admin Note</label>
              <Textarea v-model="adminNote" rows="2" class="w-full" placeholder="Reason for rejection or other note" />
            </div>
            <div class="flex flex-col gap-2">
              <label class="font-semibold text-sm">Fulfilled Deck ID (for completion)</label>
              <InputNumber v-model="fulfilledDeckId" class="w-full" placeholder="Deck ID" />
            </div>
            <div v-if="!isTerminal" class="flex gap-2 flex-wrap">
              <Button
                v-if="request.status === RequestStatus.Open"
                label="Mark In Progress"
                severity="warn"
                :loading="isUpdatingStatus"
                @click="handleStatusChange(RequestStatus.InProgress)"
              />
              <Button
                label="Complete"
                severity="success"
                :loading="isUpdatingStatus"
                @click="handleStatusChange(RequestStatus.Completed)"
              />
              <Button
                label="Reject"
                severity="danger"
                :loading="isUpdatingStatus"
                @click="handleStatusChange(RequestStatus.Rejected)"
              />
            </div>
            <div v-else class="flex gap-2 flex-wrap">
              <Button
                label="Reopen"
                severity="info"
                :loading="isUpdatingStatus"
                @click="handleStatusChange(RequestStatus.Open)"
              />
            </div>

            <div v-if="commentsWithUploads.length > 0" class="border-t border-surface-200 dark:border-surface-700 pt-3 mt-2">
              <h3 class="font-semibold text-sm mb-2">Uploaded Files ({{ commentsWithUploads.length }})</h3>
              <div class="flex flex-col gap-2">
                <div
                  v-for="comment in commentsWithUploads"
                  :key="comment.upload!.id"
                  class="p-2 rounded border border-surface-200 dark:border-surface-700 text-sm"
                >
                  <div class="flex items-center gap-2 flex-wrap">
                    <i class="pi pi-paperclip text-xs" />
                    <span class="font-medium">{{ comment.upload!.fileName }}</span>
                    <span class="text-muted-color">({{ formatFileSize(comment.upload!.fileSize) }})</span>
                    <Tag
                      v-if="(comment.upload as any)?.adminReviewed"
                      value="Reviewed"
                      severity="success"
                      class="text-xs"
                    />
                    <Tag
                      v-if="(comment.upload as any)?.fileDeleted"
                      value="Deleted"
                      severity="danger"
                      class="text-xs"
                    />
                  </div>
                  <div v-if="(comment.upload as any)?.uploaderEmail" class="text-xs text-muted-color mt-1">
                    Uploader: {{ (comment.upload as MediaRequestUploadAdminDto).uploaderEmail }}
                  </div>
                  <div class="flex gap-1 mt-2">
                    <Button
                      v-if="!(comment.upload as any)?.fileDeleted"
                      icon="pi pi-download"
                      label="Download"
                      severity="secondary"
                      text
                      size="small"
                      @click="handleAdminDownload(comment.upload!.id)"
                    />
                    <Button
                      v-if="!(comment.upload as any)?.fileDeleted"
                      icon="pi pi-trash"
                      label="Delete File"
                      severity="danger"
                      text
                      size="small"
                      @click="confirmDeleteUpload(comment.upload!.id)"
                    />
                    <Button
                      v-if="!(comment.upload as any)?.adminReviewed"
                      icon="pi pi-check"
                      label="Mark Reviewed"
                      severity="success"
                      text
                      size="small"
                      :loading="reviewingUploadId === comment.upload!.id"
                      :disabled="reviewingUploadId !== null"
                      @click="handleAdminReviewUpload(comment.upload!.id, true)"
                    />
                    <Button
                      v-else
                      icon="pi pi-times"
                      label="Unmark Reviewed"
                      severity="secondary"
                      text
                      size="small"
                      :loading="reviewingUploadId === comment.upload!.id"
                      :disabled="reviewingUploadId !== null"
                      @click="handleAdminReviewUpload(comment.upload!.id, false)"
                    />
                  </div>
                </div>
              </div>
            </div>

            <RequestActivityTimeline :request-id="requestId" />
          </div>
        </template>
      </Card>

      <!-- Comments card -->
      <Card class="shadow-md">
        <template #content>
          <h2 class="text-lg font-semibold mb-4">
            Comments
            <span v-if="comments.length > 0" class="text-muted-color font-normal">({{ comments.length }})</span>
          </h2>

          <div v-if="comments.length === 0" class="text-center py-6 text-muted-color">
            No comments yet.
          </div>

          <div v-else class="flex flex-col gap-3 mb-4">
            <div
              v-for="comment in comments"
              :key="comment.id"
              class="p-3 rounded-lg border border-surface-200 dark:border-surface-700"
            >
              <div class="flex items-center gap-2 mb-1">
                <Tag
                  :value="comment.role"
                  :severity="comment.role === 'Requester' ? 'info' : 'secondary'"
                  class="text-xs"
                />
                <span v-if="comment.userName && authStore.isAdmin" class="text-xs font-medium">{{ comment.userName }}</span>
                <span v-if="comment.isOwnComment" class="text-xs text-muted-color italic">You</span>
                <span class="text-xs text-muted-color ml-auto">{{ formatTimeAgo(comment.createdAt) }}</span>
              </div>
              <p v-if="comment.text" class="text-sm whitespace-pre-wrap">{{ comment.text }}</p>
              <div v-if="comment.upload" class="mt-2 flex items-center gap-1 text-sm text-muted-color">
                <template v-if="(comment.upload as any)?.fileDeleted">
                  <i class="pi pi-ban text-xs" />
                  <span class="italic">[File deleted by admin]</span>
                </template>
                <template v-else>
                  <i class="pi pi-paperclip text-xs" />
                  <span>{{ comment.upload.fileName }}</span>
                  <span>({{ formatFileSize(comment.upload.fileSize) }})</span>
                  <span v-if="comment.upload.originalFileCount > 1" class="text-xs">
                    ({{ comment.upload.originalFileCount }} files)
                  </span>
                </template>
              </div>
            </div>
          </div>

          <div v-if="canComment" class="flex flex-col gap-2">
            <Message severity="secondary" :closable="false" class="text-sm">
              Your username is not visible to other users, but is visible to administrators to avoid abuse.
            </Message>
            <Textarea
              v-model="commentText"
              placeholder="Add a comment..."
              rows="3"
              :maxlength="500"
              class="w-full"
            />

            <div class="flex flex-col gap-2">
              <Message severity="warn" :closable="false" class="text-sm">
                Do not zip EPUBs - they are automatically optimised when uploaded directly.
              </Message>

              <input
                ref="fileInputRef"
                type="file"
                :accept="allowedExtensions.join(',')"
                multiple
                class="hidden"
                @change="handleFileSelect"
              />

              <!-- Mobile: button only -->
              <div class="sm:hidden flex items-center gap-2">
                <Button
                  icon="pi pi-paperclip"
                  label="Attach Files"
                  severity="secondary"
                  outlined
                  size="small"
                  @click="fileInputRef?.click()"
                />
                <span v-if="isStrippingEpub" class="text-xs text-muted-color flex items-center gap-1">
                  <ProgressSpinner style="width: 14px; height: 14px" />
                  Optimising epub...
                </span>
              </div>

              <!-- Desktop: drag & drop zone -->
              <div
                class="hidden sm:flex flex-col items-center justify-center border-2 border-dashed rounded-xl p-5 text-center transition-colors cursor-pointer"
                :class="dragOver
                  ? 'border-primary bg-primary-50 dark:bg-primary-900/20'
                  : 'border-surface-300 dark:border-surface-600'"
                @dragover.prevent="dragOver = true"
                @dragleave.prevent="dragOver = false"
                @drop.prevent="onDrop"
                @click="fileInputRef?.click()"
              >
                <i class="pi pi-upload text-2xl text-muted-color mb-2" />
                <p class="text-sm text-muted-color mb-2">Drag and drop files here, or</p>
                <Button
                  icon="pi pi-paperclip"
                  label="Attach Files"
                  severity="secondary"
                  outlined
                  size="small"
                  @click.stop="fileInputRef?.click()"
                />
                <span v-if="isStrippingEpub" class="text-xs text-muted-color flex items-center gap-1 mt-2">
                  <ProgressSpinner style="width: 14px; height: 14px" />
                  Optimising epub...
                </span>
              </div>

              <small class="text-muted-color">Max 100MB. Accepted: {{ allowedExtensions.join(' ') }}</small>

              <div v-if="selectedFiles.length > 0" class="flex flex-col gap-1">
                <div
                  v-for="(file, index) in selectedFiles"
                  :key="index"
                  class="flex items-center gap-2 text-sm p-1 rounded bg-surface-50 dark:bg-surface-800"
                >
                  <i class="pi pi-file text-xs" />
                  <span class="flex-1 truncate">{{ file.name }}</span>
                  <span class="text-muted-color text-xs">{{ formatFileSize(file.size) }}</span>
                  <Button
                    icon="pi pi-times"
                    severity="secondary"
                    text
                    size="small"
                    rounded
                    @click="removeFile(index)"
                  />
                </div>
                <small v-if="selectedFiles.length > 1" class="text-muted-color">
                  Total: {{ formatFileSize(totalFileSize) }}
                </small>
              </div>
            </div>

            <div class="flex items-center justify-between">
              <small class="text-muted-color">{{ commentText.length }}/500</small>
              <Button
                :label="selectedFiles.length > 0 ? 'Post Comment & Upload' : 'Post Comment'"
                icon="pi pi-send"
                :loading="isSubmittingComment"
                :disabled="!hasContent"
                @click="handleAddComment"
              />
            </div>
          </div>
          <p v-else-if="isTerminal" class="text-sm text-muted-color italic">
            Comments are closed for {{ getRequestStatusText(request.status).toLowerCase() }} requests.
          </p>
        </template>
      </Card>
    </div>

    <!-- Delete confirmation dialog -->
    <Dialog
      v-model:visible="showDeleteDialog"
      header="Delete Request"
      :modal="true"
      :style="{ width: '400px' }"
    >
      <p>Are you sure you want to delete this request? This action cannot be undone.</p>
      <template #footer>
        <Button label="Cancel" severity="secondary" @click="showDeleteDialog = false" />
        <Button label="Delete" severity="danger" :loading="isDeleting" @click="handleDelete" />
      </template>
    </Dialog>

    <!-- Delete upload confirmation dialog -->
    <Dialog
      v-model:visible="showDeleteUploadDialog"
      header="Delete File"
      :modal="true"
      :style="{ width: '400px' }"
    >
      <p>Are you sure you want to delete this uploaded file? This action cannot be undone.</p>
      <template #footer>
        <Button label="Cancel" severity="secondary" @click="showDeleteUploadDialog = false" />
        <Button label="Delete" severity="danger" @click="handleAdminDeleteUpload(pendingDeleteUploadId!)" />
      </template>
    </Dialog>
  </div>
</template>
