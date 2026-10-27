-- =====================================================================
-- Breez / fuuu : core schema
-- ตารางตาม ERD + ระบบสิทธิ์ (owner / admin) + RLS
-- ใช้ uuid สำหรับผู้ใช้ เพื่อผูกกับ Supabase Auth (auth.users)
-- =====================================================================

-- ---------- users (โปรไฟล์ผู้เล่น ผูกกับ auth.users) ----------
create table if not exists public.users (
  user_id    uuid primary key references auth.users(id) on delete cascade,
  name       text not null default '',
  email      text,
  created_at timestamptz not null default now()
);

-- ---------- staff_roles (ใครเข้าหลังบ้านได้ และสิทธิ์ระดับไหน) ----------
create table if not exists public.staff_roles (
  user_id    uuid primary key references auth.users(id) on delete cascade,
  role       text not null check (role in ('owner', 'admin')),
  created_at timestamptz not null default now()
);

-- ---------- devices (มีอยู่แล้ว ปรับให้ผูกกับ public.users) ----------
create table if not exists public.devices (
  device_id         uuid primary key default gen_random_uuid(),
  user_id           uuid,
  device_name       text not null default 'Breath Gadget',
  device_icon       text not null default 'cat',
  battery_level     int check (battery_level between 0 and 100),
  connection_status text default 'disconnected',
  created_at        timestamptz default now(),
  updated_at        timestamptz default now()
);
alter table public.devices drop constraint if exists devices_user_id_fkey;
alter table public.devices alter column user_id set not null;
alter table public.devices alter column user_id set default auth.uid();
alter table public.devices
  add constraint devices_user_id_fkey foreign key (user_id)
  references public.users(user_id) on delete cascade;
alter table public.devices drop constraint if exists devices_connection_status_check;
alter table public.devices add constraint devices_connection_status_check
  check (connection_status in ('connected', 'disconnected'));
create index if not exists devices_user_id_idx on public.devices(user_id);

-- ---------- mood_logs ----------
create table if not exists public.mood_logs (
  mood_id     bigint generated always as identity primary key,
  user_id     uuid not null default auth.uid() references public.users(user_id) on delete cascade,
  mood        text not null check (mood in ('Lovely', 'Happy', 'Bored', 'Angry', 'Stress')),
  note        text check (char_length(note) <= 280),
  recorded_at timestamptz not null default now()
);
create index if not exists mood_logs_user_idx on public.mood_logs(user_id, recorded_at desc);

-- ---------- play_sessions ----------
create table if not exists public.play_sessions (
  session_id bigint generated always as identity primary key,
  user_id    uuid not null default auth.uid() references public.users(user_id) on delete cascade,
  device_id  uuid references public.devices(device_id) on delete set null,
  mode       text not null check (mode in ('Calm Flow', 'Power Release', 'Breathe & Focus', 'Soundscape', 'Quick Session')),
  score      int not null default 0 check (score between 0 and 1000000),
  duration   int not null default 0 check (duration between 0 and 86400), -- วินาที
  played_at  timestamptz not null default now()
);
create index if not exists play_sessions_user_idx on public.play_sessions(user_id, played_at desc);
create index if not exists play_sessions_played_at_idx on public.play_sessions(played_at desc);
create index if not exists play_sessions_device_idx on public.play_sessions(device_id);

-- ---------- ผลเฉพาะแต่ละเกม (1:1 กับ play_sessions) ----------
create table if not exists public.calm_flow_results (
  result_id       bigint generated always as identity primary key,
  session_id      bigint not null unique references public.play_sessions(session_id) on delete cascade,
  flame_out_count int not null default 0 check (flame_out_count >= 0)
);

create table if not exists public.power_release_results (
  result_id       bigint generated always as identity primary key,
  session_id      bigint not null unique references public.play_sessions(session_id) on delete cascade,
  balloons_total  int not null default 0 check (balloons_total >= 0),
  balloons_popped int not null default 0 check (balloons_popped >= 0)
);

create table if not exists public.breathe_focus_results (
  result_id         bigint generated always as identity primary key,
  session_id        bigint not null unique references public.play_sessions(session_id) on delete cascade,
  obstacles_avoided int not null default 0 check (obstacles_avoided >= 0)
);

-- =====================================================================
-- Helper: ตรวจสิทธิ์
-- =====================================================================
create or replace function public.staff_role()
returns text language sql stable security definer set search_path = ''
as $$ select role from public.staff_roles where user_id = auth.uid() $$;

create or replace function public.is_staff()
returns boolean language sql stable security definer set search_path = ''
as $$ select exists (select 1 from public.staff_roles where user_id = auth.uid()) $$;

create or replace function public.is_owner()
returns boolean language sql stable security definer set search_path = ''
as $$ select exists (select 1 from public.staff_roles where user_id = auth.uid() and role = 'owner') $$;

-- =====================================================================
-- สร้างโปรไฟล์อัตโนมัติเมื่อสมัครสมาชิก (Unity sign up)
-- =====================================================================
create or replace function public.handle_new_user()
returns trigger language plpgsql security definer set search_path = ''
as $$
begin
  insert into public.users (user_id, name, email)
  values (
    new.id,
    coalesce(nullif(trim(new.raw_user_meta_data ->> 'name'), ''), split_part(new.email, '@', 1)),
    new.email
  )
  on conflict (user_id) do nothing;
  return new;
end $$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

create or replace function public.touch_updated_at()
returns trigger language plpgsql set search_path = ''
as $$ begin new.updated_at = now(); return new; end $$;

drop trigger if exists devices_touch on public.devices;
create trigger devices_touch before update on public.devices
  for each row execute function public.touch_updated_at();

-- =====================================================================
-- Row Level Security
-- ผู้เล่น: เห็น/เขียนเฉพาะข้อมูลตัวเอง
-- owner: อ่านทุกตารางได้โดยตรง
-- admin: อ่านผ่านฟังก์ชัน dashboard_* เท่านั้น (ถูกจำกัดบางข้อมูล)
-- =====================================================================
alter table public.users                 enable row level security;
alter table public.staff_roles           enable row level security;
alter table public.devices               enable row level security;
alter table public.mood_logs             enable row level security;
alter table public.play_sessions         enable row level security;
alter table public.calm_flow_results     enable row level security;
alter table public.power_release_results enable row level security;
alter table public.breathe_focus_results enable row level security;

-- users
create policy users_select on public.users for select to authenticated
  using (user_id = (select auth.uid()) or (select public.is_owner()));
create policy users_update_own on public.users for update to authenticated
  using (user_id = (select auth.uid())) with check (user_id = (select auth.uid()));
revoke update on public.users from authenticated, anon;
grant update (name) on public.users to authenticated;

-- staff_roles: เห็นแถวของตัวเอง, owner เห็นทั้งหมด (แก้ไขผ่านฟังก์ชันเท่านั้น)
create policy staff_select on public.staff_roles for select to authenticated
  using (user_id = (select auth.uid()) or (select public.is_owner()));

-- devices
create policy devices_select on public.devices for select to authenticated
  using (user_id = (select auth.uid()) or (select public.is_owner()));
create policy devices_insert_own on public.devices for insert to authenticated
  with check (user_id = (select auth.uid()));
create policy devices_update_own on public.devices for update to authenticated
  using (user_id = (select auth.uid())) with check (user_id = (select auth.uid()));
create policy devices_delete_own on public.devices for delete to authenticated
  using (user_id = (select auth.uid()));

-- mood_logs
create policy mood_select on public.mood_logs for select to authenticated
  using (user_id = (select auth.uid()) or (select public.is_owner()));
create policy mood_insert_own on public.mood_logs for insert to authenticated
  with check (user_id = (select auth.uid()));

-- play_sessions
create policy sessions_select on public.play_sessions for select to authenticated
  using (user_id = (select auth.uid()) or (select public.is_owner()));
create policy sessions_insert_own on public.play_sessions for insert to authenticated
  with check (
    user_id = (select auth.uid())
    and (device_id is null or exists (
      select 1 from public.devices d where d.device_id = play_sessions.device_id and d.user_id = (select auth.uid())))
  );

-- result tables
create policy cf_select on public.calm_flow_results for select to authenticated
  using ((select public.is_owner()) or exists (select 1 from public.play_sessions s where s.session_id = calm_flow_results.session_id and s.user_id = (select auth.uid())));
create policy cf_insert on public.calm_flow_results for insert to authenticated
  with check (exists (select 1 from public.play_sessions s where s.session_id = calm_flow_results.session_id and s.user_id = (select auth.uid())));

create policy pr_select on public.power_release_results for select to authenticated
  using ((select public.is_owner()) or exists (select 1 from public.play_sessions s where s.session_id = power_release_results.session_id and s.user_id = (select auth.uid())));
create policy pr_insert on public.power_release_results for insert to authenticated
  with check (exists (select 1 from public.play_sessions s where s.session_id = power_release_results.session_id and s.user_id = (select auth.uid())));

create policy bf_select on public.breathe_focus_results for select to authenticated
  using ((select public.is_owner()) or exists (select 1 from public.play_sessions s where s.session_id = breathe_focus_results.session_id and s.user_id = (select auth.uid())));
create policy bf_insert on public.breathe_focus_results for insert to authenticated
  with check (exists (select 1 from public.play_sessions s where s.session_id = breathe_focus_results.session_id and s.user_id = (select auth.uid())));
