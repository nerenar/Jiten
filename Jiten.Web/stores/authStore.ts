import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { useRouter } from 'vue-router';
import type { LoginRequest, TokenResponse } from '~/types/types';

export const useAuthStore = defineStore('auth', () => {
  const tokenCookie = useCookie('token', {
    watch: true,
    maxAge: 60 * 60 * 24 * 15,
    path: '/',
  });

  const accessToken = ref<string | null>(tokenCookie.value || null);
  const refreshToken = ref<string | null>(null);
  const user = ref<any | null>(null);
  const isLoading = ref<boolean>(false);
  const error = ref<string | null>(null);
  const requiresTwoFactor = ref<boolean>(false);
  const userIdFor2fa = ref<string | null>(null);

  const isAuthenticated = computed(() => !!accessToken.value);
  const isAdmin = computed(() => user.value?.roles?.includes('Administrator') || false);

  const { $api } = useNuxtApp();

  function setTokens(newAccessToken: string, newRefreshToken: string) {
    accessToken.value = newAccessToken;
    refreshToken.value = newRefreshToken;

    // Set the token cookie for the API plugin
    tokenCookie.value = newAccessToken;
  }

  function clearAuthData() {
    accessToken.value = null;
    refreshToken.value = null;
    user.value = null;
    requiresTwoFactor.value = false;
    userIdFor2fa.value = null;

    // Clear the token cookie
    tokenCookie.value = null;
  }

  async function login(credentials: LoginRequest) {
    isLoading.value = true;
    error.value = null;
    requiresTwoFactor.value = false;
    userIdFor2fa.value = null;

    try {
      const data = await $api<TokenResponse | { requiresTwoFactor: boolean; userId: string }>('/auth/login', {
        method: 'POST',
        body: credentials,
      });

      // if (data.value.requiresTwoFactor) {
      //   requiresTwoFactor.value = true;
      //   userIdFor2fa.value = data.value.userId;
      // } else
      if ('accessToken' in data) {
        setTokens(data.accessToken, data.refreshToken);
        //wait 500ms for timing
        await new Promise((resolve) => setTimeout(resolve, 500));
        await fetchCurrentUser();
      } else {
        throw new Error('Login failed: No token or 2FA requirement received.');
      }
      return true;
    } catch (err) {
      error.value = err.data?.message || err.message || 'Login failed.';
      clearAuthData();
      return false;
    } finally {
      isLoading.value = false;
    }
  }

  async function loginWith2fa(twoFactorCode: string) {
    if (!userIdFor2fa.value) {
      error.value = 'User ID for 2FA is missing.';
      return false;
    }

    isLoading.value = true;
    error.value = null;

    try {
      const data = await $api<TokenResponse>('/auth/login-2fa', {
        method: 'POST',
        body: {
          userId: userIdFor2fa.value,
          twoFactorCode: twoFactorCode,
          // rememberMachine: false // Or true, depending on your preference
        },
      });

      if (data.value?.accessToken) {
        setTokens(data.value.accessToken, data.value.refreshToken);
        requiresTwoFactor.value = false; // 2FA completed
        userIdFor2fa.value = null;
        await fetchCurrentUser();
        return true;
      } else {
        throw new Error('2FA Login failed: No token received.');
      }
    } catch (err) {
      error.value = err.data?.message || err.message || '2FA login failed.';
      // Don't clear tokens here as they weren't set yet from this flow
      return false;
    } finally {
      isLoading.value = false;
    }
  }

  async function fetchCurrentUser() {
    if (!accessToken.value) return;

    try {
      const data = await $api('/auth/me');
      user.value = data.value;
    } catch (err) {
      console.error('Failed to fetch current user:', err);
      // Potentially clear auth if token is invalid (e.g., on 401)
      if (err.status === 401) {
        logout(); // Or a more specific token invalidation action
      }
      user.value = null;
    }
  }

  async function logout() {
    isLoading.value = true;

    try {
      if (accessToken.value) {
        // Optional: Call the backend revoke endpoint
        await $api('/auth/revoke-token', {
          method: 'POST',
        });
      }
    } catch (err) {
      console.error('Error revoking token:', err.data?.message || err.message);
      // Still proceed with client-side logout
    } finally {
      clearAuthData();
      isLoading.value = false;
      // Navigate to the login page after logout
      const router = useRouter();
      router.push('/login');
    }
  }

  // Action to initialize store from persisted state (e.g., on app load)
  function initializeAuth() {
    // If the store is rehydrated with an access token, fetch the user
    if (accessToken.value) {
      // Set the token cookie for the API plugin
      tokenCookie.value = accessToken.value;
      fetchCurrentUser();
    }
  }

  return {
    // state
    accessToken,
    refreshToken,
    user,
    isLoading,
    error,
    requiresTwoFactor,
    userIdFor2fa,

    // getters
    isAuthenticated,
    isAdmin,

    // actions
    setTokens,
    clearAuthData,
    login,
    loginWith2fa,
    fetchCurrentUser,
    logout,
    initializeAuth,
  };
});
