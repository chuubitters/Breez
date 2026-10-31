<script setup>
// ผู้เล่น: ใครเล่นบ่อย เล่นโหมดไหน ใครหายไป — กดแถวเพื่อดูประวัติการเล่นของคนนั้น
import { computed, ref } from 'vue'
import { rpc } from '../lib/supabase'
import { useRemoteList } from '../lib/useRemoteList'
import { loadSessions } from '../lib/stats'
import { MODE_COLOR, MOODS, activity, number, when } from '../lib/format'
import Pager from '../components/Pager.vue'

const emit = defineEmits(['navigate'])
const search = ref('')
const list = useRemoteList(
  ({ limit, offset }) => rpc('dashboard_players', { p_search: search.value || null, p_limit: limit, p_offset: offset }),
  { pageSize: 25, deps: [search] },
)

// โหมดที่เล่นบ่อยที่สุดของแต่ละคน (90 วันล่าสุด)
const favorite = ref({})
loadSessions(90).then(({ cur }) => {
  const by = {}
  for (const r of cur) { by[r.user_id] ??= {}; by[r.user_id][r.mode] = (by[r.user_id][r.mode] || 0) + 1 }
  favorite.value = Object.fromEntries(Object.entries(by).map(([u, m]) => [u, Object.entries(m).sort((a, b) => b[1] - a[1])[0][0]]))
}).catch(() => {})

const rows = computed(() => list.rows.value.map(p => ({ ...p, status: activity(p.last_active), fav: favorite.value[p.user_id] })))
</script>

<template>
  <section class="card">
    <div class="toolbar" style="margin-bottom:12px">
      <input v-model="search" class="input search" placeholder="ค้นหาชื่อหรืออีเมล" />
      <span class="spacer"></span>
      <span class="muted small">กดที่แถวเพื่อดูประวัติการเล่นของผู้เล่นคนนั้น · ไม่รวมบัญชีผู้ดูแล</span>
    </div>
    <p v-if="list.error.value" class="banner err">{{ list.error.value }}</p>
    <div class="table-wrap" :class="{ loading: list.loading.value }">
      <table>
        <thead><tr><th>ผู้เล่น</th><th>สถานะ</th><th class="r">ครั้งที่เล่น</th><th class="r">เวลารวม</th><th>เล่นบ่อย</th><th>อารมณ์ล่าสุด</th><th>เล่นล่าสุด</th></tr></thead>
        <tbody>
          <tr v-for="p in rows" :key="p.user_id" class="click" @click="emit('navigate', 'sessions', { player: { id: p.user_id, name: p.name } })">
            <td><strong>{{ p.name }}</strong><span class="sub">{{ p.email }}</span></td>
            <td><span class="status" :class="p.status.cls">{{ p.status.text }}</span></td>
            <td class="r num">{{ number(p.sessions) }}</td>
            <td class="r num">{{ number(p.minutes, 1) }} นาที</td>
            <td><template v-if="p.fav"><i class="dot" :style="{ background: MODE_COLOR[p.fav] }"></i>{{ p.fav }}</template><span v-else class="muted">—</span></td>
            <td>{{ p.last_mood ? (MOODS[p.last_mood]?.th ?? p.last_mood) : '—' }}</td>
            <td class="muted">{{ when(p.last_active) }}</td>
          </tr>
          <tr v-if="!list.loading.value && !rows.length"><td colspan="7" class="empty">ไม่พบผู้เล่น</td></tr>
        </tbody>
      </table>
    </div>
    <Pager v-model:page="list.page.value" :page-size="list.pageSize" :total="list.total.value" />
  </section>
</template>
