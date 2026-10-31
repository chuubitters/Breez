<script setup>
// กราฟวงกลม (โดนัท) + คำอธิบายพร้อมจำนวนและ %
import { computed, ref } from 'vue'
const props = defineProps({
  parts: { type: Array, default: () => [] }, // [{ label, value, color }]
  center: String, centerSub: String, unit: { type: String, default: '' },
  how: { type: String, default: '' },
  focusMax: Boolean, // เน้นส่วนที่มากที่สุด: ตรงกลางแสดงชื่อช่วงนั้น + %
})
const R = 52, C = 2 * Math.PI * R
const hover = ref(-1)
const topIdx = computed(() => props.parts.reduce((a, p, i) => (p.value > (props.parts[a]?.value ?? -1) ? i : a), 0))
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
  <div class="donut" @mouseleave="hover = -1">
    <svg viewBox="0 0 140 140" role="img">
      <circle cx="70" cy="70" :r="R" fill="none" stroke="#eef0ec" stroke-width="18" />
      <circle v-for="(a, i) in arcs" :key="a.label" cx="70" cy="70" :r="R" fill="none" :stroke="a.color"
        :stroke-dasharray="a.dash" :stroke-dashoffset="a.off" transform="rotate(-90 70 70)"
        :stroke-width="hover === i || (focusMax && i === topIdx && hover < 0) ? 23 : 18" style="cursor:pointer" @mouseenter="hover = i" />
      <template v-if="focusMax && arcs[topIdx] && total">
        <text x="70" y="56" text-anchor="middle" class="donut-sub">มากที่สุด</text>
        <text x="70" y="76" text-anchor="middle" class="donut-num" :class="{ sm: String(arcs[topIdx].label).length > 6 }">{{ arcs[topIdx].label }}</text>
        <text x="70" y="94" text-anchor="middle" class="donut-sub">{{ arcs[topIdx].pct }}% · {{ arcs[topIdx].value }} {{ unit }}</text>
      </template>
      <template v-else>
        <text x="70" y="68" text-anchor="middle" class="donut-num">{{ center ?? total }}</text>
        <text x="70" y="86" text-anchor="middle" class="donut-sub">{{ centerSub }}</text>
      </template>
    </svg>
    <ul class="donut-legend">
      <li v-for="(a, i) in arcs" :key="a.label" :class="{ on: hover === i, top: focusMax && i === topIdx }" @mouseenter="hover = i">
        <i class="dot" :style="{ background: a.color }"></i><span class="lbl">{{ a.label }}</span>
        <span class="num muted">{{ a.value }}</span><b class="num">{{ a.pct }}%</b>
      </li>
    </ul>
    <div v-if="hover >= 0 && arcs[hover]" class="chart-tip static">
      <b>{{ arcs[hover].label }}</b>
      <span>{{ arcs[hover].value }} {{ unit }} · {{ arcs[hover].pct }}% ของทั้งหมด ({{ total }} {{ unit }})</span>
      <span v-if="how" class="how">วิธีคำนวณ: {{ how }}</span>
    </div>
  </div>
</template>
