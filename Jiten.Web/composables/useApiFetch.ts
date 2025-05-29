import { PaginatedResponse } from '~/types/types';
import type { AsyncDataRequestStatus, UseFetchOptions } from '#app';

export function useApiFetch<T>(
  request: string | (() => string),
  opts?: any
): Promise<{
  data: Ref<T | null>;
  status: Ref<AsyncDataRequestStatus>;
  error: Ref<Error | null>;
}> {
  const { $api } = useNuxtApp();
  const tokenCookie = useCookie('token');

  // Set default headers
  const headers = new Headers(opts?.headers || {});

  // Add authorization header if token exists
  if (tokenCookie.value) {
    headers.set('Authorization', `Bearer ${tokenCookie.value}`);
  }

  // Merge options with headers
  const options = {
    ...opts,
    headers
  };

  const { data, status, error } = useFetch<T>(request, { 
    baseURL: useRuntimeConfig().public.baseURL,
    ...options
  });

  return { data, status, error };
}

export function useApiFetchPaginated<T>(
  request: string | (() => string),
  opts?: any
): Promise<{
  data: Ref<PaginatedResponse<T> | null>;
  status: Ref<AsyncDataRequestStatus>;
  error: Ref<Error | null>;
}> {
  const config = useRuntimeConfig();
  const tokenCookie = useCookie('token');

  // Set default headers
  const headers = new Headers(opts?.headers || {});

  // Add authorization header if token exists
  if (tokenCookie.value) {
    headers.set('Authorization', `Bearer ${tokenCookie.value}`);
  }

  // Merge options with headers
  const options = {
    ...opts,
    headers
  };

  // Use useFetch without await, keeping the data reactive
  const { data, status, error } = useFetch<PaginatedResponse<T>>(request, {
    baseURL: config.public.baseURL,
    ...options,
  });

  const paginatedData = computed(() => {
    if (data.value) {
      return new PaginatedResponse<T>(
        data.value.data,
        data.value.totalItems,
        data.value.pageSize,
        data.value.currentOffset
      );
    }
    return null;
  });

  return {
    data: paginatedData,
    status,
    error,
  };
}
