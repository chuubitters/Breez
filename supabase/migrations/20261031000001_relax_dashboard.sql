-- =====================================================================
-- Soundscape + Quick Session ในหลังบ้านและประวัติในเกม
--  1) dashboard_play_sessions_v2 : เหมือน dashboard_play_sessions + รอบ / % เป่า ของ Quick Session
--  2) my_history                 : เพิ่ม exhale_ms (Soundscape) และ rounds / blow_pct (Quick Session)
-- (dashboard_relax_stats ถูกสร้างไว้แล้วใน migration relax_modes)
-- =====================================================================

create or replace function public.dashboard_play_sessions_v2(
  p_mode text default null, p_days integer default null, p_search text default null,
  p_user_id uuid default null, p_limit integer default 50, p_offset integer default 0)
returns table(session_id bigint, user_id uuid, player text, mode text, score integer, duration integer,
  played_at timestamptz, device_name text, flame_out_count integer, flowers_grown integer,
  largest_flower_pct integer, longest_exhale_ms integer, total_exhale_ms integer, balloons_total integer,
  balloons_popped integer, obstacles_avoided integer, scene text, breath_cycles integer, exhale_ms integer,
  rounds integer, blow_pct integer, source text, total_count bigint)
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
    qs.rounds, qs.blow_pct, qs.source,
    count(*) over ()
  from public.play_sessions s
  left join public.users u on u.user_id = s.user_id
  left join public.devices d on d.device_id = s.device_id
  left join public.calm_flow_results cf on cf.session_id = s.session_id
  left join public.power_release_results pr on pr.session_id = s.session_id
  left join public.breathe_focus_results bf on bf.session_id = s.session_id
  left join public.soundscape_results ss on ss.session_id = s.session_id
  left join public.quick_session_results qs on qs.session_id = s.session_id
  where (p_mode is null or p_mode = '' or s.mode = p_mode)
    and (p_days is null or s.played_at >= now() - make_interval(days => p_days))
    and (p_user_id is null or s.user_id = p_user_id)
    and (p_search is null or p_search = '' or u.name ilike '%' || p_search || '%' or s.session_id::text = p_search)
  order by s.played_at desc
  limit greatest(least(p_limit, 1000), 1) offset greatest(p_offset, 0);
end $$;

revoke execute on function public.dashboard_play_sessions_v2(text, int, text, uuid, int, int) from public, anon;
grant  execute on function public.dashboard_play_sessions_v2(text, int, text, uuid, int, int) to authenticated;

create or replace function public.my_history(p_from date, p_to date)
returns jsonb language sql stable set search_path = ''
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
        'breath_cycles',      coalesce(ss.breath_cycles, -1),
        'exhale_ms',          coalesce(ss.exhale_ms, -1),
        'rounds',             coalesce(qs.rounds, -1),
        'blow_pct',           coalesce(qs.blow_pct, -1)
      ) order by s.played_at desc)
      from s
      left join public.calm_flow_results cf on cf.session_id = s.session_id
      left join public.power_release_results pr on pr.session_id = s.session_id
      left join public.breathe_focus_results bf on bf.session_id = s.session_id
      left join public.soundscape_results ss on ss.session_id = s.session_id
      left join public.quick_session_results qs on qs.session_id = s.session_id), '[]'::jsonb),
    'moods', coalesce((
      select jsonb_agg(jsonb_build_object(
        'date', to_char(m.lt, 'YYYY-MM-DD'), 'time', to_char(m.lt, 'HH24:MI'),
        'mood', m.mood, 'note', coalesce(m.note, '')
      ) order by m.recorded_at desc) from m), '[]'::jsonb)
  );
$$;
