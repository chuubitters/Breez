import { ref, watch } from 'vue'

// ช่วยโหลดข้อมูลแบบแบ่งหน้า + ค้นหาแบบหน่วงเวลา
export function useRemoteList(fetcher, { pageSize = 20, deps = [] } = {}) {
  const rows = ref([])
  const total = ref(0)
  const page = ref(1)
  const loading = ref(false)
  const error = ref('')
  let seq = 0

  async function load() {
    const my = ++seq
    loading.value = true
    error.value = ''
    try {
      const data = await fetcher({ limit: pageSize, offset: (page.value - 1) * pageSize })
      if (my !== seq) return
      rows.value = data ?? []
      total.value = Number(rows.value[0]?.total_count ?? 0)
    } catch (e) {
      if (my === seq) error.value = e.message
    } finally {
      if (my === seq) loading.value = false
    }
  }

  let timer
  watch(deps, () => {
    clearTimeout(timer)
    timer = setTimeout(() => { page.value = 1; load() }, 300)
  })
  watch(page, load)
  load()

  return { rows, total, page, pageSize, loading, error, load }
}
