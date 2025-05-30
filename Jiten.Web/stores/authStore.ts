import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import { useRouter } from 'vue-router';
import type { LoginRequest, TokenResponse } from '~/types/types';

export const useAuthStore = defineStore('auth', () => {
  const tokenCookie = useCookie('token', {
    watch: true,
    maxAge: 60 * 30,
    path: '/',
  });

  const refreshTokenCookie = useCookie('refreshToken', {
    watch: true,
    maxAge: 60 * 60 * 24 * 30,
    path: '/',
  });

  const accessToken = ref<string | null>(tokenCookie.value || null);
  const refreshToken = ref<string | null>(refreshTokenCookie.value || null);
  const user = ref<any | null>(null);
  const isLoading = ref<boolean>(false);
  const error = ref<string | null>(null);
  const requiresTwoFactor = ref<boolean>(false);
  const userIdFor2fa = ref<string | null>(null);
  const isRefreshing = ref<boolean>(false);

  const isAuthenticated = computed(() => !!accessToken.value);
  const isAdmin = computed(() => user.value?.roles?.includes('Administrator') || false);

  const { $api } = useNuxtApp();

  function setTokens(newAccessToken: string, newRefreshToken: string) {
    accessToken.value = newAccessToken;
    refreshToken.value = newRefreshToken;

    // Set the token cookie for the API plugin
    tokenCookie.value = newAccessToken;
    refreshTokenCookie.value = newRefreshToken;
  }

  function clearAuthData() {
    accessToken.value = null;
    refreshToken.value = null;
    user.value = null;
    requiresTwoFactor.value = false;
    userIdFor2fa.value = null;

    tokenCookie.value = null;
    refreshTokenCookie.value = null;
  }

  // Check if token is expired or about to expire (within 5 minutes)
  function isTokenExpired(token: string): boolean {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const currentTime = Math.floor(Date.now() / 1000);
      // Consider token expired if it expires within 5 minutes (300 seconds)
      return payload.exp < currentTime + 300;
    } catch (error) {
      console.error('Error parsing token:', error);
      return true; // Treat invalid tokens as expired
    }
  }

  async function refreshAccessToken(): Promise<boolean> {
    if (isRefreshing.value) {
      // Wait for ongoing refresh to complete
      while (isRefreshing.value) {
        await new Promise(resolve => setTimeout(resolve, 100));
      }
      return !!accessToken.value;
    }

    if (!refreshToken.value) {
      console.log('No refresh token available');
      clearAuthData();
      return false;
    }

    isRefreshing.value = true;

    try {
      const data = await $api<TokenResponse>('/auth/refresh', {
        method: 'POST',
        body: {
          accessToken: accessToken.value,
          refreshToken: refreshToken.value,
        },
      });

      if (data.accessToken && data.refreshToken) {
        setTokens(data.accessToken, data.refreshToken);
        console.log('Token refreshed successfully');
        return true;
      } else {
        throw new Error('Invalid refresh response');
      }
    } catch (err) {
      console.error('Token refresh failed:', err);
      clearAuthData();

      // Navigate to login page
      const router = useRouter();
      router.push('/login');

      return false;
    } finally {
      isRefreshing.value = false;
    }
  }

  // Ensure we have a valid token before making API calls
  async function ensureValidToken(): Promise<boolean> {
    if (!accessToken.value) {
      return false;
    }

    if (isTokenExpired(accessToken.value)) {
      console.log('Token expired, attempting to refresh...');
      return await refreshAccessToken();
    }

    return true;
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
      if ('accessToken' in data && 'refreshToken' in data) {
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
    if (!(await ensureValidToken())) {
      return;
    }

    try {
      const data = await $api('/auth/me');
      user.value = data;
    } catch (err: any) {
      console.error('Failed to fetch current user:', err);
      if (err.status === 401) {
        // Try to refresh token once more
        if (await refreshAccessToken()) {
          // Retry fetching user after refresh
          try {
            const data = await $api('/auth/me');
            user.value = data;
            return;
          } catch (retryErr) {
            console.error('Failed to fetch user after token refresh:', retryErr);
          }
        }
        await logout();
      }
      user.value = null;
    }
  }

  async function logout() {
    isLoading.value = true;

    try {
      if (accessToken.value) {
        await $api('/auth/revoke-token', {
          method: 'POST',
        });
      }
    } catch (err) {
      console.error('Error revoking token:', err.data?.message || err.message);
    } finally {
      clearAuthData();
      isLoading.value = false;
      const router = useRouter();
      router.push('/login');
    }
  }

  function initializeAuth() {
    if (tokenCookie.value && refreshTokenCookie.value) {
      accessToken.value = tokenCookie.value;
      refreshToken.value = refreshTokenCookie.value;

      if (isTokenExpired(accessToken.value)) {
        refreshAccessToken().then((success) => {
          if (success) {
            fetchCurrentUser();
          }
        });
      } else {
        fetchCurrentUser();
      }
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
    isRefreshing,

    // actions
    setTokens,
    clearAuthData,
    login,
    loginWith2fa,
    fetchCurrentUser,
    logout,
    initializeAuth,
    refreshAccessToken,
    ensureValidToken,
  };
});
