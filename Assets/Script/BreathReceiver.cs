using UnityEngine;

public class BreathReceiver : MonoBehaviour
{
    public int breathLevel;

    public void OnMessageArrived(string message)
    {
        message = message.Trim();

        if (int.TryParse(message, out int value))
        {
            breathLevel = Mathf.Clamp(value, 0, 100);

            Debug.Log("Breath Level: " + breathLevel);
        }
    }
}