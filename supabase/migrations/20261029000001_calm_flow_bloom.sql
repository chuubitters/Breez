-- Calm Flow แบบใหม่: เป่าให้ดอกไม้บาน (แทนเป่าประคองเปลวไฟ)
-- เก็บ: จำนวนดอก, ดอกใหญ่สุด (%), เป่าต่อเนื่องนานสุด / รวม (ms), เวลา session ที่เลือก (นาที)
-- details ที่เกมส่งมา:
--   {"flowers_grown": 12, "largest_flower_pct": 95, "longest_exhale_ms": 7400, "total_exhale_ms": 68000, "session_minutes": 2}

alter table public.calm_flow_results
  add column if not exists flowers_grown      int not null default 0 check (flowers_grown >= 0),
  add column if not exists largest_flower_pct int not null default 0 check (largest_flower_pct between 0 and 100),
  add column if not exists longest_exhale_ms  int not null default 0 check (longest_exhale_ms >= 0),
  add column if not exists total_exhale_ms    int not null default 0 check (total_exhale_ms >= 0),
  add column if not exists session_minutes    int check (session_minutes between 1 and 60);

-- ---------------------------------------------------------------------
-- บันทึกผล (signature เดิม)
-- ---------------------------------------------------------------------
create or replace function public.record_play_session(
  p_mode      text,
  p_score     int,
  p_duration  int,
  p_device_id uuid        default null,
  p_details   jsonb       default '{}'::jsonb,
  p_played_at timestamptz default null
) returns bigint
language plpgsql security invoker set search_path = ''
as $$
declare
  v_id bigint;
  d    jsonb := coalesce(p_details, '{}'::jsonb);
  v_at timestamptz := least(greatest(coalesce(p_played_at, now()), now() - interval '30 days'), now());
begin
  if auth.uid() is null then
    raise exception 'not authenticated' using errcode = '28000';
  end if;

  insert into public.play_sessions (user_id, device_id, mode, score, duration, played_at)
  values (auth.uid(), p_device_id, p_mode, greatest(coalesce(p_score, 0), 0), greatest(coalesce(p_duration, 0), 0), v_at)
  returning session_id into v_id;

  if p_mode = 'Calm Flow' then
    insert into public.calm_flow_results
      (session_id, flame_out_count, flowers_grown, largest_flower_pct, longest_exhale_ms, total_exhale_ms, session_minutes)
    values (v_id,
      greatest(coalesce((d ->> 'flame_out_count')::int, 0), 0),
      greatest(coalesce((d ->> 'flowers_grown')::int, 0), 0),
      least(greatest(coalesce((d ->> 'largest_flower_pct')::int, 0), 0), 100),
      greatest(coalesce((d ->> 'longest_exhale_ms')::int, 0), 0),
      greatest(coalesce((d ->> 'total_exhale_ms')::int, 0), 0),
      case when (d ->> 'session_minutes')::int between 1 and 60 then (d ->> 'session_minutes')::int end);
  elsif p_mode = 'Power Release' then
    insert into public.power_release_results (session_id, balloons_total, balloons_popped)
    values (v_id, coalesce((d ->> 'balloons_total')::int, 0), coalesce((d ->> 'balloons_popped')::int, 0));
  elsif p_mode = 'Breathe & Focus' then
    insert into public.breathe_focus_results (session_id, obstacles_avoided)
    values (v_id, coalesce((d ->> 'obstacles_avoided')::int, 0));
  end if;

  return v_id;
end $$;

-- ---------------------------------------------------------------------
-- ประวัติการเล่นในหลังบ้าน: เพิ่มผล Calm Flow (เปลี่ยนคอลัมน์ต้อง drop ก่อน)
-- ---------------------------------------------------------------------
drop function if exists public.dashboard_sessions(text, int, text, uuid, int, int);

create or replace function public.dashboard_sessions(
  p_mode text default null, p_days int default null, p_search text default null,
  p_user_id uuid default null, p_limit int default 50, p_offset int default 0
) returns table (
  session_id bigint, user_id uuid, player text, mode text, score int, duration int,
  played_at timestamptz, device_name text,
  flame_out_count int, flowers_grown int, largest_flower_pct int, longest_exhale_ms int, total_exhale_ms int,
  balloons_total int, balloons_popped int, obstacles_avoided int,
  total_count bigint
)
language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
begin
  perform public._require_staff();
  return query
  select s.session_id, s.user_id, coalesce(u.name, '—'), s.mode, s.score, s.duration, s.played_at,
    d.device_name, cf.flame_out_count, cf.flowers_grown, cf.largest_flower_pct, cf.longest_exhale_ms, cf.total_exhale_ms,
    pr.balloons_total, pr.balloons_popped, bf.obstacles_avoided,
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

revoke execute on function public.dashboard_sessions(text, int, text, uuid, int, int) from public, anon;
grant  execute on function public.dashboard_sessions(text, int, text, uuid, int, int) to authenticated;

-- ---------------------------------------------------------------------
-- สรุปในเกม: เพิ่มผล Calm Flow ใน recent_sessions
-- ---------------------------------------------------------------------
create or replace function public.my_summary(p_days int default 30)
returns jsonb
language sql stable security invoker set search_path = ''
as $$
  with s as (
    select * from public.play_sessions
    where user_id = auth.uid()
      and (p_days is null or played_at >= now() - make_interval(days => p_days))
  ),
  m as (
    select * from public.mood_logs
    where user_id = auth.uid()
      and (p_days is null or recorded_at >= now() - make_interval(days => p_days))
  ),
  days as (
    select distinct (played_at at time zone 'Asia/Bangkok')::date as d
    from public.play_sessions where user_id = auth.uid()
  ),
  streak as (
    select count(*)::int as n from (
      select d, d + (row_number() over (order by d desc))::int as grp from days
    ) x
    where grp = (select max(d) + 1 from days)
      and (select max(d) from days) >= (now() at time zone 'Asia/Bangkok')::date - 1
  )
  select jsonb_build_object(
    'name',            (select name from public.users where user_id = auth.uid()),
    'total_sessions',  (select count(*) from s),
    'total_minutes',   (select coalesce(round(sum(duration) / 60.0, 1), 0) from s),
    'best_score',      (select coalesce(max(score), 0) from s),
    'streak_days',     coalesce((select n from streak), 0),
    'today_mood', coalesce((
      select mood from public.mood_logs
      where user_id = auth.uid()
        and (recorded_at at time zone 'Asia/Bangkok')::date = (now() at time zone 'Asia/Bangkok')::date
      order by recorded_at desc limit 1), ''),
    'top_mood', coalesce((
      select mood from m group by mood order by count(*) desc, max(recorded_at) desc limit 1), ''),
    'mode_counts', jsonb_build_object(
      'calm',       (select count(*) from s where mode = 'Calm Flow'),
      'power',      (select count(*) from s where mode = 'Power Release'),
      'focus',      (select count(*) from s where mode = 'Breathe & Focus'),
      'soundscape', (select count(*) from s where mode = 'Soundscape'),
      'quick',      (select count(*) from s where mode = 'Quick Session')),
    'modes', coalesce((
      select jsonb_agg(jsonb_build_object('mode', mode, 'count', c, 'best_score', b) order by c desc)
      from (select mode, count(*) c, max(score) b from s group by mode) t), '[]'::jsonb),
    'mood_counts', coalesce((
      select jsonb_agg(jsonb_build_object('mood', mood, 'count', c) order by c desc)
      from (select mood, count(*) c from m group by mood) t), '[]'::jsonb),
    'recent_moods', coalesce((
      select jsonb_agg(jsonb_build_object('mood', mood, 'note', coalesce(note, ''), 'recorded_at', recorded_at) order by recorded_at desc)
      from (select * from m order by recorded_at desc limit 10) t), '[]'::jsonb),
    'recent_sessions', coalesce((
      select jsonb_agg(jsonb_build_object(
        'mode', t.mode, 'score', t.score, 'duration', t.duration, 'played_at', t.played_at,
        'flame_out_count',   coalesce(cf.flame_out_count, -1),
        'flowers_grown',     coalesce(cf.flowers_grown, -1),
        'largest_flower_pct', coalesce(cf.largest_flower_pct, -1),
        'longest_exhale_ms', coalesce(cf.longest_exhale_ms, -1),
        'total_exhale_ms',   coalesce(cf.total_exhale_ms, -1),
        'balloons_total',    coalesce(pr.balloons_total, -1),
        'balloons_popped',   coalesce(pr.balloons_popped, -1),
        'obstacles_avoided', coalesce(bf.obstacles_avoided, -1)
      ) order by t.played_at desc)
      from (select * from s order by played_at desc limit 10) t
      left join public.calm_flow_results cf on cf.session_id = t.session_id
      left join public.power_release_results pr on pr.session_id = t.session_id
      left join public.breathe_focus_results bf on bf.session_id = t.session_id), '[]'::jsonb)
  );
$$;

