-- =====================================================================
-- หลังบ้าน: เลือกได้ว่าจะ "รวมบัญชีผู้ดูแล" ในสถิติหรือไม่ (ค่าเริ่มต้น: รวม)
--   ตอนทดสอบเกมด้วยบัญชี owner ข้อมูลจะขึ้นในหน้าภาพรวม / โหมดการฝึก / ผู้เล่น ด้วย
--   owner ปิดได้ที่ปุ่มด้านบนของหลังบ้าน
-- =====================================================================
create table if not exists public.dashboard_settings (
  id            int primary key default 1 check (id = 1),
  include_staff boolean not null default true,
  updated_at    timestamptz not null default now()
);
insert into public.dashboard_settings (id) values (1) on conflict (id) do nothing;
alter table public.dashboard_settings enable row level security;   -- ไม่มี policy: เข้าถึงผ่านฟังก์ชันเท่านั้น

create or replace function public._dash_include_staff()
returns boolean language sql stable security definer set search_path = ''
as $$ select coalesce((select include_staff from public.dashboard_settings where id = 1), true) $$;
revoke execute on function public._dash_include_staff() from public, anon, authenticated;

create or replace function public.dashboard_settings_get()
returns jsonb language plpgsql stable security definer set search_path = ''
as $$
begin
  perform public._require_staff();
  return jsonb_build_object('include_staff', public._dash_include_staff());
end $$;
revoke execute on function public.dashboard_settings_get() from public, anon;
grant  execute on function public.dashboard_settings_get() to authenticated;

create or replace function public.dashboard_set_include_staff(p_include boolean)
returns boolean language plpgsql security definer set search_path = ''
as $$
begin
  perform public._require_owner();
  update public.dashboard_settings set include_staff = coalesce(p_include, true), updated_at = now() where id = 1;
  return public._dash_include_staff();
end $$;
revoke execute on function public.dashboard_set_include_staff(boolean) from public, anon;
grant  execute on function public.dashboard_set_include_staff(boolean) to authenticated;

-- แก้ฟังก์ชันสถิติเดิม: ตัดบัญชีผู้ดูแลออกเฉพาะเมื่อปิด include_staff
do $$
declare f record; d text;
begin
  for f in
    select p.oid from pg_proc p join pg_namespace n on n.oid = p.pronamespace
    where n.nspname = 'public' and p.proname in ('dashboard_insights', 'dashboard_mode_stats', 'dashboard_overview', 'dashboard_players')
  loop
    d := pg_get_functiondef(f.oid);
    continue when position('_dash_include_staff' in d) > 0;
    d := replace(d, 'from public.staff_roles r where r.user_id', 'from public.staff_roles r where not public._dash_include_staff() and r.user_id');
    d := replace(d, 'from public.staff_roles sr)', 'from public.staff_roles sr where not public._dash_include_staff())');
    d := replace(d, 'select user_id from public.staff_roles)', 'select user_id from public.staff_roles where not public._dash_include_staff())');
    execute d;
  end loop;
end $$;
