<script setup>
// กราฟแท่งรายวัน ซ้อนตามโหมด: days = [{ day, total, [mode]: n }]
import { computed } from 'vue'
import { MODES, MODE_COLOR, dayOfMonth, weekdayShort } from '../lib/format'
const props = defineProps({ days: { type: Array, default: () => [] }, modes: { type: Array, default: () => MODES } })
const max = computed(() => {
  const m = Math.max(1, ...props.days.map(d => props.modes.reduce((a, k) => a + (d[k] || 0), 0)))
  return Math.max(4, Math.ceil(m / 4) * 4)
})
const ticks = computed(() => [4, 3, 2, 1, 0].map(i => (max.value / 4) * i))
const label = (d, i) => {
  const n = props.days.length
  if (n > 16 && i % Math.ceil(n / 12) !== 0) return ''
  return n > 10 ? dayOfMonth(d.day) : weekdayShort(d.day)
}
const tip = d => `${d.day}\n` + props.modes.map(m => `${m}: ${d[m] || 0}`).join('\n')
</script>

<template>
  <div>
    <div class="legend"><span v-for="m in modes" :key="m"><i class="dot" :style="{ background: MODE_COLOR[m] }"></i>{{ m }}</span></div>
    <div class="chart">
      <div class="y"><span v-for="t in ticks" :key="t">{{ t }}</span></div>
      <div class="plot">
        <div class="grid"><i v-for="t in ticks" :key="t"></i></div>
        <div v-for="(d, i) in days" :key="d.day" class="col" :title="tip(d)">
          <div class="stack">
            <span v-for="m in modes" :key="m" :style="{ height: ((d[m] || 0) / max) * 200 + 'px', background: MODE_COLOR[m] }"></span>
          </div>
          <span class="x">{{ label(d, i) }}</span>
        </div>
      </div>
    </div>
  </div>
</template>
