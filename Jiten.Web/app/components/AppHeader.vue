<script setup lang="ts">
  import Button from 'primevue/button';

  import { storeToRefs } from 'pinia';
  import { useJitenStore } from '~/stores/jitenStore';
  import { useAuthStore } from '~/stores/authStore';
  import { useSrsStore } from '~/stores/srsStore';

  import { useToast } from 'primevue/usetoast';
  import { ThemeMode } from '~/types';

  const toast = useToast();
  const store = useJitenStore();
  const {
    displayAdminFunctions,
    themeMode,
  } = storeToRefs(store);
  const auth = useAuthStore();
  const srs = useSrsStore();

  // Mobile menu state
  const mobileMenuOpen = ref(false);
  const toggleMobileMenu = () => (mobileMenuOpen.value = !mobileMenuOpen.value);

  // Close mobile menu on route change
  const route = useRoute();
  watch(
    () => route.fullPath,
    () => {
      mobileMenuOpen.value = false;
    }
  );

  function applyTheme(mode: ThemeMode) {
    const shouldBeDark = mode === ThemeMode.Dark
      || (mode === ThemeMode.Auto && window.matchMedia('(prefers-color-scheme: dark)').matches);
    document.documentElement.classList.toggle('dark-mode', shouldBeDark);
  }

  const themeLabels: Record<ThemeMode, string> = {
    [ThemeMode.Light]: 'light',
    [ThemeMode.Dark]: 'dark',
    [ThemeMode.Auto]: 'system',
  };

  function cycleTheme() {
    const systemIsDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const opposite = systemIsDark ? ThemeMode.Light : ThemeMode.Dark;
    const same = systemIsDark ? ThemeMode.Dark : ThemeMode.Light;
    const order = [ThemeMode.Auto, opposite, same];
    const next = order[(order.indexOf(themeMode.value) + 1) % order.length];
    themeMode.value = next;
    applyTheme(next);
    toast.add({ severity: 'info', summary: `Switched to ${themeLabels[next].toLowerCase()} theme`, life: 1500, group: 'bottom' });
  }

  const themeIcon = computed(() => {
    if (themeMode.value === ThemeMode.Light) return 'line-md:sun-rising-loop';
    if (themeMode.value === ThemeMode.Dark) return 'line-md:moon-rising-loop';
    return 'line-md:light-dark';
  });

  const themeLabel = computed(() => {
    if (themeMode.value === ThemeMode.Light) return 'Light';
    if (themeMode.value === ThemeMode.Dark) return 'Dark';
    return 'Auto';
  });

  onMounted(() => {
    applyTheme(store.themeMode);
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
      if (store.themeMode === ThemeMode.Auto) {
        applyTheme(ThemeMode.Auto);
      }
    });
  });

  const { startPolling, stopPolling } = useNotifications();
  watch(() => auth.isAuthenticated, (isAuth) => {
    if (isAuth) startPolling();
    else stopPolling();
  }, { immediate: true });
  onUnmounted(() => stopPolling());

  const settings = ref();

  const toggleSettings = (event: boolean) => {
    settings.value.toggle(event);
  };

  const showSettings = (event: boolean) => {
    settings.value.show(event);
  };
</script>

<template>
  <header>
    <div class="bg-indigo-900">
      <div class="flex justify-between items-center mb-6 mx-auto p-4 max-w-6xl">
        <NuxtLink to="/" class="!no-underline">
          <h1 class="text-2xl font-bold text-white">Jiten <span class="text-red-600 text-xs align-super">beta</span></h1>
        </NuxtLink>

        <!-- Desktop nav -->
        <nav class="hidden md:flex items-center space-x-4">
          <nuxt-link to="/" :class="route.path === '/' ? 'font-semibold !text-purple-200' : '!text-white'">Home</nuxt-link>
          <nuxt-link to="/decks/media" :class="route.path.startsWith('/decks/media') ? 'font-semibold !text-purple-200' : '!text-white'">Media</nuxt-link>
          <nuxt-link v-if="auth.isAuthenticated && srs.srsEnrolled" to="/srs/decks" :class="route.path.startsWith('/srs') ? 'font-semibold !text-purple-200' : '!text-white'">Study</nuxt-link>
          <nuxt-link v-if="auth.isAuthenticated" to="/profile" :class="route.path.startsWith('/profile') ? 'font-semibold !text-purple-200' : '!text-white'">Profile</nuxt-link>
          <nuxt-link v-if="auth.isAuthenticated" to="/ratings" :class="route.path === '/ratings' ? 'font-semibold !text-purple-200' : '!text-white'">Ratings</nuxt-link>
          <nuxt-link v-if="auth.isAuthenticated" to="/settings" :class="route.path === '/settings' ? 'font-semibold !text-purple-200' : '!text-white'">Settings</nuxt-link>
          <nuxt-link to="/other" :class="route.path === '/other' ? 'font-semibold !text-purple-200' : '!text-white'">Tools</nuxt-link>
          <nuxt-link to="/faq" :class="route.path === '/faq' ? 'font-semibold !text-purple-200' : '!text-white'">FAQ</nuxt-link>
          <nuxt-link v-if="auth.isAuthenticated && auth.isAdmin && store.displayAdminFunctions" to="/Dashboard" :class="route.path === '/Dashboard' ? 'font-semibold !text-purple-200' : '!text-white'">Dashboard</nuxt-link>
          <a v-if="auth.isAuthenticated" href="#" class="!text-white cursor-pointer" @click.prevent="auth.logout()">Logout</a>
          <nuxt-link v-else to="/login" :class="route.path === '/login' ? 'font-semibold !text-purple-200' : '!text-white'">Login</nuxt-link>
          <NotificationBell v-if="auth.isAuthenticated" />
          <Button
            type="button"
            label="Settings"
            severity="secondary"
            @mouseover="showSettings($event)"
            @mouseleave="onSettingsMouseLeave"
            @click="toggleSettings($event)"
          >
            <Icon name="material-symbols-light:settings" />
          </Button>

          <Button :label="themeLabel" severity="secondary" @click="cycleTheme()">
            <Icon :name="themeIcon" />
          </Button>
        </nav>

        <!-- Mobile: bell + hamburger -->
        <div class="md:hidden flex items-center gap-1">
          <NotificationBell v-if="auth.isAuthenticated" />
          <button
            class="inline-flex items-center justify-center p-2 rounded text-white hover:bg-indigo-800 focus:outline-none focus:ring-2 focus:ring-white"
            @click="toggleMobileMenu"
            aria-label="Toggle navigation menu"
            :aria-expanded="mobileMenuOpen.toString()"
          >
            <Icon :name="mobileMenuOpen ? 'material-symbols:close' : 'material-symbols:menu'" size="28" />
          </button>
        </div>
      </div>

      <!-- Mobile menu panel -->
      <div v-if="mobileMenuOpen" class="md:hidden mx-auto max-w-6xl px-4 pb-4">
        <div class="bg-indigo-800 rounded-lg shadow-lg divide-y divide-indigo-700">
          <div class="flex flex-col py-2">
            <nuxt-link to="/" class="py-2 px-3" :class="route.path === '/' ? 'font-semibold !text-purple-200' : '!text-white'" @click="mobileMenuOpen = false">Home</nuxt-link>
            <nuxt-link to="/decks/media" class="py-2 px-3" :class="route.path.startsWith('/decks/media') ? 'font-semibold !text-purple-200' : '!text-white'" @click="mobileMenuOpen = false">Media</nuxt-link>
            <nuxt-link v-if="auth.isAuthenticated && srs.srsEnrolled" to="/srs/decks" class="py-2 px-3" :class="route.path.startsWith('/srs') ? 'font-semibold !text-purple-200' : '!text-white'">Study</nuxt-link>
            <nuxt-link v-if="auth.isAuthenticated" to="/profile" class="py-2 px-3" :class="route.path.startsWith('/profile') ? 'font-semibold !text-purple-200' : '!text-white'" @click="mobileMenuOpen = false">Profile</nuxt-link>
            <nuxt-link v-if="auth.isAuthenticated" to="/ratings" class="py-2 px-3" :class="route.path === '/ratings' ? 'font-semibold !text-purple-200' : '!text-white'" @click="mobileMenuOpen = false">Ratings</nuxt-link>
            <nuxt-link v-if="auth.isAuthenticated" to="/settings" class="py-2 px-3" :class="route.path === '/settings' ? 'font-semibold !text-purple-200' : '!text-white'" @click="mobileMenuOpen = false">Settings</nuxt-link>
            <nuxt-link to="/other" class="py-2 px-3" :class="route.path === '/other' ? 'font-semibold !text-purple-200' : '!text-white'" @click="mobileMenuOpen = false">Tools</nuxt-link>
            <nuxt-link to="/faq" class="py-2 px-3" :class="route.path === '/faq' ? 'font-semibold !text-purple-200' : '!text-white'" @click="mobileMenuOpen = false">FAQ</nuxt-link>
            <nuxt-link
              v-if="auth.isAuthenticated && auth.isAdmin && store.displayAdminFunctions"
              to="/Dashboard"
              class="py-2 px-3"
              :class="route.path === '/Dashboard' ? 'font-semibold !text-purple-200' : '!text-white'"
              @click="mobileMenuOpen = false"
              >Dashboard</nuxt-link
            >
            <a
              v-if="auth.isAuthenticated"
              href="#"
              class="py-2 px-3 !text-white cursor-pointer"
              @click.prevent="auth.logout(); mobileMenuOpen = false"
              >Logout</a
            >
            <nuxt-link v-else to="/login" class="py-2 px-3" :class="route.path === '/login' ? 'font-semibold !text-purple-200' : '!text-white'" @click="mobileMenuOpen = false">Login</nuxt-link>
          </div>
          <div class="flex items-center gap-3 py-3 px-3">
            <Button
              type="button"
              label="Settings"
              severity="secondary"
              class="w-full justify-center"
              @click="toggleSettings($event)"
            >
              <Icon name="material-symbols-light:settings" />
            </Button>
            <Button :label="themeLabel" severity="secondary" class="w-full justify-center" @click="cycleTheme()">
              <Icon :name="themeIcon" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  </header>

  <LazyAppHeaderSettings ref="settings" />
</template>

<style scoped></style>
