-- รองรับการส่งข้อมูลย้อนหลัง (เกมเก็บคิวไว้ตอนออฟไลน์ แล้วส่งทีหลัง)
-- เวลาที่ส่งมาต้องไม่อยู่ในอนาคต และไม่เก่ากว่า 30 วัน

drop function if exists public.record_play_session(text, int, int, uuid, jsonb);

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

drop function if exists public.log_mood(text, text);

create or replace function public.log_mood(p_mood text, p_note text default null, p_recorded_at timestamptz default null)
returns bigint
language sql security invoker set search_path = ''
as $$
  insert into public.mood_logs (user_id, mood, note, recorded_at)
  values (auth.uid(), p_mood, nullif(trim(p_note), ''),
          least(greatest(coalesce(p_recorded_at, now()), now() - interval '30 days'), now()))
  returning mood_id;
$$;

revoke execute on function public.record_play_session(text, int, int, uuid, jsonb, timestamptz) from anon, public;
revoke execute on function public.log_mood(text, text, timestamptz) from anon, public;
grant  execute on function public.record_play_session(text, int, int, uuid, jsonb, timestamptz) to authenticated;
grant  execute on function public.log_mood(text, text, timestamptz) to authenticated;
