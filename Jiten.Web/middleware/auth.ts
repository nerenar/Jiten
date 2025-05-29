export default defineNuxtRouteMiddleware((to, from) => {
  const authStore = useAuthStore();

  if (!authStore.isAuthenticated) {
    return navigateTo({
      path: '/login',
      query: { redirect: to.fullPath !== '/login' ? to.fullPath : undefined }
    });
  }
});
