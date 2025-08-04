using UnityEngine;
using System.IO;

public class FileLogger : MonoBehaviour
{
    private string logFilePath;

    // This is called when the object first becomes active.
    void OnEnable()
    {
        // Define the path for our log file.
        // Application.persistentDataPath is a safe place to save files on any device.
        logFilePath = Path.Combine(Application.persistentDataPath, "my_app_log.txt");

        // Start listening for log messages.
        Application.logMessageReceived += HandleLog;

        Debug.Log("File logger started. Logging to: " + logFilePath);
    }

    // This is called when the object is disabled or destroyed.
    void OnDisable()
    {
        // Stop listening to prevent errors.
        Application.logMessageReceived -= HandleLog;
    }

    /// <summary>
    /// This method is called every time a Debug.Log message is sent.
    /// </summary>
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Format the message with a timestamp, type, and the message itself.
        string formattedMessage = $"[{System.DateTime.Now}] [{type}] {logString}\n";

        // Append the formatted message to the end of our log file.
        File.AppendAllText(logFilePath, formattedMessage);
    }
}