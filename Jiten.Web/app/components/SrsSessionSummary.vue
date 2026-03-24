<script setup lang="ts">
  import type { HardestCard } from '~/stores/srsStore';
  import type { ReviewForecastDto, SessionStreakDto } from '~/types';

  const props = defineProps<{
    cardsReviewed: number;
    newCardsLearned: number;
    correctCount: number;
    startTime: Date | null;
    hardestCards: HardestCard[];
    gradeCounts: { again: number; hard: number; good: number; easy: number };
  }>();

  const emit = defineEmits<{
    close: [];
    studyMore: [];
  }>();

  const { $api } = useNuxtApp();

  const forecast = ref<ReviewForecastDto | null>(null);
  const streak = ref<SessionStreakDto | null>(null);

  onMounted(async () => {
    const [forecastResult, streakResult] = await Promise.allSettled([
      $api<ReviewForecastDto>('srs/review-forecast'),
      $api<SessionStreakDto>('srs/session-streak'),
    ]);
    if (forecastResult.status === 'fulfilled') forecast.value = forecastResult.value;
    if (streakResult.status === 'fulfilled') streak.value = streakResult.value;
  });

  const duration = computed(() => {
    if (!props.startTime) return '0m';
    const ms = Date.now() - props.startTime.getTime();
    const minutes = Math.floor(ms / 60000);
    const seconds = Math.floor((ms % 60000) / 1000);
    if (minutes === 0) return `${seconds}s`;
    return `${minutes}m ${seconds}s`;
  });

  const accuracy = computed(() => {
    if (props.cardsReviewed === 0) return 0;
    return Math.round((props.correctCount / props.cardsReviewed) * 100);
  });

  const isAllCaughtUp = computed(() => {
    if (!forecast.value) return false;
    return forecast.value.dueWithinHour === 0 && forecast.value.dueToday === 0;
  });

  const caughtUpMessage = computed(() => {
    if (!forecast.value?.nextReviewAt) return 'No more reviews scheduled';
    const next = new Date(forecast.value.nextReviewAt);
    const diffMs = next.getTime() - Date.now();
    if (diffMs <= 0) return 'Next review is ready now';
    const diffMin = Math.round(diffMs / 60000);
    if (diffMin < 60) return `Next review in ${diffMin}m`;
    const diffHours = Math.round(diffMin / 60);
    if (diffHours < 24) return `Next review in ${diffHours}h`;
    const diffDays = Math.round(diffHours / 24);
    return `Next review in ${diffDays}d`;
  });

  const forecastText = computed(() => {
    if (!forecast.value) return null;
    if (isAllCaughtUp.value) return null;
    const { dueWithinHour, dueToday, dueTomorrow } = forecast.value;
    const parts: string[] = [];
    if (dueWithinHour > 0) parts.push(`${dueWithinHour} due within the hour`);
    else if (dueToday > 0) parts.push(`${dueToday} due later today`);
    if (dueTomorrow > 0) parts.push(`${dueTomorrow} due tomorrow`);
    return parts.join(' · ');
  });

  const streakMessage = computed(() => {
    if (!streak.value || streak.value.currentStreak === 0) return null;
    const s = streak.value;
    if (s.isNewRecord && s.currentStreak > 1)
      return `New personal best! ${s.currentStreak} days and counting.`;
    if (s.currentStreak >= 365)
      return `${s.currentStreak} days. A full year of dedication — incredible.`;
    if (s.currentStreak >= 100)
      return `${s.currentStreak} days strong. Triple digits!`;
    if (s.currentStreak >= 30)
      return `${s.currentStreak} day streak. A whole month of consistency!`;
    if (s.currentStreak >= 14)
      return `${s.currentStreak} days in a row — you're building a real habit.`;
    if (s.currentStreak >= 7)
      return `${s.currentStreak} day streak! One full week down.`;
    if (s.currentStreak >= 3)
      return `${s.currentStreak} days in a row. Keep it going!`;
    return `${s.currentStreak} day streak — great start!`;
  });

  function formatDuration(ms: number): string {
    if (ms < 1000) return '<1s';
    const s = Math.round(ms / 1000);
    return s < 60 ? `${s}s` : `${Math.floor(s / 60)}m ${s % 60}s`;
  }
</script>

<template>
  <div class="flex flex-col items-center justify-center p-8 bg-white dark:bg-gray-800 rounded-2xl shadow-lg max-w-md mx-auto w-full">
    <div class="text-2xl font-bold mb-2">Session Complete</div>

    <!-- All caught up -->
    <div
      v-if="isAllCaughtUp"
      class="w-full mb-6 text-center px-4 py-3 rounded-xl bg-gradient-to-r from-green-50 to-emerald-50 dark:from-green-950/30 dark:to-emerald-950/30 ring-1 ring-green-200 dark:ring-green-800/50"
    >
      <div class="text-base font-semibold text-green-700 dark:text-green-300">All caught up!</div>
      <div class="text-sm text-green-600 dark:text-green-400 mt-0.5">{{ caughtUpMessage }}</div>
    </div>
    <div v-else class="mb-6" />

    <!-- Streak -->
    <div
      v-if="streakMessage"
      class="w-full mb-6 flex items-center gap-2.5 px-4 py-3 rounded-xl"
      :class="streak?.isNewRecord && streak.currentStreak > 1
        ? 'bg-gradient-to-r from-orange-50 to-amber-50 dark:from-orange-950/30 dark:to-amber-950/30 ring-1 ring-orange-200 dark:ring-orange-800/50'
        : 'bg-orange-50 dark:bg-orange-950/20'"
    >
      <Icon name="material-symbols:local-fire-department" size="1.75rem" class="text-orange-500 shrink-0" />
      <span class="text-sm font-medium text-gray-700 dark:text-gray-200">{{ streakMessage }}</span>
    </div>

    <div class="grid grid-cols-2 gap-6 w-full mb-6">
      <div class="text-center">
        <div class="text-3xl font-bold text-primary-600 dark:text-primary-400">{{ cardsReviewed }}</div>
        <div class="text-sm text-gray-500">Cards Reviewed</div>
      </div>
      <div class="text-center">
        <div class="text-3xl font-bold text-green-600 dark:text-green-400">{{ newCardsLearned }}</div>
        <div class="text-sm text-gray-500">New Cards</div>
      </div>
      <div class="text-center">
        <div class="text-3xl font-bold text-blue-600 dark:text-blue-400">{{ accuracy }}%</div>
        <div class="text-sm text-gray-500">Accuracy</div>
      </div>
      <div class="text-center">
        <div class="text-3xl font-bold text-purple-600 dark:text-purple-400">{{ duration }}</div>
        <div class="text-sm text-gray-500">Time Spent</div>
      </div>
    </div>

    <!-- Grade Distribution -->
    <div v-if="cardsReviewed > 0" class="w-full mb-6">
      <div class="flex h-3 rounded-full overflow-hidden">
        <div
          v-if="gradeCounts.again > 0"
          class="bg-red-500"
          :style="{ width: `${(gradeCounts.again / cardsReviewed) * 100}%` }"
          :title="`Again: ${gradeCounts.again}`"
        />
        <div
          v-if="gradeCounts.hard > 0"
          class="bg-orange-400"
          :style="{ width: `${(gradeCounts.hard / cardsReviewed) * 100}%` }"
          :title="`Hard: ${gradeCounts.hard}`"
        />
        <div
          v-if="gradeCounts.good > 0"
          class="bg-green-500"
          :style="{ width: `${(gradeCounts.good / cardsReviewed) * 100}%` }"
          :title="`Good: ${gradeCounts.good}`"
        />
        <div
          v-if="gradeCounts.easy > 0"
          class="bg-blue-500"
          :style="{ width: `${(gradeCounts.easy / cardsReviewed) * 100}%` }"
          :title="`Easy: ${gradeCounts.easy}`"
        />
      </div>
      <div class="flex justify-between mt-1.5 text-xs text-gray-500">
        <div v-if="gradeCounts.again > 0" class="flex items-center gap-1">
          <span class="w-2 h-2 rounded-full bg-red-500" />{{ gradeCounts.again }} Again
        </div>
        <div v-if="gradeCounts.hard > 0" class="flex items-center gap-1">
          <span class="w-2 h-2 rounded-full bg-orange-400" />{{ gradeCounts.hard }} Hard
        </div>
        <div v-if="gradeCounts.good > 0" class="flex items-center gap-1">
          <span class="w-2 h-2 rounded-full bg-green-500" />{{ gradeCounts.good }} Good
        </div>
        <div v-if="gradeCounts.easy > 0" class="flex items-center gap-1">
          <span class="w-2 h-2 rounded-full bg-blue-500" />{{ gradeCounts.easy }} Easy
        </div>
      </div>
    </div>

    <!-- Hardest Cards -->
    <div v-if="hardestCards.length > 0" class="w-full mb-6">
      <div class="text-sm font-semibold text-gray-600 dark:text-gray-300 mb-2">Keep an eye on</div>
      <div class="space-y-1.5">
        <NuxtLink
          v-for="card in hardestCards"
          :key="`${card.wordId}-${card.readingIndex}`"
          :to="`/vocabulary/${card.wordId}/${card.readingIndex}`"
          target="_blank"
          class="flex items-center justify-between px-3 py-2 rounded-lg bg-gray-50 dark:bg-gray-700/50 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
        >
          <div class="flex items-center gap-2 min-w-0">
            <span class="font-medium text-gray-800 dark:text-gray-200 truncate" lang="ja">{{ card.wordText }}</span>
            <span v-if="card.reading !== card.wordText" class="text-xs text-gray-400 truncate" lang="ja">{{ card.reading }}</span>
          </div>
          <div class="flex items-center gap-2 shrink-0 ml-2">
            <span class="text-xs px-1.5 py-0.5 rounded bg-red-100 dark:bg-red-900/40 text-red-600 dark:text-red-300">
              Again ×{{ card.againCount }}
            </span>
            <span v-if="card.avgDuration > 0" class="text-xs text-gray-400">
              {{ formatDuration(card.avgDuration) }}
            </span>
          </div>
        </NuxtLink>
      </div>
    </div>

    <!-- Review Forecast -->
    <div v-if="forecastText" class="w-full mb-6 text-center text-sm text-gray-500 dark:text-gray-400">
      {{ forecastText }}
    </div>

    <div class="flex gap-3 w-full">
      <Button label="Study More" severity="secondary" class="flex-1" @click="emit('studyMore')" />
      <Button label="Done" class="flex-1" @click="emit('close')" />
    </div>
  </div>
</template>
