-- =====================================================================
-- API หลังบ้าน (Admin Dashboard)
-- security definer + ตรวจสิทธิ์ทุกฟังก์ชัน
--
-- สิทธิ์                     owner   admin
-- ภาพรวม / กราฟ               ✓       ✓
-- รายชื่อผู้เล่น              ✓       ✓ (อีเมลถูกซ่อนบางส่วน)
-- ประวัติการเล่น              ✓       ✓
-- อุปกรณ์                    ✓       ✓
-- บันทึกอารมณ์รายบุคคล        ✓       ✗ (เห็นเฉพาะสัดส่วนรวม)
-- จัดการผู้ดูแล / ลบผู้เล่น    ✓       ✗
-- =====================================================================

create or replace function public._require_staff()
returns text language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
declare r text;
begin
  select role into r from public.staff_roles where user_id = auth.uid();
  if r is null then
    raise exception 'permission denied: staff only' using errcode = '42501';
  end if;
  return r;
end $$;

create or replace function public._require_owner()
returns void language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
begin
  if not exists (select 1 from public.staff_roles where user_id = auth.uid() and role = 'owner') then
    raise exception 'permission denied: owner only' using errcode = '42501';
  end if;
end $$;

create or replace function public._mask_email(e text)
returns text language sql immutable set search_path = ''
as $$
  select case when e is null or position('@' in e) = 0 then e
         else left(split_part(e, '@', 1), 2) || '•••@' || split_part(e, '@', 2) end
$$;

-- ---------------------------------------------------------------------
-- ข้อมูลผู้ดูแลที่ login อยู่
-- ---------------------------------------------------------------------
create or replace function public.dashboard_me()
returns jsonb language sql stable security definer set search_path = ''
as $$
  select jsonb_build_object(
    'user_id', r.user_id,
    'role',    r.role,
    'email',   a.email,
    'name',    coalesce(nullif(u.name, ''), split_part(a.email, '@', 1))
  )
  from public.staff_roles r
  join auth.users a on a.id = r.user_id
  left join public.users u on u.user_id = r.user_id
  where r.user_id = auth.uid();
$$;

-- ---------------------------------------------------------------------
-- ภาพรวม
-- p_days = 7 / 30 / null (ทั้งหมด)
-- ---------------------------------------------------------------------
create or replace function public.dashboard_overview(p_days int default 7)
returns jsonb language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
declare
  v_role  text := public._require_staff();
  v_from  timestamptz := case when p_days is null then '-infinity'::timestamptz else now() - make_interval(days => p_days) end;
  v_prev  timestamptz := case when p_days is null then null else now() - make_interval(days => p_days * 2) end;
  v_chart int := least(coalesce(p_days, 30), 30);
  v_today date := (now() at time zone 'Asia/Bangkok')::date;
  result  jsonb;
begin
  with players as (
    select u.* from public.users u
    where not exists (select 1 from public.staff_roles r where r.user_id = u.user_id)
  ),
  cur  as (select * from public.play_sessions where played_at >= v_from),
  prev as (select * from public.play_sessions where v_prev is not null and played_at >= v_prev and played_at < v_from)
  select jsonb_build_object(
    'role',               v_role,
    'players_total',      (select count(*) from players),
    'players_new',        (select count(*) from players where created_at >= v_from),
    'players_new_prev',   (select count(*) from players where v_prev is not null and created_at >= v_prev and created_at < v_from),
    'active_players',     (select count(distinct user_id) from cur),
    'sessions',           (select count(*) from cur),
    'sessions_prev',      (select count(*) from prev),
    'minutes',            (select coalesce(round(sum(duration) / 60.0, 1), 0) from cur),
    'minutes_prev',       (select coalesce(round(sum(duration) / 60.0, 1), 0) from prev),
    'avg_score',          (select coalesce(round(avg(score) filter (where mode in ('Calm Flow','Power Release','Breathe & Focus')), 1), 0) from cur),
    'avg_duration',       (select coalesce(round(avg(duration)), 0) from cur),
    'moods_total',        (select count(*) from public.mood_logs where recorded_at >= v_from),
    'devices_total',      (select count(*) from public.devices),
    'devices_online',     (select count(*) from public.devices where connection_status = 'connected' and updated_at > now() - interval '5 minutes'),
    'devices_low_battery',(select count(*) from public.devices where battery_level < 20),
    'daily', (
      select coalesce(jsonb_agg(jsonb_build_object(
        'day',        g.d,
        'calm',       (select count(*) from public.play_sessions s where (s.played_at at time zone 'Asia/Bangkok')::date = g.d and s.mode = 'Calm Flow'),
        'power',      (select count(*) from public.play_sessions s where (s.played_at at time zone 'Asia/Bangkok')::date = g.d and s.mode = 'Power Release'),
        'focus',      (select count(*) from public.play_sessions s where (s.played_at at time zone 'Asia/Bangkok')::date = g.d and s.mode = 'Breathe & Focus'),
        'relax',      (select count(*) from public.play_sessions s where (s.played_at at time zone 'Asia/Bangkok')::date = g.d and s.mode in ('Soundscape','Quick Session'))
      ) order by g.d), '[]'::jsonb)
      from (select generate_series(v_today - (v_chart - 1), v_today, interval '1 day')::date as d) g
    ),
    'modes', (
      select coalesce(jsonb_agg(jsonb_build_object('mode', mode, 'count', c) order by c desc), '[]'::jsonb)
      from (select mode, count(*) c from cur group by mode) t
    ),
    'moods', (
      select coalesce(jsonb_agg(jsonb_build_object('mood', mood, 'count', c) order by c desc), '[]'::jsonb)
      from (select mood, count(*) c from public.mood_logs where recorded_at >= v_from group by mood) t
    ),
    'recent_sessions', (
      select coalesce(jsonb_agg(x order by x->>'played_at' desc), '[]'::jsonb) from (
        select jsonb_build_object(
          'session_id', s.session_id, 'user_id', s.user_id, 'player', coalesce(u.name, '—'),
          'mode', s.mode, 'score', s.score, 'duration', s.duration, 'played_at', s.played_at) x
        from public.play_sessions s left join public.users u on u.user_id = s.user_id
        order by s.played_at desc limit 5
      ) t
    ),
    'recent_moods', case when v_role = 'owner' then (
      select coalesce(jsonb_agg(x order by x->>'recorded_at' desc), '[]'::jsonb) from (
        select jsonb_build_object('player', coalesce(u.name, '—'), 'mood', m.mood, 'note', m.note, 'recorded_at', m.recorded_at) x
        from public.mood_logs m left join public.users u on u.user_id = m.user_id
        order by m.recorded_at desc limit 5
      ) t
    ) else null end
  ) into result;

  return result;
end $$;

-- ---------------------------------------------------------------------
-- รายชื่อผู้เล่น
-- ---------------------------------------------------------------------
create or replace function public.dashboard_players(
  p_search text default null, p_limit int default 20, p_offset int default 0
) returns table (
  user_id uuid, name text, email text, created_at timestamptz,
  sessions bigint, minutes numeric, best_score int, last_mood text,
  last_active timestamptz, battery_level int, total_count bigint
)
language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
declare v_role text := public._require_staff();
begin
  return query
  with base as (
    select u.*
    from public.users u
    where not exists (select 1 from public.staff_roles r where r.user_id = u.user_id)
      and (p_search is null or p_search = ''
           or u.name ilike '%' || p_search || '%'
           or u.email ilike '%' || p_search || '%'
           or u.user_id::text ilike p_search || '%')
  ),
  agg as (
    select b.user_id,
      count(s.session_id) as sessions,
      coalesce(round(sum(s.duration) / 60.0, 1), 0) as minutes,
      coalesce(max(s.score), 0) as best_score,
      greatest(max(s.played_at), (select max(m.recorded_at) from public.mood_logs m where m.user_id = b.user_id)) as last_active
    from base b left join public.play_sessions s on s.user_id = b.user_id
    group by b.user_id
  )
  select b.user_id, b.name,
    case when v_role = 'owner' then b.email else public._mask_email(b.email) end,
    b.created_at, a.sessions, a.minutes, a.best_score,
    (select m.mood from public.mood_logs m where m.user_id = b.user_id order by m.recorded_at desc limit 1),
    a.last_active,
    (select d.battery_level from public.devices d where d.user_id = b.user_id order by d.updated_at desc limit 1),
    count(*) over ()
  from base b join agg a on a.user_id = b.user_id
  order by a.last_active desc nulls last, b.created_at desc
  limit greatest(least(p_limit, 200), 1) offset greatest(p_offset, 0);
end $$;

-- ---------------------------------------------------------------------
-- ประวัติการเล่น (พร้อมผลเฉพาะโหมด)
-- ---------------------------------------------------------------------
create or replace function public.dashboard_sessions(
  p_mode text default null, p_days int default null, p_search text default null,
  p_user_id uuid default null, p_limit int default 50, p_offset int default 0
) returns table (
  session_id bigint, user_id uuid, player text, mode text, score int, duration int,
  played_at timestamptz, device_name text,
  flame_out_count int, balloons_total int, balloons_popped int, obstacles_avoided int,
  total_count bigint
)
language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
begin
  perform public._require_staff();
  return query
  select s.session_id, s.user_id, coalesce(u.name, '—'), s.mode, s.score, s.duration, s.played_at,
    d.device_name, cf.flame_out_count, pr.balloons_total, pr.balloons_popped, bf.obstacles_avoided,
    count(*) over ()
  from public.play_sessions s
  left join public.users u on u.user_id = s.user_id
  left join public.devices d on d.device_id = s.device_id
  left join public.calm_flow_results cf on cf.session_id = s.session_id
  left join public.power_release_results pr on pr.session_id = s.session_id
  left join public.breathe_focus_results bf on bf.session_id = s.session_id
  where (p_mode is null or p_mode = '' or s.mode = p_mode)
    and (p_days is null or s.played_at >= now() - make_interval(days => p_days))
    and (p_user_id is null or s.user_id = p_user_id)
    and (p_search is null or p_search = '' or u.name ilike '%' || p_search || '%' or s.session_id::text = p_search)
  order by s.played_at desc
  limit greatest(least(p_limit, 1000), 1) offset greatest(p_offset, 0);
end $$;

-- ---------------------------------------------------------------------
-- บันทึกอารมณ์รายบุคคล (owner เท่านั้น)
-- ---------------------------------------------------------------------
create or replace function public.dashboard_moods(
  p_days int default null, p_search text default null, p_limit int default 50, p_offset int default 0
) returns table (
  mood_id bigint, user_id uuid, player text, mood text, note text, recorded_at timestamptz, total_count bigint
)
language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
begin
  perform public._require_owner();
  return query
  select m.mood_id, m.user_id, coalesce(u.name, '—'), m.mood, m.note, m.recorded_at, count(*) over ()
  from public.mood_logs m left join public.users u on u.user_id = m.user_id
  where (p_days is null or m.recorded_at >= now() - make_interval(days => p_days))
    and (p_search is null or p_search = '' or u.name ilike '%' || p_search || '%' or m.mood ilike p_search)
  order by m.recorded_at desc
  limit greatest(least(p_limit, 500), 1) offset greatest(p_offset, 0);
end $$;

-- ---------------------------------------------------------------------
-- อุปกรณ์
-- ---------------------------------------------------------------------
create or replace function public.dashboard_devices(p_search text default null)
returns table (
  device_id uuid, device_name text, device_icon text, owner_name text, user_id uuid,
  battery_level int, connection_status text, is_online boolean, updated_at timestamptz, sessions bigint
)
language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
begin
  perform public._require_staff();
  return query
  select d.device_id, d.device_name, d.device_icon, coalesce(u.name, '—'), d.user_id,
    d.battery_level, d.connection_status,
    (d.connection_status = 'connected' and d.updated_at > now() - interval '5 minutes'),
    d.updated_at,
    (select count(*) from public.play_sessions s where s.device_id = d.device_id)
  from public.devices d left join public.users u on u.user_id = d.user_id
  where p_search is null or p_search = ''
     or d.device_name ilike '%' || p_search || '%'
     or u.name ilike '%' || p_search || '%'
     or d.device_id::text ilike p_search || '%'
  order by d.updated_at desc nulls last;
end $$;

-- ---------------------------------------------------------------------
-- จัดการผู้ดูแล (owner เท่านั้น)
-- ---------------------------------------------------------------------
create or replace function public.staff_list()
returns table (user_id uuid, email text, name text, role text, created_at timestamptz, last_sign_in_at timestamptz)
language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
begin
  perform public._require_owner();
  return query
  select r.user_id, a.email::text, coalesce(nullif(u.name, ''), split_part(a.email, '@', 1)), r.role, r.created_at, a.last_sign_in_at
  from public.staff_roles r
  join auth.users a on a.id = r.user_id
  left join public.users u on u.user_id = r.user_id
  order by case r.role when 'owner' then 0 else 1 end, r.created_at;
end $$;

-- ให้สิทธิ์ผู้ใช้ที่มีบัญชีอยู่แล้ว (role = 'owner' | 'admin')
create or replace function public.staff_set_role(p_email text, p_role text)
returns void language plpgsql security definer set search_path = ''
as $$
#variable_conflict use_column
declare v_uid uuid;
begin
  perform public._require_owner();
  if p_role not in ('owner', 'admin') then
    raise exception 'invalid role %', p_role;
  end if;
  select id into v_uid from auth.users where lower(email) = lower(trim(p_email));
  if v_uid is null then
    raise exception 'ไม่พบบัญชีอีเมลนี้ในระบบ';
  end if;
  if v_uid = auth.uid() and p_role <> 'owner' then
    raise exception 'ไม่สามารถลดสิทธิ์ตัวเองได้';
  end if;
  insert into public.staff_roles (user_id, role) values (v_uid, p_role)
  on conflict (user_id) do update set role = excluded.role;
end $$;

create or replace function public.staff_remove(p_user_id uuid)
returns void language plpgsql security definer set search_path = ''
as $$
#variable_conflict use_column
begin
  perform public._require_owner();
  if p_user_id = auth.uid() then
    raise exception 'ไม่สามารถลบสิทธิ์ตัวเองได้';
  end if;
  delete from public.staff_roles where user_id = p_user_id;
end $$;

-- ลบผู้เล่นทั้งบัญชี (ข้อมูลที่เกี่ยวข้องจะถูกลบตาม cascade)
create or replace function public.owner_delete_player(p_user_id uuid)
returns void language plpgsql security definer set search_path = ''
as $$
#variable_conflict use_column
begin
  perform public._require_owner();
  if exists (select 1 from public.staff_roles where user_id = p_user_id) then
    raise exception 'บัญชีนี้เป็นผู้ดูแล ลบสิทธิ์ผู้ดูแลก่อน';
  end if;
  delete from auth.users where id = p_user_id;
end $$;

-- ---------------------------------------------------------------------
-- ตั้ง owner คนแรก: รันใน SQL Editor เท่านั้น (ผู้ใช้ทั่วไปเรียกไม่ได้)
--   select public.grant_owner_by_email('you@example.com');
-- ---------------------------------------------------------------------
create or replace function public.grant_owner_by_email(p_email text)
returns text language plpgsql security definer set search_path = ''
as $$
#variable_conflict use_column
declare v_uid uuid;
begin
  select id into v_uid from auth.users where lower(email) = lower(trim(p_email));
  if v_uid is null then
    raise exception 'ไม่พบบัญชี % (สร้างใน Authentication > Users ก่อน)', p_email;
  end if;
  insert into public.staff_roles (user_id, role) values (v_uid, 'owner')
  on conflict (user_id) do update set role = 'owner';
  return 'owner granted to ' || p_email;
end $$;

-- ---------- สิทธิ์การเรียกฟังก์ชัน ----------
do $$
declare f text;
begin
  foreach f in array array[
    'public._require_staff()', 'public._require_owner()', 'public.dashboard_me()',
    'public.dashboard_overview(int)', 'public.dashboard_players(text,int,int)',
    'public.dashboard_sessions(text,int,text,uuid,int,int)', 'public.dashboard_moods(int,text,int,int)',
    'public.dashboard_devices(text)', 'public.staff_list()', 'public.staff_set_role(text,text)',
    'public.staff_remove(uuid)', 'public.owner_delete_player(uuid)',
    'public.staff_role()', 'public.is_staff()', 'public.is_owner()'
  ] loop
    execute format('revoke execute on function %s from public, anon', f);
    execute format('grant execute on function %s to authenticated', f);
  end loop;
end $$;

revoke execute on function public.grant_owner_by_email(text) from public, anon, authenticated;
revoke execute on function public.handle_new_user() from public, anon, authenticated;
