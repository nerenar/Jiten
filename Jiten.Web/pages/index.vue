<script setup lang="ts">
  import WordSearch from '~/components/WordSearch.vue';
</script>

<template>
  <div>
    <WordSearch />

    <Card class="p-2 mt-4">
      <template #content>
        <h1><b>Welcome to Jiten.moe!</b></h1>
        Jiten is a tool to help you learn Japanese by providing vocabulary lists and anki decks on all types of media. <br/>
        If you have any issues, want to ask questions, make suggestions or just want to chat with the community, feel free to join our
        <a :href="getDiscordLink()" target="_blank">Discord server</a>. <br/>
        Jiten is also open source, you can find it on <a href="https://github.com/Sirush/Jiten" target="_blank">Github</a>.<br/><br/>

        <h2><b>Update June 25, 2025</b></h2>
        New big update with example sentences, deck coverage, and more!
      </template>
    </Card>
  </div>
</template>

<style scoped></style>
