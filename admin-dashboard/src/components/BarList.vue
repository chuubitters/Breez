<script setup>
// แท่งแนวนอน: [{ label, value, extra?, color? }]
import { computed } from 'vue'
const props = defineProps({ data: { type: Array, default: () => [] }, unit: { type: String, default: '' }, percent: Boolean })
const max = computed(() => Math.max(1, ...props.data.map(d => d.value)))
const total = computed(() => props.data.reduce((a, d) => a + d.value, 0))
</script>

<template>
  <div class="bars">
    <div v-for="d in data" :key="d.label" class="bar-row">
      <span class="label" :title="d.label"><i v-if="d.color" class="dot" :style="{ background: d.color }"></i>{{ d.label }}</span>
      <div class="track"><div class="fill" :style="{ width: (d.value / max) * 100 + '%', background: d.color || undefined }"></div></div>
      <span class="v">{{ percent ? (total ? Math.round(d.value / total * 100) : 0) + '%' : d.value.toLocaleString('th-TH') + unit }}</span>
      <span v-if="d.extra" class="extra">{{ d.extra }}</span>
    </div>
    <p v-if="!data.length || !total" class="muted small">ยังไม่มีข้อมูลในช่วงเวลานี้</p>
  </div>
</template>
