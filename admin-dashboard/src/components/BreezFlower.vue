<script setup>
// ดอกไม้พาสเทลตาม CI ของ breez (กลีบไล่สีจากขาวตรงกลาง)
import { computed } from 'vue'

const props = defineProps({
  petals: { type: Number, default: 6 },
  color: { type: String, default: '#f2b6ca' },      // สีกลีบด้านนอก
  center: { type: String, default: '#f3f6cf' },     // เกสร
  size: { type: [Number, String], default: 120 },
  rotate: { type: Number, default: 0 },
})

const uid = `f${Math.random().toString(36).slice(2, 8)}`
const petalList = computed(() => Array.from({ length: props.petals }, (_, i) => {
  const a = (i / props.petals) * Math.PI * 2 - Math.PI / 2
  return { cx: 50 + Math.cos(a) * 25, cy: 50 + Math.sin(a) * 25 }
}))
</script>

<template>
  <svg :width="size" :height="size" viewBox="0 0 100 100" :style="{ transform: `rotate(${rotate}deg)` }" aria-hidden="true">
    <defs>
      <!-- ไล่สีจากกลางดอกออกไปทั้งดอก (ไม่ใช่ทีละกลีบ) -->
      <radialGradient :id="uid" gradientUnits="userSpaceOnUse" cx="50" cy="50" r="48">
        <stop offset="0" stop-color="#ffffff" />
        <stop offset="0.35" stop-color="#ffffff" stop-opacity="0.9" />
        <stop offset="0.75" :stop-color="color" stop-opacity="0.85" />
        <stop offset="1" :stop-color="color" />
      </radialGradient>
      <radialGradient :id="uid + 'c'" cx="50%" cy="50%" r="50%">
        <stop offset="0" stop-color="#ffffff" />
        <stop offset="1" :stop-color="center" />
      </radialGradient>
    </defs>
    <g :fill="`url(#${uid})`">
      <circle v-for="(p, i) in petalList" :key="i" :cx="p.cx" :cy="p.cy" r="20" />
    </g>
    <circle v-if="center" cx="50" cy="50" r="15" :fill="`url(#${uid}c)`" />
  </svg>
</template>
