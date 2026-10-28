<script setup>
import { computed, ref } from 'vue'
import { rpc } from '../lib/supabase'
import { useRemoteList } from '../lib/useRemoteList'
import { MOODS, STATUS_LABEL, activityStatus, avatarColor, downloadCsv, initials, number, shortId, when } from '../lib/format'
import Pager from '../components/Pager.vue'

const props = defineProps({ me: Object, search: String })
const emit = defineEmits(['navigate'])
const isOwner = computed(() => props.me.role === 'owner')

const list = useRemoteList(
  ({ limit, offset }) => rpc('dashboard_players', { p_search: props.search || null, p_limit: limit, p_offset: offset }),
  { pageSize: 20, deps: [() => props.search] },
)

const summary = ref(null)
rpc('dashboard_overview', { p_days: 30 }).then(d => (summary.value = d)).catch(() => {})

async function exportAll() {
  const rows = await rpc('dashboard_players', { p_search: props.search || null, p_limit: 200, p_offset: 0 })
  downloadCsv('breez-players.csv',
    ['user_id', 'name', 'email', 'joined', 'sessions', 'minutes', 'best_score', 'last_mood', 'last_active'],
    rows.map(p => [p.user_id, p.name, p.email, p.created_at, p.sessions, p.minutes, p.best_score, p.last_mood, p.last_active]))
}

const deleting = ref(null)
async function removePlayer(p) {
  if (!window.confirm(`ลบบัญชี "${p.name}" และข้อมูลการเล่นทั้งหมด?\nการลบไม่สามารถย้อนกลับได้`)) return
  deleting.value = p.user_id
  try {
    await rpc('owner_delete_player', { p_user_id: p.user_id })
    await list.load()
  } catch (e) {
    window.alert(e.message)
  } finally {
    deleting.value = null
  }
}
</script>

<template>
  <section class="welcome-row section-welcome">
    <div><div class="eyebrow"><span class="eyebrow-dot"></span> PLAYER DIRECTORY</div><h1>ผู้เล่นของ<span class="highlight">breez</span></h1><p class="page-description">ข้อมูลผู้เล่นที่ลงทะเบียนผ่านเกม{{ isOwner ? '' : ' (อีเมลถูกซ่อนบางส่วนสำหรับผู้ดูแล)' }}</p></div>
    <div class="header-actions"><button class="secondary-button" @click="exportAll">↓ ส่งออกข้อมูล</button></div>
  </section>

  <section class="stats-grid compact-stats">
    <article class="stat-card stat-lilac"><div class="stat-label">ผู้เล่นทั้งหมด</div><div class="stat-value">{{ number(summary?.players_total) }}</div><div class="stat-foot">บัญชีที่ลงทะเบียน</div></article>
    <article class="stat-card stat-green"><div class="stat-label">ใช้งานอยู่</div><div class="stat-value">{{ number(summary?.active_players) }}</div><div class="stat-foot"><span class="online-dot"></span> เล่นใน 30 วันล่าสุด</div></article>
    <article class="stat-card stat-peach"><div class="stat-label">ผู้เล่นใหม่</div><div class="stat-value">+{{ number(summary?.players_new) }}</div><div class="stat-foot">ใน 30 วันล่าสุด</div></article>
    <article class="stat-card stat-yellow"><div class="stat-label">เฉลี่ย session / คน</div><div class="stat-value">{{ summary?.active_players ? number(summary.sessions / summary.active_players, 2) : '0' }}</div><div class="stat-foot">ใน 30 วันล่าสุด</div></article>
  </section>

  <section class="panel full-panel">
    <div class="panel-heading"><div><h2>รายชื่อผู้เล่น</h2><p>เรียงตามการใช้งานล่าสุด</p></div></div>
    <p v-if="list.error.value" class="banner-error">{{ list.error.value }}</p>
    <div class="table-wrap" :class="{ loading: list.loading.value }">
      <table class="wide-table">
        <thead><tr><th>ผู้เล่น</th><th>รหัสผู้ใช้</th><th>จำนวน session</th><th>เวลาฝึกสะสม</th><th>คะแนนสูงสุด</th><th>อารมณ์ล่าสุด</th><th>ใช้งานล่าสุด</th><th>สถานะ</th><th></th></tr></thead>
        <tbody>
          <tr v-for="p in list.rows.value" :key="p.user_id">
            <td><div class="player-cell"><div class="tiny-avatar" :class="avatarColor(p.user_id)">{{ initials(p.name) }}</div><div><strong>{{ p.name }}</strong><small>{{ p.email }}</small></div></div></td>
            <td class="muted" :title="p.user_id">{{ shortId(p.user_id) }}</td>
            <td>{{ number(p.sessions) }} ครั้ง</td>
            <td>{{ number(p.minutes, 1) }} นาที</td>
            <td>{{ number(p.best_score) }}</td>
            <td><span v-if="p.last_mood" class="mood-tag" :class="MOODS[p.last_mood]?.cls">{{ MOODS[p.last_mood]?.emoji }} {{ p.last_mood }}</span><span v-else class="muted">—</span></td>
            <td>{{ when(p.last_active) }}</td>
            <td><span class="status-label" :class="activityStatus(p.last_active)"><i></i>{{ STATUS_LABEL[activityStatus(p.last_active)] }}</span></td>
            <td><div class="row-actions">
              <button class="text-button" @click="emit('navigate', 'sessions', { player: { id: p.user_id, name: p.name } })">ประวัติ ↗</button>
              <button v-if="isOwner" class="text-button danger" :disabled="deleting === p.user_id" @click="removePlayer(p)">ลบ</button>
            </div></td>
          </tr>
          <tr v-if="!list.loading.value && !list.rows.value.length"><td colspan="9" class="empty-state">{{ search ? 'ไม่พบผู้เล่นที่ตรงกับคำค้นหา' : 'ยังไม่มีผู้เล่นลงทะเบียน' }}</td></tr>
        </tbody>
      </table>
    </div>
    <Pager v-model:page="list.page.value" :page-size="list.pageSize" :total="list.total.value" label="ผู้เล่น" />
  </section>
</template>
