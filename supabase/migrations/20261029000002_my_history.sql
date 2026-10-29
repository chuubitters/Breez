-- ประวัติรายวันของผู้เล่นเอง (หน้า Weekly Summary > ดูประวัติ)
-- ช่วงวันที่ตามเวลาไทย p_from..p_to (สูงสุด 62 วัน)
-- คืนค่า:
--   days:     [{date, sessions, minutes, mood}]      ทุกวันในช่วง (mood = อารมณ์ล่าสุดของวันนั้น)
--   sessions: [{date, time, mode, score, duration, ...ผลเฉพาะโหมด}]  (-1 = ไม่มีค่า)
--   moods:    [{date, time, mood, note}]

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
        'obstacles_avoided',  coalesce(bf.obstacles_avoided, -1)
      ) order by s.played_at desc)
      from s
      left join public.calm_flow_results cf on cf.session_id = s.session_id
      left join public.power_release_results pr on pr.session_id = s.session_id
      left join public.breathe_focus_results bf on bf.session_id = s.session_id), '[]'::jsonb),
    'moods', coalesce((
      select jsonb_agg(jsonb_build_object(
        'date', to_char(m.lt, 'YYYY-MM-DD'), 'time', to_char(m.lt, 'HH24:MI'),
        'mood', m.mood, 'note', coalesce(m.note, '')
      ) order by m.recorded_at desc) from m), '[]'::jsonb)
  );
$$;

revoke execute on function public.my_history(date, date) from public, anon;
grant  execute on function public.my_history(date, date) to authenticated;
