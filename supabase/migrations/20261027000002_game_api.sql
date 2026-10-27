-- =====================================================================
-- API ฝั่งเกม Unity (เรียกผ่าน POST /rest/v1/rpc/<name>)
-- ทั้งหมดเป็น security invoker => RLS ทำงานตามผู้เล่นที่ login อยู่
-- =====================================================================

-- บันทึกผลการเล่น 1 ครั้ง + ผลเฉพาะโหมด ในคำสั่งเดียว
-- p_details ตัวอย่าง:
--   Calm Flow       {"flame_out_count": 2}
--   Power Release   {"balloons_total": 20, "balloons_popped": 15}
--   Breathe & Focus {"obstacles_avoided": 12}
create or replace function public.record_play_session(
  p_mode      text,
  p_score     int,
  p_duration  int,
  p_device_id uuid  default null,
  p_details   jsonb default '{}'::jsonb
) returns bigint
language plpgsql security invoker set search_path = ''
as $$
declare
  v_id bigint;
  d    jsonb := coalesce(p_details, '{}'::jsonb);
begin
  if auth.uid() is null then
    raise exception 'not authenticated' using errcode = '28000';
  end if;

  insert into public.play_sessions (user_id, device_id, mode, score, duration)
  values (auth.uid(), p_device_id, p_mode, greatest(coalesce(p_score, 0), 0), greatest(coalesce(p_duration, 0), 0))
  returning session_id into v_id;

  if p_mode = 'Calm Flow' then
    insert into public.calm_flow_results (session_id, flame_out_count)
    values (v_id, coalesce((d ->> 'flame_out_count')::int, 0));
  elsif p_mode = 'Power Release' then
    insert into public.power_release_results (session_id, balloons_total, balloons_popped)
    values (v_id, coalesce((d ->> 'balloons_total')::int, 0), coalesce((d ->> 'balloons_popped')::int, 0));
  elsif p_mode = 'Breathe & Focus' then
    insert into public.breathe_focus_results (session_id, obstacles_avoided)
    values (v_id, coalesce((d ->> 'obstacles_avoided')::int, 0));
  end if;

  return v_id;
end $$;

-- บันทึกอารมณ์
create or replace function public.log_mood(p_mood text, p_note text default null)
returns bigint
language sql security invoker set search_path = ''
as $$
  insert into public.mood_logs (user_id, mood, note)
  values (auth.uid(), p_mood, nullif(trim(p_note), ''))
  returning mood_id;
$$;

-- โหลด/สร้าง/อัปเดตอุปกรณ์ของผู้เล่น (1 ผู้เล่นใช้อุปกรณ์หลัก 1 เครื่อง)
-- ส่งเฉพาะค่าที่ต้องการเปลี่ยน ค่าที่เป็น null จะไม่ถูกแก้
create or replace function public.sync_my_device(
  p_battery int  default null,
  p_status  text default null,
  p_name    text default null,
  p_icon    text default null
) returns jsonb
language plpgsql security invoker set search_path = ''
as $$
declare
  v public.devices;
begin
  if auth.uid() is null then
    raise exception 'not authenticated' using errcode = '28000';
  end if;

  select * into v from public.devices
  where user_id = auth.uid()
  order by created_at
  limit 1;

  if not found then
    insert into public.devices (user_id) values (auth.uid()) returning * into v;
  end if;

  update public.devices set
    battery_level     = coalesce(p_battery, battery_level),
    connection_status = coalesce(p_status, connection_status),
    device_name       = coalesce(nullif(trim(p_name), ''), device_name),
    device_icon       = coalesce(nullif(trim(p_icon), ''), device_icon)
  where device_id = v.device_id
  returning * into v;

  return to_jsonb(v);
end $$;

-- สรุปข้อมูลของผู้เล่นเอง สำหรับหน้า Mood Tracking & Summary ในเกม
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
    -- จำนวนวันติดต่อกันที่ฝึก (นับถึงวันนี้หรือเมื่อวาน)
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
      select jsonb_agg(jsonb_build_object('mode', mode, 'score', score, 'duration', duration, 'played_at', played_at) order by played_at desc)
      from (select * from s order by played_at desc limit 10) t), '[]'::jsonb)
  );
$$;

revoke execute on function public.record_play_session(text, int, int, uuid, jsonb) from anon, public;
revoke execute on function public.log_mood(text, text)                         from anon, public;
revoke execute on function public.sync_my_device(int, text, text, text)        from anon, public;
revoke execute on function public.my_summary(int)                              from anon, public;
grant  execute on function public.record_play_session(text, int, int, uuid, jsonb) to authenticated;
grant  execute on function public.log_mood(text, text)                         to authenticated;
grant  execute on function public.sync_my_device(int, text, text, text)        to authenticated;
grant  execute on function public.my_summary(int)                              to authenticated;
