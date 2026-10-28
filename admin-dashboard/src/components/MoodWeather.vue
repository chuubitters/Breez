<script setup>
// "พยากรณ์อารมณ์" ภาพรวมอารมณ์/ความเครียดของชาว breez
// ใช้ทำคอนเทนต์การตลาด ชวนคนมาฝึกหายใจและส่งกำลังใจให้กัน
import { computed, ref } from 'vue'
import { number, pct } from '../lib/format'
import BreezFlower from './BreezFlower.vue'

const props = defineProps({ data: Object, periodLabel: String })

const idx = computed(() => props.data?.mood_index ?? {})
const total = computed(() => idx.value.total ?? 0)
const good = computed(() => pct(idx.value.good, total.value))
const neutral = computed(() => pct(idx.value.neutral, total.value))
const tense = computed(() => pct(idx.value.tense, total.value))

const prevTense = computed(() => idx.value.prev_total ? pct(idx.value.prev_tense, idx.value.prev_total) : null)
const tenseDiff = computed(() => prevTense.value == null ? null : tense.value - prevTense.value)

const weather = computed(() => {
  if (!total.value) return { icon: '☁', title: 'ยังไม่มีข้อมูลอารมณ์', tone: 'empty' }
  if (tense.value >= 40) return { icon: '🌧', title: 'ฟ้าครึ้ม ความเครียดสูง', tone: 'tense' }
  if (good.value >= 55) return { icon: '☀', title: 'ท้องฟ้าแจ่มใส', tone: 'good' }
  return { icon: '⛅', title: 'มีเมฆบางส่วน', tone: 'neutral' }
})

const headline = computed(() => {
  if (!total.value) return 'ชวนผู้เล่นบันทึกอารมณ์หลังฝึก เพื่อดูภาพรวมที่นี่'
  if (weather.value.tone === 'tense') return `${tense.value}% ของชาว breez กำลังรู้สึกเครียด`
  return `${good.value}% ของชาว breez รู้สึกดี`
})

const caption = computed(() => {
  const p = props.periodLabel || ''
  if (!total.value) return ''
  if (weather.value.tone === 'tense')
    return `🌧 ช่วง ${p} ${tense.value}% ของชาว breez รู้สึกเครียด\nมาหยุดพักแล้วหายใจไปด้วยกัน เข้า 4 วิ กลั้น 7 วิ ออก 8 วิ 🌿\nส่งกำลังใจให้กันนะ #breez #หายใจไปด้วยกัน`
  if (weather.value.tone === 'good')
    return `☀ ช่วง ${p} ${good.value}% ของชาว breez รู้สึกดี\nขอบคุณทุกลมหายใจที่ช่วยให้ใจเบาลง 🌸\nชวนเพื่อนมาฝึกหายใจด้วยกัน #breez`
  return `⛅ อารมณ์ชาว breez ช่วง ${p} มีเมฆบางส่วน\nลองหายใจช้าๆ สัก 1 นาที แล้วส่งกำลังใจให้คนข้างๆ 🌿 #breez`
})

const copied = ref(false)
async function copyCaption() {
  try {
    await navigator.clipboard.writeText(caption.value)
    copied.value = true
    setTimeout(() => (copied.value = false), 1800)
  } catch { /* clipboard ไม่พร้อม */ }
}
</script>

<template>
  <article class="panel weather-card" :class="weather.tone">
    <BreezFlower class="weather-flower" :size="190" color="#f2b6ca" />
    <div class="panel-heading"><div><h2>Overall Mood · พยากรณ์อารมณ์</h2><p>อารมณ์ล่าสุดของ {{ number(total) }} คน · {{ periodLabel }}</p></div></div>

    <div class="weather-main">
      <div class="weather-icon">{{ weather.icon }}</div>
      <div>
        <div class="weather-title">{{ weather.title }}</div>
        <div class="weather-headline">{{ headline }}</div>
      </div>
    </div>

    <template v-if="total">
      <div class="tone-bar">
        <span class="tone-good" :style="{ width: good + '%' }"></span>
        <span class="tone-neutral" :style="{ width: neutral + '%' }"></span>
        <span class="tone-tense" :style="{ width: tense + '%' }"></span>
      </div>
      <div class="tone-legend">
        <span><i class="tone-good"></i>รู้สึกดี {{ good }}%</span>
        <span><i class="tone-neutral"></i>กลางๆ {{ neutral }}%</span>
        <span><i class="tone-tense"></i>เครียด/โกรธ {{ tense }}%</span>
      </div>

      <ul class="weather-facts">
        <li v-if="tenseDiff != null">
          ความเครียด{{ tenseDiff <= 0 ? 'ลดลง' : 'เพิ่มขึ้น' }}
          <b :class="tenseDiff <= 0 ? 'up' : 'down'">{{ Math.abs(tenseDiff) }}%</b> จากช่วงก่อน
        </li>
        <li v-if="idx.tense">
          <b>{{ number(idx.tense_then_practiced) }}</b> จาก {{ number(idx.tense) }} คนที่รู้สึกเครียด ได้มาฝึกหายใจแล้ว
        </li>
      </ul>

      <div class="caption-box">
        <div class="caption-head"><span>ข้อความชวนฝึก / ส่งกำลังใจ</span><button class="secondary-button small" @click="copyCaption">{{ copied ? 'คัดลอกแล้ว ✓' : 'คัดลอก' }}</button></div>
        <p>{{ caption }}</p>
      </div>
    </template>
  </article>
</template>
