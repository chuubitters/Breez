-- Soundscape + Quick Session: เก็บรายละเอียด Quick Session และสถิติสำหรับหลังบ้าน
-- (ไม่มีการ DROP อะไร)

-- 1) ผล Quick Session (4-7-8 บนตัวเครื่อง)
create table if not exists public.quick_session_results (
  result_id  bigint generated always as identity primary key,
  session_id bigint not null unique references public.play_sessions(session_id) on delete cascade,
  pattern    text,
  rounds     integer not null default 0,
  blow_pct   integer,
  source     text
);
alter table public.quick_session_results enable row level security;

do $$ begin
  if not exists (select 1 from pg_policies where tablename = 'quick_session_results' and policyname = 'qs_select') then
    create policy qs_select on public.quick_session_results for select to authenticated
      using ((select public.is_owner()) or exists (
        select 1 from public.play_sessions s
        where s.session_id = quick_session_results.session_id and s.user_id = (select auth.uid())));
  end if;
end $$;

-- 2) record_play_session: เพิ่มการบันทึก Quick Session
create or replace function public.record_play_session(p_mode text, p_score integer, p_duration integer,
  p_device_id uuid default null, p_details jsonb default '{}'::jsonb, p_played_at timestamptz default null)
returns bigint
language plpgsql
set search_path to ''
as $function$
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
  elsif p_mode = 'Soundscape' then
    insert into public.soundscape_results (session_id, scene, breath_cycles, exhale_ms)
    values (v_id,
      nullif(left(trim(coalesce(d ->> 'scene', '')), 40), ''),
      greatest(coalesce((d ->> 'breath_cycles')::int, 0), 0),
      greatest(coalesce((d ->> 'exhale_ms')::int, 0), 0));
  elsif p_mode = 'Quick Session' then
    insert into public.quick_session_results (session_id, pattern, rounds, blow_pct, source)
    values (v_id,
      nullif(left(trim(coalesce(d ->> 'pattern', '4-7-8')), 20), ''),
      greatest(coalesce((d ->> 'rounds')::int, 0), 0),
      case when d ? 'blow_pct' then least(greatest((d ->> 'blow_pct')::int, 0), 100) end,
      nullif(left(trim(coalesce(d ->> 'source', '')), 20), ''));
  end if;

  return v_id;
end $function$;

-- 3) สถิติ Soundscape + Quick Session สำหรับหลังบ้าน
create or replace function public.dashboard_relax_stats(p_days integer default 30)
returns jsonb
language plpgsql
stable security definer
set search_path to ''
as $function$
declare
  v_from timestamptz := case when p_days is null then '-infinity'::timestamptz else now() - make_interval(days => p_days) end;
  v_days integer := coalesce(p_days, 30);
  r jsonb;
begin
  perform public._require_staff();

  with s as (
    select ps.*, (ps.played_at at time zone 'Asia/Bangkok')::date as day
    from public.play_sessions ps
    where ps.mode in ('Soundscape', 'Quick Session') and ps.played_at >= v_from
  ),
  ss as (select s.*, x.scene, x.breath_cycles, x.exhale_ms from s left join public.soundscape_results x on x.session_id = s.session_id where s.mode = 'Soundscape'),
  qs as (select s.*, q.rounds, q.blow_pct, q.source from s left join public.quick_session_results q on q.session_id = s.session_id where s.mode = 'Quick Session'),
  days as (
    select g::date as day
    from generate_series((now() at time zone 'Asia/Bangkok')::date - (least(v_days, 90) - 1), (now() at time zone 'Asia/Bangkok')::date, interval '1 day') g
  )
  select jsonb_build_object(
    'soundscape', jsonb_build_object(
      'sessions',     (select count(*) from ss),
      'players',      (select count(distinct user_id) from ss),
      'minutes',      (select coalesce(round(sum(duration) / 60.0, 1), 0) from ss),
      'avg_minutes',  (select coalesce(round(avg(duration) / 60.0, 1), 0) from ss),
      'total_cycles', (select coalesce(sum(breath_cycles), 0) from ss),
      'avg_cycles',   (select coalesce(round(avg(breath_cycles), 1), 0) from ss),
      'avg_exhale_s', (select coalesce(round(avg(exhale_ms) / 1000.0, 1), 0) from ss),
      'scenes', coalesce((select jsonb_agg(x order by x.sessions desc) from (
          select coalesce(scene, 'ไม่ระบุ') as scene, count(*) as sessions,
                 round(sum(duration) / 60.0, 1) as minutes, round(avg(breath_cycles), 1) as avg_cycles
          from ss group by 1) x), '[]'::jsonb)
    ),
    'quick', jsonb_build_object(
      'sessions',     (select count(*) from qs),
      'players',      (select count(distinct user_id) from qs),
      'minutes',      (select coalesce(round(sum(duration) / 60.0, 1), 0) from qs),
      'avg_minutes',  (select coalesce(round(avg(duration) / 60.0, 1), 0) from qs),
      'avg_rounds',   (select coalesce(round(avg(rounds), 1), 0) from qs),
      'completed',    (select count(*) from qs where rounds >= 4),
      'avg_blow_pct', (select round(avg(blow_pct)) from qs where blow_pct is not null)
    ),
    'daily', coalesce((select jsonb_agg(jsonb_build_object(
        'day', to_char(days.day, 'YYYY-MM-DD'),
        'soundscape', (select count(*) from s where s.day = days.day and s.mode = 'Soundscape'),
        'quick',      (select count(*) from s where s.day = days.day and s.mode = 'Quick Session'),
        'minutes',    (select coalesce(round(sum(duration) / 60.0, 1), 0) from s where s.day = days.day)
      ) order by days.day) from days), '[]'::jsonb),
    'recent', coalesce((select jsonb_agg(x) from (
        select s.session_id, s.mode, coalesce(u.name, '—') as player, s.played_at, s.duration,
               x1.scene, x1.breath_cycles, x1.exhale_ms, q.rounds, q.blow_pct
        from s
        left join public.users u on u.user_id = s.user_id
        left join public.soundscape_results x1 on x1.session_id = s.session_id
        left join public.quick_session_results q on q.session_id = s.session_id
        order by s.played_at desc limit 20) x), '[]'::jsonb)
  ) into r;
  return r;
end $function$;

revoke all on function public.dashboard_relax_stats(integer) from public, anon;
grant execute on function public.dashboard_relax_stats(integer) to authenticated;
