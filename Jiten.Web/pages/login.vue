<script setup lang="ts">
  import { ref, reactive, onMounted } from 'vue';
  import { useAuthStore } from '~/stores/authStore';
  import type { LoginRequest } from '~/types';

  const authStore = useAuthStore();
  const router = useRouter();

  const credentials = reactive<LoginRequest>({
    usernameOrEmail: '',
    password: '',
  });
  const twoFactorCode = ref('');

  onMounted(() => {
    if (authStore.isAuthenticated) {
      router.push('/');
    }
  });

  async function handleLoginSubmit() {
    const success = await authStore.login(credentials);
    if (success && !authStore.requiresTwoFactor && authStore.isAuthenticated) {
      router.push('/');
    } else if (success && authStore.requiresTwoFactor) {
      console.log('2FA is required. Please enter the code.');
    }
  }

  async function handle2faSubmit() {
    const success = await authStore.loginWith2fa(twoFactorCode.value);
    if (success && authStore.isAuthenticated) {
      router.push('/');
    }
  }
</script>

<template>
  <Card v-if="authStore" class="login-container">
    <template #title>Login</template>
    <template #content>
      <form @submit.prevent="handleLoginSubmit">
        <div v-if="!authStore.requiresTwoFactor">
          <div>
            <FloatLabel for="usernameOrEmail">Username or Email:</FloatLabel>
            <InputText id="usernameOrEmail" v-model="credentials.usernameOrEmail" type="text" required />
          </div>
          <div>
            <FloatLabel for="password">Password:</FloatLabel>
            <InputText id="password" v-model="credentials.password" type="password" required />
          </div>
          <Button type="submit" :disabled="authStore.isLoading">
            {{ authStore.isLoading ? 'Logging in...' : 'Login' }}
          </Button>
        </div>

        <div v-if="authStore.requiresTwoFactor">
          <h3>Enter 2FA Code</h3>
          <div>
            <label for="twoFactorCode">Authenticator Code:</label>
            <input id="twoFactorCode" v-model="twoFactorCode" type="text" required />
          </div>
          <Button :disabled="authStore.isLoading" @click="handle2faSubmit">
            {{ authStore.isLoading ? 'Verifying...' : 'Verify Code' }}
          </Button>
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
