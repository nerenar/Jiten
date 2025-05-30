export default defineNuxtPlugin((nuxtApp) => {
  const config = useRuntimeConfig();

  const api = $fetch.create({
    baseURL: config.public.baseURL,
    onRequest({ options }) {
      const token = useCookie('token');
      if (token?.value) {
        options.headers = options.headers || {};
        options.headers.set('Authorization', `Bearer ${token.value}`);
      }
    },
    onResponse({ response }) {
      // Process response if needed
    },
    async onResponseError({ request, options, response }) {
      // Handle 401 errors with automatic token refresh
      if (response.status === 401) {
        const authStore = useAuthStore();

        // Don't try to refresh on auth endpoints to avoid infinite loops
        const url = request.toString();
        const isAuthEndpoint = url.includes('/auth/');

        if (!isAuthEndpoint && !authStore.isRefreshing) {
          console.log('Received 401, attempting token refresh...');

          // Try to refresh the token
          const refreshSuccess = await authStore.refreshAccessToken();

          if (refreshSuccess) {
            console.log('Token refreshed, retrying original request...');

            // Update the authorization header with the new token
            const newToken = useCookie('token');
            if (newToken?.value) {
              options.headers = options.headers || {};
              options.headers.set('Authorization', `Bearer ${newToken.value}`);
            }

            // Retry the original request with the new token
            try {
              return await $fetch(request, options);
            } catch (retryError) {
              console.error('Retry after token refresh failed:', retryError);
              // If retry fails, proceed with original 401 handling
            }
          }
        }

        // If we reach here, token refresh failed or this is an auth endpoint
        // Navigate to login page
        await nuxtApp.runWithContext(() => navigateTo('/login'));
      }
    },
  });

  return {
    provide: {
      api,
    },
  };
});
