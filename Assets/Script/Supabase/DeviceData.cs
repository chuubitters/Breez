// using Supabase.Postgrest.Attributes;
// using Supabase.Postgrest.Models;

// [Table("devices")]
// public class DeviceData : BaseModel
// {
//     [PrimaryKey("device_id", false)]
//     public string DeviceId { get; set; }

//     [Column("user_id")]
//     public string UserId { get; set; }

//     [Column("device_name")]
//     public string DeviceName { get; set; }

//     [Column("device_icon")]
//     public string DeviceIcon { get; set; }

//     [Column("battery_level")]
//     public int? BatteryLevel { get; set; }

//     [Column("connection_status")]
//     public string ConnectionStatus { get; set; }

//     [Column("created_at")]
//     public System.DateTime CreatedAt { get; set; }

//     [Column("updated_at")]
//     public System.DateTime UpdatedAt { get; set; }
// }