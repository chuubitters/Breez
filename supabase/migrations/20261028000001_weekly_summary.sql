-- สรุปสำหรับหน้า Weekly Summary / Mood Tracking ในเกม
-- เพิ่ม today_mood, top_mood, mode_counts และรายละเอียดผลเกมใน recent_sessions
-- ค่าที่ไม่มีใน recent_sessions ส่งเป็น -1 (Unity JsonUtility อ่าน null เป็น 0 ไม่ได้แยก)

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
