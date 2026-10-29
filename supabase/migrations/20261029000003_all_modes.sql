-- ทุกโหมดมีข้อมูลเฉพาะของตัวเองในหลังบ้าน
--   Soundscape: ฉาก (Rainy Forest / Windy Mountain), จำนวนรอบหายใจ 5-5 ที่ครบ, เวลาที่เป่า (ms)
--     details: {"scene": "Rainy Forest", "breath_cycles": 6, "exhale_ms": 21000}
--   dashboard_mode_stats(p_days): สถิติแยกตามโหมดสำหรับหน้า "โหมดการฝึก"

create table if not exists public.soundscape_results (
  result_id     bigint generated always as identity primary key,
  session_id    bigint not null unique references public.play_sessions(session_id) on delete cascade,
  scene         text check (scene is null or length(scene) <= 40),
  breath_cycles int not null default 0 check (breath_cycles >= 0),
  exhale_ms     int not null default 0 check (exhale_ms >= 0)
);
alter table public.soundscape_results enable row level security;

create policy ss_select on public.soundscape_results for select to authenticated
  using ((select public.is_owner()) or exists (select 1 from public.play_sessions s where s.session_id = soundscape_results.session_id and s.user_id = (select auth.uid())));
create policy ss_insert on public.soundscape_results for insert to authenticated
  with check (exists (select 1 from public.play_sessions s where s.session_id = soundscape_results.session_id and s.user_id = (select auth.uid())));

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
  elsif p_mode = 'Soundscape' then
    insert into public.soundscape_results (session_id, scene, breath_cycles, exhale_ms)
    values (v_id,
      nullif(left(trim(coalesce(d ->> 'scene', '')), 40), ''),
      greatest(coalesce((d ->> 'breath_cycles')::int, 0), 0),
      greatest(coalesce((d ->> 'exhale_ms')::int, 0), 0));
  end if;

  return v_id;
end $$;

-- ---------------------------------------------------------------------
-- ประวัติการเล่น + ผล Soundscape (ฟังก์ชันใหม่ ไม่ต้อง drop ของเดิม)
create or replace function public.dashboard_play_sessions(
  p_mode text default null, p_days int default null, p_search text default null,
  p_user_id uuid default null, p_limit int default 50, p_offset int default 0
) returns table (
  session_id bigint, user_id uuid, player text, mode text, score int, duration int,
  played_at timestamptz, device_name text,
  flame_out_count int, flowers_grown int, largest_flower_pct int, longest_exhale_ms int, total_exhale_ms int,
  balloons_total int, balloons_popped int, obstacles_avoided int,
  scene text, breath_cycles int, exhale_ms int,
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
    ss.scene, ss.breath_cycles, ss.exhale_ms,
    count(*) over ()
  from public.play_sessions s
  left join public.users u on u.user_id = s.user_id
  left join public.devices d on d.device_id = s.device_id
  left join public.calm_flow_results cf on cf.session_id = s.session_id
  left join public.power_release_results pr on pr.session_id = s.session_id
  left join public.breathe_focus_results bf on bf.session_id = s.session_id
  left join public.soundscape_results ss on ss.session_id = s.session_id
  where (p_mode is null or p_mode = '' or s.mode = p_mode)
    and (p_days is null or s.played_at >= now() - make_interval(days => p_days))
    and (p_user_id is null or s.user_id = p_user_id)
    and (p_search is null or p_search = '' or u.name ilike '%' || p_search || '%' or s.session_id::text = p_search)
  order by s.played_at desc
  limit greatest(least(p_limit, 1000), 1) offset greatest(p_offset, 0);
end $$;

revoke execute on function public.dashboard_play_sessions(text, int, text, uuid, int, int) from public, anon;
grant  execute on function public.dashboard_play_sessions(text, int, text, uuid, int, int) to authenticated;

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
        'obstacles_avoided', coalesce(bf.obstacles_avoided, -1),
        'scene',             coalesce(ss.scene, ''),
        'breath_cycles',     coalesce(ss.breath_cycles, -1)
      ) order by t.played_at desc)
      from (select * from s order by played_at desc limit 10) t
      left join public.calm_flow_results cf on cf.session_id = t.session_id
      left join public.power_release_results pr on pr.session_id = t.session_id
      left join public.breathe_focus_results bf on bf.session_id = t.session_id
      left join public.soundscape_results ss on ss.session_id = t.session_id), '[]'::jsonb)
  );
$$;

-- ---------------------------------------------------------------------
create or replace function public.my_history(p_from date, p_to date)
returns jsonb
language sql stable security invoker set search_path = ''
as $$
  with r as (
    select least(p_from, p_to) as f,
           least(greatest(p_from, p_to), least(p_from, p_to) + 61) as t
  ),
  s as (
    select ps.*, (ps.played_at at time zone 'Asia/Bangkok') as lt
    from public.play_sessions ps, r
    where ps.user_id = auth.uid()
      and (ps.played_at at time zone 'Asia/Bangkok')::date between r.f and r.t
  ),
  m as (
    select ml.*, (ml.recorded_at at time zone 'Asia/Bangkok') as lt
    from public.mood_logs ml, r
    where ml.user_id = auth.uid()
      and (ml.recorded_at at time zone 'Asia/Bangkok')::date between r.f and r.t
  ),
  d as (
    select g::date as d from r, generate_series(r.f, r.t, interval '1 day') g
  )
  select jsonb_build_object(
    'days', coalesce((
      select jsonb_agg(jsonb_build_object(
        'date',     to_char(d.d, 'YYYY-MM-DD'),
        'sessions', (select count(*) from s where s.lt::date = d.d),
        'minutes',  (select coalesce(round(sum(duration) / 60.0, 1), 0) from s where s.lt::date = d.d),
        'mood',     coalesce((select mood from m where m.lt::date = d.d order by m.recorded_at desc limit 1), '')
      ) order by d.d) from d), '[]'::jsonb),
    'sessions', coalesce((
      select jsonb_agg(jsonb_build_object(
        'date', to_char(s.lt, 'YYYY-MM-DD'), 'time', to_char(s.lt, 'HH24:MI'),
        'mode', s.mode, 'score', s.score, 'duration', s.duration,
        'flame_out_count',    coalesce(cf.flame_out_count, -1),
        'flowers_grown',      coalesce(cf.flowers_grown, -1),
        'largest_flower_pct', coalesce(cf.largest_flower_pct, -1),
        'longest_exhale_ms',  coalesce(cf.longest_exhale_ms, -1),
        'total_exhale_ms',    coalesce(cf.total_exhale_ms, -1),
        'balloons_total',     coalesce(pr.balloons_total, -1),
        'balloons_popped',    coalesce(pr.balloons_popped, -1),
        'obstacles_avoided',  coalesce(bf.obstacles_avoided, -1),
        'scene',              coalesce(ss.scene, ''),
        'breath_cycles',      coalesce(ss.breath_cycles, -1)
      ) order by s.played_at desc)
      from s
      left join public.calm_flow_results cf on cf.session_id = s.session_id
      left join public.power_release_results pr on pr.session_id = s.session_id
      left join public.breathe_focus_results bf on bf.session_id = s.session_id
      left join public.soundscape_results ss on ss.session_id = s.session_id), '[]'::jsonb),
    'moods', coalesce((
      select jsonb_agg(jsonb_build_object(
        'date', to_char(m.lt, 'YYYY-MM-DD'), 'time', to_char(m.lt, 'HH24:MI'),
        'mood', m.mood, 'note', coalesce(m.note, '')
      ) order by m.recorded_at desc) from m), '[]'::jsonb)
  );
$$;

-- ---------------------------------------------------------------------
create or replace function public.dashboard_mode_stats(p_days int default null)
returns jsonb
language plpgsql stable security definer set search_path = ''
as $$
declare r jsonb;
begin
  perform public._require_staff();
  with s as (
    select ps.* from public.play_sessions ps
    where (p_days is null or ps.played_at >= now() - make_interval(days => p_days))
      and ps.user_id not in (select sr.user_id from public.staff_roles sr)
  ),
  modes(mode, ord) as (values ('Calm Flow',1),('Power Release',2),('Breathe & Focus',3),('Soundscape',4),('Quick Session',5))
  select jsonb_build_object(
    'modes', (
      select jsonb_agg(jsonb_build_object(
        'mode', m.mode,
        'sessions', (select count(*) from s where s.mode = m.mode),
        'players', (select count(distinct user_id) from s where s.mode = m.mode),
        'minutes', (select coalesce(round(sum(duration) / 60.0, 1), 0) from s where s.mode = m.mode),
        'avg_duration', (select coalesce(round(avg(duration)), 0) from s where s.mode = m.mode),
        'avg_score', (select coalesce(round(avg(score), 1), 0) from s where s.mode = m.mode),
        'best_score', (select coalesce(max(score), 0) from s where s.mode = m.mode),
        'last_played', (select max(played_at) from s where s.mode = m.mode)
      ) order by m.ord) from modes m),
    'calm', (
      select jsonb_build_object(
        'sessions', count(*),
        'flowers', coalesce(sum(cf.flowers_grown), 0),
        'avg_flowers', coalesce(round(avg(cf.flowers_grown), 1), 0),
        'avg_largest_pct', coalesce(round(avg(cf.largest_flower_pct)), 0),
        'full_bloom_sessions', count(*) filter (where cf.largest_flower_pct >= 100),
        'best_longest_exhale_ms', coalesce(max(cf.longest_exhale_ms), 0),
        'avg_longest_exhale_ms', coalesce(round(avg(cf.longest_exhale_ms)), 0),
        'avg_total_exhale_ms', coalesce(round(avg(cf.total_exhale_ms)), 0),
        'minutes_1', count(*) filter (where cf.session_minutes = 1),
        'minutes_2', count(*) filter (where cf.session_minutes = 2),
        'minutes_3', count(*) filter (where cf.session_minutes = 3))
      from s join public.calm_flow_results cf on cf.session_id = s.session_id where s.mode = 'Calm Flow'),
    'power', (
      select jsonb_build_object(
        'sessions', count(*),
        'balloons_popped', coalesce(sum(pr.balloons_popped), 0),
        'avg_popped', coalesce(round(avg(pr.balloons_popped), 1), 0),
        'avg_total', coalesce(round(avg(pr.balloons_total), 1), 0),
        'completed', count(*) filter (where pr.balloons_total > 0 and pr.balloons_popped >= pr.balloons_total),
        'avg_duration_completed', coalesce(round(avg(s.duration) filter (where pr.balloons_total > 0 and pr.balloons_popped >= pr.balloons_total)), 0))
      from s join public.power_release_results pr on pr.session_id = s.session_id where s.mode = 'Power Release'),
    'focus', (
      select jsonb_build_object(
        'sessions', count(*),
        'obstacles', coalesce(sum(bf.obstacles_avoided), 0),
        'avg_obstacles', coalesce(round(avg(bf.obstacles_avoided), 1), 0),
        'best_obstacles', coalesce(max(bf.obstacles_avoided), 0),
        'avg_survive', coalesce(round(avg(s.duration)), 0),
        'best_survive', coalesce(max(s.duration), 0))
      from s join public.breathe_focus_results bf on bf.session_id = s.session_id where s.mode = 'Breathe & Focus'),
    'soundscape', (
      select jsonb_build_object(
        'sessions', (select count(*) from s where s.mode = 'Soundscape'),
        'avg_cycles', coalesce((select round(avg(ss.breath_cycles), 1) from s join public.soundscape_results ss on ss.session_id = s.session_id), 0),
        'scenes', coalesce((
          select jsonb_agg(jsonb_build_object('scene', x.scene, 'sessions', x.n, 'minutes', x.minutes, 'players', x.players) order by x.n desc)
          from (
            select coalesce(ss.scene, 'ไม่ระบุ') as scene, count(*) n, round(sum(s.duration) / 60.0, 1) minutes, count(distinct s.user_id) players
            from s left join public.soundscape_results ss on ss.session_id = s.session_id
            where s.mode = 'Soundscape' group by 1) x), '[]'::jsonb))),
    'quick', (
      select jsonb_build_object('sessions', count(*), 'minutes', coalesce(round(sum(duration) / 60.0, 1), 0), 'players', count(distinct user_id))
      from s where s.mode = 'Quick Session'),
    'daily', (
      select jsonb_agg(to_jsonb(x) order by x.day) from (
        select g.d::date as day,
          count(s.session_id) filter (where s.mode = 'Calm Flow')       as calm,
          count(s.session_id) filter (where s.mode = 'Power Release')   as power,
          count(s.session_id) filter (where s.mode = 'Breathe & Focus') as focus,
          count(s.session_id) filter (where s.mode = 'Soundscape')      as soundscape,
          count(s.session_id) filter (where s.mode = 'Quick Session')   as quick
        from generate_series((now() at time zone 'Asia/Bangkok')::date - (least(coalesce(p_days, 30), 60) - 1),
                             (now() at time zone 'Asia/Bangkok')::date, interval '1 day') g(d)
        left join s on (s.played_at at time zone 'Asia/Bangkok')::date = g.d::date
        group by g.d) x)
  ) into r;
  return r;
end $$;

revoke execute on function public.dashboard_mode_stats(int) from public, anon;
grant  execute on function public.dashboard_mode_stats(int) to authenticated;
