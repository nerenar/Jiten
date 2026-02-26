<script setup lang="ts">
import { MediaType, RequestStatus } from '~/types';
import type { MediaRequestDto } from '~/types/types';
import { getMediaTypeText } from '~/utils/mediaTypeMapper';
import { getRequestStatusText, getRequestStatusSeverity } from '~/utils/requestStatusMapper';
import { getLinkTypeText } from '~/utils/linkTypeMapper';

definePageMeta({
  middleware: ['auth'],
});

useHead({ title: 'Media Requests - Jiten' });

const { requests, totalCount, isLoading, fetchRequests, toggleUpvote, subscribe, unsubscribe, fetchMyQuota } = useMediaRequests();

const quota = ref<{ activeCount: number; limit: number } | null>(null);
const toast = useToast();
const router = useRouter();
const route = useRoute();

const limit = 20;

function parseTabFromQuery() { return route.query.tab === 'mine' ? 1 : 0; }
function parseTypeFromQuery() { return route.query.type !== undefined ? Number(route.query.type) as MediaType : undefined; }
function parseStatusFromQuery() {
  if (route.query.status === 'all') return undefined;
  if (route.query.status !== undefined) return Number(route.query.status) as RequestStatus;
  return RequestStatus.Open;
}
function parseSortFromQuery() { return typeof route.query.sort === 'string' ? route.query.sort : 'votes'; }
function parseOffsetFromQuery() { return route.query.page ? (Number(route.query.page) - 1) * limit : 0; }

const activeTab = ref(parseTabFromQuery());
const selectedMediaType = ref<MediaType | undefined>(parseTypeFromQuery());
const selectedStatus = ref<RequestStatus | undefined>(parseStatusFromQuery());
const sortBy = ref(parseSortFromQuery());
const offset = ref(parseOffsetFromQuery());

const mediaTypeOptions = [
  { label: 'All', value: undefined },
  ...Object.values(MediaType)
    .filter(v => typeof v === 'number')
    .map(v => ({ label: getMediaTypeText(v as MediaType), value: v as MediaType })),
];

const statusOptions = [
  { label: 'All', value: undefined },
  { label: 'Open', value: RequestStatus.Open },
  { label: 'In Progress', value: RequestStatus.InProgress },
  { label: 'Completed', value: RequestStatus.Completed },
  { label: 'Rejected', value: RequestStatus.Rejected },
];

const sortOptions = [
  { label: 'Most Voted', value: 'votes' },
  { label: 'Newest', value: 'recent' },
];

const isMine = computed(() => activeTab.value === 1);

const displaySections = computed(() => {
  if (!isMine.value) {
    return [{ title: '', items: requests.value, muted: false }];
  }
  return [
    { title: 'Active', items: requests.value.filter(r => r.status === RequestStatus.Open || r.status === RequestStatus.InProgress), muted: false },
    { title: 'Fulfilled', items: requests.value.filter(r => r.status === RequestStatus.Completed), muted: false },
    { title: 'Rejected', items: requests.value.filter(r => r.status === RequestStatus.Rejected), muted: true },
  ];
});

async function loadRequests() {
  if (isMine.value) {
    await fetchRequests({
      mediaType: selectedMediaType.value,
      sort: 'recent',
      offset: 0,
      limit: 200,
      mine: true,
    });
  } else {
    await fetchRequests({
      mediaType: selectedMediaType.value,
      status: selectedStatus.value,
      sort: sortBy.value,
      offset: offset.value,
      limit,
      mine: false,
    });
  }
}

watch([selectedMediaType, selectedStatus, sortBy, activeTab], () => {
  offset.value = 0;
  loadRequests();
  if (activeTab.value === 1) {
    fetchMyQuota().then(q => { quota.value = q; });
  }
});

watch(offset, () => loadRequests());

watch([activeTab, selectedMediaType, selectedStatus, sortBy, offset], () => {
  const query: Record<string, string> = {};
  if (activeTab.value === 1) query.tab = 'mine';
  if (selectedMediaType.value !== undefined) query.type = String(selectedMediaType.value);
  if (!isMine.value) {
    if (selectedStatus.value === undefined) query.status = 'all';
    else if (selectedStatus.value !== RequestStatus.Open) query.status = String(selectedStatus.value);
    if (sortBy.value !== 'votes') query.sort = sortBy.value;
    if (offset.value > 0) query.page = String(offset.value / limit + 1);
  }
  router.replace({ query });
});

async function handleUpvote(request: MediaRequestDto) {
  const result = await toggleUpvote(request.id);
  if (result) {
    request.hasUserUpvoted = result.upvoted;
    request.upvoteCount = result.upvoteCount;
    if (result.upvoted) {
      request.isSubscribed = true;
    }
  }
}

async function handleSubscribe(request: MediaRequestDto) {
  if (request.isSubscribed) {
    const success = await unsubscribe(request.id);
    if (success) request.isSubscribed = false;
  } else {
    const success = await subscribe(request.id);
    if (success) request.isSubscribed = true;
  }
}

function formatCompletedAt(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const diffDays = Math.floor((now.getTime() - date.getTime()) / 86400000);
  if (diffDays < 30) return `${diffDays || 1}d ago`;
  return `on ${date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined })}`;
}

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
  const diffMonths = Math.floor(diffDays / 30);
  return `${diffMonths}mo ago`;
}

function onPageChange(event: { first: number }) {
  offset.value = event.first;
}

onMounted(() => loadRequests());
</script>

<template>
  <div class="container mx-auto p-2 md:p-4">
    <div class="flex items-center justify-between mb-6">
      <h1 class="text-2xl font-bold">Media Requests</h1>
      <Button
        label="New Request"
        icon="pi pi-plus"
        @click="router.push('/requests/new')"
      />
    </div>

    <Tabs v-model:value="activeTab">
      <TabList>
        <Tab :value="0">All Requests</Tab>
        <Tab :value="1">My Requests</Tab>
      </TabList>
    </Tabs>

    <div class="flex flex-wrap gap-3 my-4 items-center">
      <Select
        v-model="selectedMediaType"
        :options="mediaTypeOptions"
        optionLabel="label"
        optionValue="value"
        placeholder="Media Type"
        class="w-40"
      />
      <template v-if="!isMine">
        <Select
          v-model="selectedStatus"
          :options="statusOptions"
          optionLabel="label"
          optionValue="value"
          placeholder="Status"
          class="w-40"
        />
        <Select
          v-model="sortBy"
          :options="sortOptions"
          optionLabel="label"
          optionValue="value"
          class="w-40"
        />
      </template>
    </div>

    <div v-if="isLoading" class="flex justify-center py-12">
      <ProgressSpinner style="width: 50px; height: 50px" />
    </div>

    <div v-else-if="requests.length === 0" class="text-center py-12 text-muted-color">
      <i class="pi pi-inbox text-4xl mb-3" />
      <template v-if="isMine">
        <p>You haven't made any requests yet.</p>
        <Button
          label="Make a request"
          icon="pi pi-arrow-right"
          iconPos="right"
          text
          class="mt-2"
          @click="router.push('/requests/new')"
        />
      </template>
      <p v-else>No requests found. Be the first!</p>
    </div>

    <template v-else>
      <div v-if="isMine && quota" class="mb-3">
        <Tag :value="`${quota.activeCount} / ${quota.limit} active slots used`" severity="secondary" />
      </div>

      <template v-for="section in displaySections" :key="section.title">
        <div v-if="section.items.length > 0" class="mb-6" :class="{ 'opacity-50': section.muted }">
          <h3 v-if="section.title" class="text-lg font-semibold mb-3 flex items-center gap-2">
            {{ section.title }}
            <span class="text-sm font-normal text-muted-color">({{ section.items.length }})</span>
          </h3>
          <div class="flex flex-col gap-3">
            <Card
              v-for="request in section.items"
              :key="request.id"
              class="shadow-sm cursor-pointer hover:bg-surface-50 dark:hover:bg-surface-800 transition-colors"
              @click="router.push(`/requests/${request.id}`)"
            >
              <template #content>
                <div class="flex items-start gap-4">
                  <div @click.stop>
                    <UpvoteButton
                      :has-upvoted="request.hasUserUpvoted"
                      :upvote-count="request.upvoteCount"
                      compact
                      @toggle="handleUpvote(request)"
                    />
                  </div>

                  <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2 flex-wrap mb-1">
                      <span class="font-semibold text-lg">{{ request.title }}</span>
                      <Tag :value="getMediaTypeText(request.mediaType)" severity="secondary" />
                      <Tag
                        :value="getRequestStatusText(request.status)"
                        :severity="getRequestStatusSeverity(request.status)"
                      />
                      <span v-if="request.isOwnRequest && !isMine" class="text-xs text-muted-color italic">
                        Your request
                      </span>
                    </div>

                    <div class="flex items-center gap-3 text-sm text-muted-color">
                      <span v-if="request.externalLinkType" class="flex items-center gap-1">
                        <i class="pi pi-external-link text-xs" />
                        {{ getLinkTypeText(request.externalLinkType) }}
                      </span>
                      <span v-if="request.commentCount > 0" class="flex items-center gap-1">
                        <i class="pi pi-comments text-xs" />
                        {{ request.commentCount }}
                      </span>
                      <span v-if="request.uploadCount > 0" class="flex items-center gap-1">
                        <i class="pi pi-paperclip text-xs" />
                        {{ request.uploadCount }}
                      </span>
                      <span>{{ formatTimeAgo(request.createdAt) }}</span>
                      <span v-if="request.completedAt" class="flex items-center gap-1">
                        <i class="pi pi-check-circle text-xs" />
                        completed {{ formatCompletedAt(request.completedAt) }}
                      </span>
                    </div>
                  </div>

                  <div class="shrink-0" @click.stop>
                    <RequestSubscribeButton
                      :is-subscribed="request.isSubscribed"
                      compact
                      @toggle="handleSubscribe(request)"
                    />
                  </div>
                </div>
              </template>
            </Card>
          </div>
        </div>
      </template>

      <Paginator
        v-if="!isMine && totalCount > limit"
        :rows="limit"
        :totalRecords="totalCount"
        :first="offset"
        class="mt-4"
        @page="onPageChange"
      />
    </template>
  </div>
</template>
