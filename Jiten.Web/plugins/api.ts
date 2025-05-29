export default defineNuxtPlugin((nuxtApp) => {
  const config = useRuntimeConfig();

  const api = $fetch.create({
    baseURL: config.public.baseURL,
    onRequest({ options }) {
      const token = useCookie('token');
      if (token?.value) {
        options.headers = options.headers || {};
        options.headers.set("Authorization", `Bearer ${token.value}`);
      }
    },
    onResponse({ response }) {
      // Process response if needed
      // response._data = new myBusinessResponse(response._data)
    },
    async onResponseError({ response }) {
      if (response.status === 401) {
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
