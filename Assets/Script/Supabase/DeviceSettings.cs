// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;
// using System.Threading.Tasks;
// using Supabase.Postgrest;

// public class DeviceSettings : MonoBehaviour
// {
//     [Header("UI")]
//     public TMP_InputField deviceNameInput;
//     public Image deviceIconImage;
//     public TMP_Text statusText;

//     [Header("Icons")]
//     public Sprite catIcon;
//     public Sprite flowerIcon;
//     public Sprite cloudIcon;
//     public Sprite starIcon;

//     private DeviceData currentDevice;
//     private string selectedIcon = "cat";

//     private async void Start()
//     {
//         await LoadDevice();
//     }

//     public async Task LoadDevice()
//     {
//         try
//         {
//             if (SupabaseManager.Instance == null ||
//                 SupabaseManager.Instance.Client == null)
//             {
//                 Debug.LogError("SupabaseManager is not ready.");
//                 return;
//             }

//             var user = SupabaseManager.Instance.Client.Auth.CurrentUser;

//             if (user == null)
//             {
//                 Debug.LogWarning("User is not logged in.");
//                 statusText.text = "Not logged in";
//                 return;
//             }

//             string userId = user.Id;

//             var result = await SupabaseManager.Instance.Client
//                 .From<DeviceData>()
//                 .Where(x => x.UserId == userId)
//                 .Get();

//             if (result.Models.Count > 0)
//             {
//                 currentDevice = result.Models[0];

//                 deviceNameInput.text = currentDevice.DeviceName;

//                 selectedIcon = currentDevice.DeviceIcon;

//                 UpdateIconUI(selectedIcon);

//                 if (statusText != null)
//                     statusText.text = "Loaded";
//             }
//             else
//             {
//                 await CreateDefaultDevice(userId);
//             }
//         }
//         catch (System.Exception e)
//         {
//             Debug.LogError("Load device error: " + e.Message);

//             if (statusText != null)
//                 statusText.text = "Load failed";
//         }
//     }

//     private async Task CreateDefaultDevice(string userId)
//     {
//         try
//         {
//             var newDevice = new DeviceData
//             {
//                 UserId = userId,
//                 DeviceName = "Breath Gadget",
//                 DeviceIcon = "cat",
//                 BatteryLevel = null,
//                 ConnectionStatus = "disconnected"
//             };

//             var result = await SupabaseManager.Instance.Client
//                 .From<DeviceData>()
//                 .Insert(newDevice);

//             if (result.Models.Count > 0)
//             {
//                 currentDevice = result.Models[0];

//                 deviceNameInput.text = currentDevice.DeviceName;

//                 selectedIcon = currentDevice.DeviceIcon;

//                 UpdateIconUI(selectedIcon);
//             }
//         }
//         catch (System.Exception e)
//         {
//             Debug.LogError("Create device error: " + e.Message);
//         }
//     }

//     public void SelectCat()
//     {
//         selectedIcon = "cat";
//         UpdateIconUI(selectedIcon);
//     }

//     public void SelectFlower()
//     {
//         selectedIcon = "flower";
//         UpdateIconUI(selectedIcon);
//     }

//     public void SelectCloud()
//     {
//         selectedIcon = "cloud";
//         UpdateIconUI(selectedIcon);
//     }

//     public void SelectStar()
//     {
//         selectedIcon = "star";
//         UpdateIconUI(selectedIcon);
//     }

//     private void UpdateIconUI(string iconName)
//     {
//         switch (iconName)
//         {
//             case "cat":
//                 deviceIconImage.sprite = catIcon;
//                 break;

//             case "flower":
//                 deviceIconImage.sprite = flowerIcon;
//                 break;

//             case "cloud":
//                 deviceIconImage.sprite = cloudIcon;
//                 break;

//             case "star":
//                 deviceIconImage.sprite = starIcon;
//                 break;

//             default:
//                 deviceIconImage.sprite = catIcon;
//                 break;
//         }
//     }

//     public async void SaveDevice()
//     {
//         if (currentDevice == null)
//         {
//             Debug.LogWarning("No device loaded.");
//             return;
//         }

//         string newName = deviceNameInput.text.Trim();

//         if (string.IsNullOrEmpty(newName))
//         {
//             statusText.text = "Please enter device name.";
//             return;
//         }

//         try
//         {
//             currentDevice.DeviceName = newName;
//             currentDevice.DeviceIcon = selectedIcon;

//             await currentDevice.Update<DeviceData>();

//             statusText.text = "Saved";

//             Debug.Log(
//                 $"Device saved: {currentDevice.DeviceName}, " +
//                 $"Icon: {currentDevice.DeviceIcon}"
//             );
//         }
//         catch (System.Exception e)
//         {
//             Debug.LogError("Save device error: " + e.Message);

//             statusText.text = "Save failed";
//         }
//     }
// }