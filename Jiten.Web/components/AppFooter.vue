<script setup lang="ts"></script>

<template>
  <footer class="bg-indigo-900 text-white p-4 bottom-0 w-full">
    <div class="flex flex-col md:flex-row justify-evenly items-center gap-4 max-w-6xl mx-auto">
      <div class="flex flex-col sm:flex-row items-center gap-4">
        <p class="text-sm">© {{ new Date().getFullYear() }} Jiten</p>
        <NuxtLink :to="`/terms`" target="_blank" class="!text-white text-sm">Terms of Service</NuxtLink>
        <NuxtLink :to="`/privacy`" target="_blank" class="!text-white text-sm">Privacy Policy</NuxtLink>
      </div>
      <div class="flex items-center space-x-4">
        <a href="https://github.com/Sirush/Jiten" target="_blank">
          <Icon name="mdi:github" size="2em" class="text-white" />
        </a>
        <a :href="getDiscordLink()" target="_blank">
          <Icon name="ic:baseline-discord" size="2em" class="text-white" />
        </a>
      </div>
      <div class="flex space-x-4">
        <NuxtLink to="/media-updates" class="!text-white" no-rel>New media updates</NuxtLink>
      </div>
    </div>
  </footer>
</template>

<style scoped></style>
