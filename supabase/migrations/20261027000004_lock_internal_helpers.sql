-- ฟังก์ชันช่วยภายใน ถูกเรียกจากฟังก์ชัน security definer เท่านั้น ไม่ต้องเปิดให้เรียกผ่าน API
revoke execute on function public._require_staff() from authenticated;
revoke execute on function public._require_owner() from authenticated;
revoke execute on function public.staff_role() from authenticated;
