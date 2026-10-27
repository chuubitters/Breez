// สร้างบัญชีผู้ดูแล (owner/admin) ใหม่ — เรียกได้เฉพาะ owner
// ใช้ service role key ฝั่ง server เท่านั้น (Supabase ใส่ให้อัตโนมัติใน Edge Function)
import { createClient } from "npm:@supabase/supabase-js@2";

const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { ...cors, "Content-Type": "application/json" } });

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: cors });
  if (req.method !== "POST") return json({ error: "Method not allowed" }, 405);

  const url = Deno.env.get("SUPABASE_URL")!;
  const serviceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
  const admin = createClient(url, serviceKey, { auth: { persistSession: false } });

  // ตรวจว่าผู้เรียกเป็น owner
  const token = (req.headers.get("Authorization") ?? "").replace(/^Bearer\s+/i, "");
  const { data: caller, error: callerErr } = await admin.auth.getUser(token);
  if (callerErr || !caller?.user) return json({ error: "กรุณาเข้าสู่ระบบ" }, 401);

  const { data: roleRow } = await admin
    .from("staff_roles").select("role").eq("user_id", caller.user.id).maybeSingle();
  if (roleRow?.role !== "owner") return json({ error: "เฉพาะเจ้าของระบบเท่านั้น" }, 403);

  let body: { email?: string; password?: string; name?: string; role?: string };
  try { body = await req.json(); } catch { return json({ error: "Invalid JSON" }, 400); }

  const email = (body.email ?? "").trim().toLowerCase();
  const password = body.password ?? "";
  const name = (body.name ?? "").trim();
  const role = body.role === "owner" ? "owner" : "admin";

  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) return json({ error: "อีเมลไม่ถูกต้อง" }, 400);
  if (password.length < 8) return json({ error: "รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร" }, 400);

  const { data: created, error } = await admin.auth.admin.createUser({
    email, password, email_confirm: true, user_metadata: { name: name || email.split("@")[0] },
  });
  if (error || !created.user) {
    const msg = /already/i.test(error?.message ?? "")
      ? "อีเมลนี้มีบัญชีอยู่แล้ว ใช้ช่อง 'ให้สิทธิ์บัญชีที่มีอยู่' แทน"
      : (error?.message ?? "สร้างบัญชีไม่สำเร็จ");
    return json({ error: msg }, 400);
  }

  const { error: roleErr } = await admin
    .from("staff_roles").upsert({ user_id: created.user.id, role });
  if (roleErr) return json({ error: roleErr.message }, 500);

  return json({ ok: true, user_id: created.user.id, email, role });
});
