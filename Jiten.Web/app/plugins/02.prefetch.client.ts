export default defineNuxtPlugin(() => {
  const authStore = useAuthStore();
  if (authStore.isAuthenticated) {
    const srs = useSrsStore();
    srs.fetchEnrollment();
    srs.fetchStudyDecks();
    srs.fetchDueSummary();
    srs.fetchDeckStreak();
  }
});
