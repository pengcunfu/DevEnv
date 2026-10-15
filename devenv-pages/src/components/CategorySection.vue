<script setup>
// 分类区块：分类标题（icon + 名称 + 数量）+ 软件卡片网格
// id 由 App.vue 通过 fallthrough attribute 传入，供分类导航锚点跳转
import SoftwareCard from './SoftwareCard.vue'
import SvgIcon from './SvgIcon.vue'

defineProps({
  category: { type: Object, required: true },
})
</script>

<template>
  <section class="category">
    <h2 class="category-title">
      <SvgIcon v-if="category.icon" :name="category.icon" :size="20" class="category-icon" />
      {{ category.name }}
      <span class="count">{{ category.softwares.length }}</span>
    </h2>
    <div class="card-grid">
      <SoftwareCard
        v-for="sw in category.softwares"
        :key="sw.name"
        :software="sw"
      />
    </div>
  </section>
</template>
