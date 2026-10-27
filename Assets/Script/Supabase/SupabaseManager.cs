// using UnityEngine;
// using Supabase;
// using System.Threading.Tasks;

// public class SupabaseManager : MonoBehaviour
// {
//     public static SupabaseManager Instance;

//     [Header("Supabase")]
//     [SerializeField]
//     private string supabaseUrl =
//         "https://ubdiwwmtbuzksyswmwod.supabase.co";

//     [SerializeField]
//     private string supabasePublishableKey =
//         "sb_publishable_E8-tt98y8Z0-ZxWUPvJgWg_2mYOAGdP";

//     public Client Client { get; private set; }

//     private async void Awake()
//     {
//         if (Instance != null && Instance != this)
//         {
//             Destroy(gameObject);
//             return;
//         }

//         Instance = this;
//         DontDestroyOnLoad(gameObject);

//         await Initialize();
//     }

//     private async Task Initialize()
//     {
//         try
//         {
//             var options = new SupabaseOptions
//             {
//                 AutoRefreshToken = true,
//                 AutoConnectRealtime = false
//             };

//             Client = new Client(
//                 supabaseUrl,
//                 supabasePublishableKey,
//                 options
//             );

//             await Client.InitializeAsync();

//             Debug.Log("✅ Supabase connected successfully!");
//         }
//         catch (System.Exception e)
//         {
//             Debug.LogError("❌ Supabase connection failed: " + e.Message);
//         }
//     }
// }