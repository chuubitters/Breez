<script setup>
// ภาพรวมอารมณ์ของผู้ใช้ทั้งหมด + ข้อความพร้อมใช้สำหรับโพสต์ชวนคนมาฝึกหายใจ
import { computed, ref } from 'vue'
import { moodMessages } from '../lib/stats'
import ShareBar from './ShareBar.vue'

const props = defineProps({ m: Object, prev: Object, periodText: String, compact: Boolean })
const copied = ref(-1)

const parts = computed(() => [
  { label: 'รู้สึกดี', pct: props.m.goodPct, value: `${props.m.good} คน`, color: '#8fb35a' },
  { label: 'เฉย ๆ / เบื่อ', pct: props.m.neutralPct, value: `${props.m.neutral} คน`, color: '#c9cdc4' },
  { label: 'เครียด / โกรธ', pct: props.m.tensePct, value: `${props.m.tense} คน`, color: '#b5645c' },
])
const diff = computed(() => (props.prev?.people ? props.m.tensePct - props.prev.tensePct : null))
const messages = computed(() => moodMessages(props.m, props.periodText))

async function copy(text, i) {
  try { await navigator.clipboard.writeText(text); copied.value = i; setTimeout(() => (copied.value = -1), 1500) } catch { /* เบราว์เซอร์ไม่อนุญาต */ }
}
</script>

<template>
  <div v-if="!m.people" class="empty">ยังไม่มีการบันทึกอารมณ์ในช่วงเวลานี้</div>
  <div v-else class="pulse">
    <div class="pulse-head">
      <div>
        <div class="pulse-value">{{ m.tensePct }}<small>%</small></div>
        <div class="muted small">ของผู้ใช้ทั้งหมด {{ m.people }} คน รู้สึกเครียดหรือโกรธ</div>
      </div>
      <span v-if="diff !== null" class="pulse-diff" :class="diff > 0 ? 'down' : 'up'">
        {{ diff > 0 ? 'เพิ่มขึ้น' : diff < 0 ? 'ลดลง' : 'คงที่' }} {{ Math.abs(diff) }} จุดเปอร์เซ็นต์ เทียบกับช่วงก่อนหน้า
      </span>
    </div>
    <ShareBar :parts="parts" />
    <div v-if="!compact" class="pulse-msgs">
      <p class="small" style="font-weight:600">ข้อความตัวอย่างสำหรับประชาสัมพันธ์และส่งกำลังใจ</p>
      <div v-for="(t, i) in messages" :key="i" class="pulse-msg">
        <span>{{ t }}</span>
        <button class="btn btn-sm" @click="copy(t, i)">{{ copied === i ? 'คัดลอกแล้ว' : 'คัดลอก' }}</button>
      </div>
    </div>
  </div>
</template>
