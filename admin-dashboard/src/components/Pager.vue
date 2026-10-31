<script setup>
const props = defineProps({ page: Number, pageSize: Number, total: Number })
const emit = defineEmits(['update:page'])
const pages = () => Math.max(1, Math.ceil((props.total || 0) / props.pageSize))
</script>

<template>
  <div class="pager">
    <span>{{ total ? `${(page - 1) * pageSize + 1}–${Math.min(page * pageSize, total)} จากทั้งหมด ${total.toLocaleString('th-TH')} รายการ` : 'ไม่มีข้อมูล' }}</span>
    <div class="btns">
      <button class="btn" :disabled="page <= 1" @click="emit('update:page', page - 1)">ก่อนหน้า</button>
      <button class="btn" :disabled="page >= pages()" @click="emit('update:page', page + 1)">ถัดไป</button>
    </div>
  </div>
</template>
