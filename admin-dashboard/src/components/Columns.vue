<script setup>
// กราฟแท่งแนวตั้งสีเดียว: data = [{ label, value }] — แท่งสูงสุดเน้นสีเข้ม
import { computed } from 'vue'
const props = defineProps({
  data: { type: Array, default: () => [] },
  color: { type: String, default: 'var(--accent)' },
  height: { type: Number, default: 150 },
  every: { type: Number, default: 1 }, // แสดง label ทุกกี่แท่ง
  unit: { type: String, default: '' },
  showAll: Boolean, // แสดงตัวเลขบนทุกแท่ง (และ % ของทั้งหมด)
})
const total = computed(() => props.data.reduce((a, d) => a + d.value, 0))
const max = computed(() => Math.max(1, ...props.data.map(d => d.value)))
const top = computed(() => props.data.reduce((a, d, i) => (d.value > (props.data[a]?.value ?? -1) ? i : a), 0))
</script>

<template>
  <div class="cols" :style="{ height: height + 'px' }">
    <div v-for="(d, i) in data" :key="d.label" class="cols-c" :title="`${d.label}: ${d.value} ${unit}`">
      <span class="cols-v" v-if="showAll || (i === top && d.value)">{{ d.value }}<small v-if="showAll && total" class="cols-p">{{ Math.round(d.value / total * 100) }}%</small></span>
      <span class="cols-b" :style="{ height: (d.value / max) * (height - 34) + 'px', background: color, opacity: i === top ? 1 : .55 }"></span>
      <span class="cols-x">{{ i % every === 0 ? d.label : '' }}</span>
    </div>
  </div>
</template>
