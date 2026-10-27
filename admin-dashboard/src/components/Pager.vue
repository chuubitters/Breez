<script setup>
import { computed } from 'vue'
const props = defineProps({ page: Number, pageSize: Number, total: Number, label: String })
const emit = defineEmits(['update:page'])
const pages = computed(() => Math.max(1, Math.ceil((props.total || 0) / props.pageSize)))
const from = computed(() => (props.total ? (props.page - 1) * props.pageSize + 1 : 0))
const to = computed(() => Math.min(props.page * props.pageSize, props.total || 0))
</script>

<template>
  <div class="table-footer">
    <span>แสดง {{ from }}–{{ to }} จาก {{ (total || 0).toLocaleString('th-TH') }} {{ label }}</span>
    <div class="pagination">
      <button :disabled="page <= 1" @click="emit('update:page', page - 1)">←</button>
      <button class="current-page">{{ page }}</button>
      <span>/ {{ pages }}</span>
      <button :disabled="page >= pages" @click="emit('update:page', page + 1)">→</button>
    </div>
  </div>
</template>
