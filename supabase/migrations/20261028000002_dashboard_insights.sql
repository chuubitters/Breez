-- =====================================================================
-- dashboard_insights: ข้อมูลเชิงสรุปสำหรับหน้า ภาพรวม / ผู้เล่น
-- - จำนวนคนที่มาฝึก (รวม + รายวัน), สัดส่วนรูปแบบการฝึก (ครั้ง + คน + %)
-- - อารมณ์: จำนวนคนในแต่ละอารมณ์ (นับจากอารมณ์ล่าสุดของแต่ละคนในช่วงเวลา) + %
-- - ดัชนีอารมณ์โดยรวม (ดี / กลางๆ / เครียด) เทียบช่วงก่อน สำหรับใช้ทำการตลาด
-- - ผู้เล่น: ทั้งหมด / ใช้งาน / ใหม่ / กลับมาฝึกซ้ำ + รายวัน
-- ไม่มีข้อมูลระบุตัวบุคคล ใช้ได้ทั้ง owner และ admin
-- =====================================================================
create or replace function public.dashboard_insights(p_days int default 7)
returns jsonb language plpgsql stable security definer set search_path = ''
as $$
#variable_conflict use_column
declare
  v_from  timestamptz := case when p_days is null then '-infinity'::timestamptz else now() - make_interval(days => p_days) end;
  v_prev  timestamptz := case when p_days is null then null else now() - make_interval(days => p_days * 2) end;
  v_chart int := least(coalesce(p_days, 30), 30);
  v_today date := (now() at time zone 'Asia/Bangkok')::date;
  result  jsonb;
begin
  perform public._require_staff();

  with staff as (select user_id from public.staff_roles),
  players as (select u.* from public.users u where u.user_id not in (select user_id from staff)),
  s  as (select * from public.play_sessions where played_at >= v_from and user_id not in (select user_id from staff)),
  sp as (select * from public.play_sessions where v_prev is not null and played_at >= v_prev and played_at < v_from and user_id not in (select user_id from staff)),
  m  as (select * from public.mood_logs where recorded_at >= v_from and user_id not in (select user_id from staff)),
  mp as (select * from public.mood_logs where v_prev is not null and recorded_at >= v_prev and recorded_at < v_from and user_id not in (select user_id from staff)),
  -- อารมณ์ล่าสุดของแต่ละคนในช่วงเวลา
  latest  as (select distinct on (user_id) user_id, mood from m  order by user_id, recorded_at desc),
  latestp as (select distinct on (user_id) user_id, mood from mp order by user_id, recorded_at desc),
  modes(mode, ord) as (values ('Calm Flow',1),('Power Release',2),('Breathe & Focus',3),('Soundscape',4),('Quick Session',5)),
  moods(mood, ord, tone) as (values ('Happy',1,'good'),('Lovely',2,'good'),('Bored',3,'neutral'),('Stress',4,'tense'),('Angry',5,'tense')),
  days as (select generate_series(v_today - (v_chart - 1), v_today, interval '1 day')::date as d)
  select jsonb_build_object(
    'period_days', p_days,

    -- ---------- การฝึก ----------
    'practitioners',      (select count(distinct user_id) from s),
    'practitioners_prev', (select count(distinct user_id) from sp),
    'sessions',           (select count(*) from s),
    'sessions_prev',      (select count(*) from sp),
    'minutes',            (select coalesce(round(sum(duration) / 60.0, 1), 0) from s),
    'minutes_prev',       (select coalesce(round(sum(duration) / 60.0, 1), 0) from sp),
    'avg_minutes_per_person', (select coalesce(round(sum(duration) / 60.0 / nullif(count(distinct user_id), 0), 1), 0) from s),
    'training_types', (
      select jsonb_agg(jsonb_build_object(
        'mode', md.mode,
        'sessions', (select count(*) from s where s.mode = md.mode),
        'people',   (select count(distinct user_id) from s where s.mode = md.mode),
        'minutes',  (select coalesce(round(sum(duration) / 60.0, 1), 0) from s where s.mode = md.mode)
      ) order by md.ord) from modes md),
    'daily', (
      select jsonb_agg(jsonb_build_object(
        'day',    g.d,
        'calm',   (select count(*) from public.play_sessions x where (x.played_at at time zone 'Asia/Bangkok')::date = g.d and x.mode = 'Calm Flow' and x.user_id not in (select user_id from staff)),
        'power',  (select count(*) from public.play_sessions x where (x.played_at at time zone 'Asia/Bangkok')::date = g.d and x.mode = 'Power Release' and x.user_id not in (select user_id from staff)),
        'focus',  (select count(*) from public.play_sessions x where (x.played_at at time zone 'Asia/Bangkok')::date = g.d and x.mode = 'Breathe & Focus' and x.user_id not in (select user_id from staff)),
        'relax',  (select count(*) from public.play_sessions x where (x.played_at at time zone 'Asia/Bangkok')::date = g.d and x.mode in ('Soundscape','Quick Session') and x.user_id not in (select user_id from staff)),
        'people', (select count(distinct x.user_id) from public.play_sessions x where (x.played_at at time zone 'Asia/Bangkok')::date = g.d and x.user_id not in (select user_id from staff)),
        'new_players', (select count(*) from players p where (p.created_at at time zone 'Asia/Bangkok')::date = g.d)
      ) order by g.d) from days g),

    -- ---------- อารมณ์ ----------
    'mood_people', (select count(*) from latest),
    'mood_logs',   (select count(*) from m),
    'moods', (
      select jsonb_agg(jsonb_build_object(
        'mood', mo.mood, 'tone', mo.tone,
        'people', (select count(*) from latest l where l.mood = mo.mood),
        'logs',   (select count(*) from m where m.mood = mo.mood)
      ) order by mo.ord) from moods mo),
    'mood_index', jsonb_build_object(
      'good',    (select count(*) from latest l join moods mo using (mood) where mo.tone = 'good'),
      'neutral', (select count(*) from latest l join moods mo using (mood) where mo.tone = 'neutral'),
      'tense',   (select count(*) from latest l join moods mo using (mood) where mo.tone = 'tense'),
      'total',   (select count(*) from latest),
      'prev_good',  (select count(*) from latestp l join moods mo using (mood) where mo.tone = 'good'),
      'prev_tense', (select count(*) from latestp l join moods mo using (mood) where mo.tone = 'tense'),
      'prev_total', (select count(*) from latestp),
      -- คนที่บันทึกว่าเครียด/โกรธ แล้วมาฝึกหายใจในช่วงเดียวกัน
      'tense_then_practiced', (
        select count(*) from latest l join moods mo using (mood)
        where mo.tone = 'tense' and exists (select 1 from s where s.user_id = l.user_id))
    ),

    -- ---------- ผู้เล่น ----------
    'players_total',    (select count(*) from players),
    'players_new',      (select count(*) from players where created_at >= v_from),
    'players_new_prev', (select count(*) from players where v_prev is not null and created_at >= v_prev and created_at < v_from),
    'returning_players', (
      select count(*) from (
        select user_id from s group by user_id
        having count(distinct (played_at at time zone 'Asia/Bangkok')::date) >= 2) r),
    'never_played', (select count(*) from players p where not exists (select 1 from public.play_sessions x where x.user_id = p.user_id))
  ) into result;

  return result;
end $$;

revoke execute on function public.dashboard_insights(int) from public, anon;
grant execute on function public.dashboard_insights(int) to authenticated;
