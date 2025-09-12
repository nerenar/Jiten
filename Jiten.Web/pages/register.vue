<script setup lang="ts">
  const { $api } = useNuxtApp();

  const form = reactive({
    username: '',
    email: '',
    password: '',
    tosAccepted: false,
    receiveNewsletter: false,
  });

  const recaptchaResponse = ref();
  useRecaptchaProvider();

  const isLoading = ref(false);
  const message = ref<string | null>(null);
  const error = ref<string | null>(null);

  async function handleRegister() {
    error.value = null;
    message.value = null;
    isLoading.value = true;
    try {
      if (!recaptchaResponse.value) {
        throw new Error('Please complete the reCAPTCHA.');
      }
      await $api('/auth/register', { method: 'POST', body: { ...form, recaptchaResponse: recaptchaResponse.value } });
      message.value =
        "Registration successful. Please check your email to confirm your account. If you don't receive the email within a few minutes, please contact us on Discord or send an email to contact@jiten.moe from the email address you used to register for a manual confirmation.";
    } catch (err: any) {
      if (err.response && err.response._data) {
        const apiMessage = err.response._data.message || 'Registration failed.';
        error.value = `Registration failed: ${apiMessage}`;
      } else {
        error.value = (err as Error)?.message ? `Registration failed: ${(err as Error).message}` : 'Registration failed: An unexpected error occurred.';
      }
    } finally {
      isLoading.value = false;
    }
  }
</script>

<template>
  <Card class="max-w-100 mx-auto p-2">
    <template #title>Register</template>
    <template #content>
      <form @submit.prevent="handleRegister" class="flex flex-col gap-2">
        <div class="w-full">
          <FloatLabel for="username">Username</FloatLabel>
          <InputText id="username" v-model.trim="form.username" required />
        </div>
        <div class="w-full">
          <FloatLabel for="email">Email</FloatLabel>
          <InputText id="email" v-model.trim="form.email" type="email" required />
          <div class="text-sm text-gray-500Can dark:text-gray-300">
            Please avoid hotmail/outlook email addresses or you might not receive confirmation emails.
          </div>
        </div>
        <div class="w-full">
          <FloatLabel for="password">Password</FloatLabel>
          <Password
            id="password"
            v-model="form.password"
            toggleMask
            :feedback="true"
            :promptLabel="'At least 10 characters including upper, lower, digit'"
            :weakLabel="'Weak'"
            :mediumLabel="'Medium'"
            :strongLabel="'Strong'"
            :inputProps="{ autocomplete: 'new-password', minlength: 10 }"
            required
          />
        </div>
        <div>
          <Checkbox id="terms" v-model="form.tosAccepted" name="terms" binary required />
          <label for="terms">
            I agree to the <NuxtLink to="/terms" target="_blank" class="text-blue-500 hover:underline">Terms of Service</NuxtLink> and
            <NuxtLink to="/privacy" target="_blank" class="text-blue-500 hover:underline">Privacy Policy</NuxtLink>.</label
          >
        </div>
        <div>
          <Checkbox id="newsletter" v-model="form.receiveNewsletter" name="newsletter" binary />
          <label for="newsletter">I would like to receive occasional updates and newsletters via email</label>
        </div>

        <RecaptchaCheckbox v-model="recaptchaResponse" />
        <Button type="submit" :disabled="isLoading">{{ isLoading ? 'Registering...' : 'Register' }}</Button>
      </form>
      <p v-if="message" class="text-amber-400">{{ message }}</p>
      <p v-if="error" class="text-red-500">{{ error }}</p>
      <div class="links">
        <NuxtLink to="/login">Back to Login</NuxtLink>
      </div>
    </template>
  </Card>
</template>
