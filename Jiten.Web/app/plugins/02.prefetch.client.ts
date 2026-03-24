export default defineNuxtPlugin(() => {
  const authStore = useAuthStore();
  if (authStore.isAuthenticated) {
    const srs = useSrsStore();
    srs.fetchStudyDecks();
    srs.fetchEnrollment();
  }
});
