using System;
using System.Collections.Generic; // Required if using error map later

namespace DeepSimGames.UnchartedReach.Common.DTOs
{
    // Generic wrapper to match backend response structure
    [Serializable] // Helps with Unity's JsonUtility if used, good practice anyway
    public class ApiResponse<T>
    {
        public string status; // e.g., "SUCCESS", "FAIL", "ERROR"
        public string message;
        public T data; // Generic data payload (e.g., PlayerProfileDto)
        public string code; // Your custom error code
                            // public Dictionary<string, object> error; // Removed as per your update
        public string requestId;
        public string timestamp; // Using string, can parse to DateTimeOffset if needed

        // Default constructor needed for deserialization
        public ApiResponse() { }
    }
}