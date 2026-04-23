<script setup lang="ts">
  import { ref, reactive, onMounted } from 'vue';
  import { useAuthStore } from '~/stores/authStore';
  import type { LoginRequest } from '~/types/types';

  const runtimeConfig = useRuntimeConfig();
  const googleSignInEnabled = !!runtimeConfig.public.googleSignInClientId;

  const GoogleSignInButtonComponent = googleSignInEnabled ? resolveComponent('GoogleSignInButton') : null;

  const authStore = useAuthStore();
  const router = useRouter();
  const route = useRoute();

  const credentials = reactive<LoginRequest>({
    usernameOrEmail: '',
    password: '',
  });

  onMounted(() => {
    if (authStore.isAuthenticated) {
      router.push('/');
    }
  });

  function getSafeRedirect(): string | null {
    const redirect = Array.isArray(route.query.redirect) ? route.query.redirect[0] : route.query.redirect;
    return redirect && redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : null;
  }

  async function handleLoginSubmit() {
    const success = await authStore.login(credentials);
    if (success && authStore.isAuthenticated) {
      await router.push(getSafeRedirect() ?? '/');
    }
  }

  const handleGoogleOnSuccess = async (response: { credential?: string }) => {
    const { credential } = response;

    try {
      const result = await authStore.loginWithGoogle(credential);

      if (result === 'requiresRegistration') {
        await router.push({ path: '/google-registration' });
      } else if (result === true) {
        await router.push(getSafeRedirect() ?? '/');
      } else {
        console.error('Login failed:', authStore.error);
      }
    } catch (error) {
      console.error('Unexpected error during Google login:', error);
    }
  };

  const handleGoogleOnError = () => {
    console.error('Google login failed. Please try again.');
  };
</script>

<template>
  <Card v-if="authStore" class="login-container">
    <template #title>Login</template>
    <template #content>
      <form @submit.prevent="handleLoginSubmit">
        <div>
          <FloatLabel for="usernameOrEmail">Username or Email:</FloatLabel>
          <InputText id="usernameOrEmail" v-model="credentials.usernameOrEmail" type="text" required />
        </div>
        <div>
          <FloatLabel for="password">Password:</FloatLabel>
          <InputText id="password" v-model="credentials.password" type="password" required />
        </div>
        <div class="flex flex-col items-center">
          <div>
            <Button type="submit" :disabled="authStore.isLoading">
              {{ authStore.isLoading ? 'Logging in...' : 'Login' }}
            </Button>
          </div>
          <div v-if="GoogleSignInButtonComponent">
            <component :is="GoogleSignInButtonComponent" @success="handleGoogleOnSuccess" @error="handleGoogleOnError" />
          </div>
        </div>
        <div>
          <NuxtLink to="/register">Create an account</NuxtLink>
          <span> · </span>
          <NuxtLink to="/forgot-password">Forgot password?</NuxtLink>
        </div>
      </form>

      <p v-if="authStore.error" class="error-message">{{ authStore.error }}</p>
    </template>
  </Card>
</template>

<style scoped>
  .login-container {
    max-width: 400px;
    margin: 50px auto;
    padding: 20px;
    border: 1px solid #ccc;
    border-radius: 8px;
  }

  .login-container div {
    margin-bottom: 15px;
  }

  .login-container label {
    display: block;
    margin-bottom: 5px;
  }

  .login-container input {
    width: 100%;
    padding: 8px;
    box-sizing: border-box;
  }

  .error-message {
    color: red;
    margin-top: 10px;
  }
</style>
