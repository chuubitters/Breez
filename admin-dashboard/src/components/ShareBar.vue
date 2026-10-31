<script setup>
// แถบ 100% แบ่งสัดส่วน พร้อมคำอธิบายสี
defineProps({ parts: Array }) // [{ label, value, pct, color }]
</script>

<template>
  <div class="share">
    <div class="share-bar">
      <span v-for="p in parts.filter(x => x.pct > 0)" :key="p.label" :style="{ width: p.pct + '%', background: p.color }" :title="`${p.label} ${p.pct}%`"></span>
    </div>
    <ul class="share-legend">
      <li v-for="p in parts" :key="p.label"><i class="dot" :style="{ background: p.color }"></i>{{ p.label }} <b>{{ p.pct }}%</b><span v-if="p.value != null" class="muted"> · {{ p.value }}</span></li>
    </ul>
  </div>
</template>
