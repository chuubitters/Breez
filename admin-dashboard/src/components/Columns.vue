<script setup>
// กราฟแท่งแนวตั้งสีเดียว: data = [{ label, value }] — แท่งสูงสุดเน้นสีเข้ม
// hover แท่ง => กล่องข้อความบอกค่า, สัดส่วน และวิธีคำนวณ (how)
import { computed, ref } from 'vue'
const props = defineProps({
  data: { type: Array, default: () => [] },
  color: { type: String, default: 'var(--accent)' },
  height: { type: Number, default: 150 },
  every: { type: Number, default: 1 }, // แสดง label ทุกกี่แท่ง
  unit: { type: String, default: '' },
  showAll: Boolean, // แสดงตัวเลขบนทุกแท่ง (และ % ของทั้งหมด)
  how: { type: String, default: '' },
})
const total = computed(() => props.data.reduce((a, d) => a + d.value, 0))
const max = computed(() => Math.max(1, ...props.data.map(d => d.value)))
const top = computed(() => props.data.reduce((a, d, i) => (d.value > (props.data[a]?.value ?? -1) ? i : a), 0))
const hover = ref(-1)
const pct = d => (total.value ? Math.round((d.value / total.value) * 100) : 0)
const left = i => `${((i + 0.5) / Math.max(1, props.data.length)) * 100}%`
</script>

<template>
  <div class="cols" :style="{ height: height + 'px' }" @mouseleave="hover = -1">
    <div v-for="(d, i) in data" :key="d.label" class="cols-c" @mouseenter="hover = i">
      <span class="cols-v" v-if="showAll || (i === top && d.value)">{{ d.value }}<small v-if="showAll && total" class="cols-p">{{ pct(d) }}%</small></span>
      <span class="cols-b" :style="{ height: (d.value / max) * (height - 34) + 'px', background: color, opacity: i === top || i === hover ? 1 : .55 }"></span>
      <span class="cols-x">{{ i % every === 0 ? d.label : '' }}</span>
    </div>
    <div v-if="hover >= 0 && data[hover]" class="chart-tip" :style="{ left: left(hover) }">
      <b>{{ data[hover].label }}</b>
      <span>{{ data[hover].value }} {{ unit }}<template v-if="total"> · {{ pct(data[hover]) }}% ของทั้งหมด ({{ total }} {{ unit }})</template></span>
      <span v-if="how" class="how">วิธีคำนวณ: {{ how }}</span>
    </div>
  </div>
</template>
