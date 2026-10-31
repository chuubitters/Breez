<script setup>
// กราฟวงกลม (โดนัท) + คำอธิบายพร้อมจำนวนและ %
import { computed } from 'vue'
const props = defineProps({
  parts: { type: Array, default: () => [] }, // [{ label, value, color }]
  center: String, centerSub: String, unit: { type: String, default: '' },
})
const R = 52, C = 2 * Math.PI * R
const total = computed(() => props.parts.reduce((a, p) => a + p.value, 0))
const arcs = computed(() => {
  let off = 0
  return props.parts.map(p => {
    const len = total.value ? (p.value / total.value) * C : 0
    const a = { ...p, dash: `${Math.max(len - (len > 3 ? 1.5 : 0), 0)} ${C}`, off: -off, pct: total.value ? Math.round((p.value / total.value) * 100) : 0 }
    off += len
    return a
  })
})
</script>

<template>
  <div class="donut">
    <svg viewBox="0 0 140 140" role="img">
      <circle cx="70" cy="70" :r="R" fill="none" stroke="#eef0ec" stroke-width="18" />
      <circle v-for="a in arcs" :key="a.label" cx="70" cy="70" :r="R" fill="none" :stroke="a.color" stroke-width="18"
        :stroke-dasharray="a.dash" :stroke-dashoffset="a.off" transform="rotate(-90 70 70)"><title>{{ a.label }}: {{ a.value }} {{ unit }} ({{ a.pct }}%)</title></circle>
      <text x="70" y="68" text-anchor="middle" class="donut-num">{{ center ?? total }}</text>
      <text x="70" y="86" text-anchor="middle" class="donut-sub">{{ centerSub }}</text>
    </svg>
    <ul class="donut-legend">
      <li v-for="a in arcs" :key="a.label">
        <i class="dot" :style="{ background: a.color }"></i><span class="lbl">{{ a.label }}</span>
        <span class="num muted">{{ a.value }}</span><b class="num">{{ a.pct }}%</b>
      </li>
    </ul>
  </div>
</template>
