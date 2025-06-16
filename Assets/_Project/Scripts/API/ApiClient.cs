using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using Newtonsoft.Json; // Using Newtonsoft for robust JSON handling
using DeepSimGames.UnchartedReach.Common.DTOs; // Adjust namespace if needed for your DTOs

namespace DeepSimGames.UnchartedReach.API
{
    public class ApiClient : MonoBehaviour
    {
        // Make sure this points to your running backend server
        private string backendUrl = "http://34.41.252.147:8080"; // Or your deployed URL

        // Renamed to match convention, still takes callbacks
        public IEnumerator GetPlayerProfileRoutine(string idToken, Action<PlayerProfileDto> onSuccess, Action<ApiResponse<object>> onError)
        {
            string url = backendUrl + "/api/v1/player/profile";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // Set Headers
                request.SetRequestHeader("Authorization", "Bearer " + idToken);
                print( idToken);
                request.SetRequestHeader("Accept", "application/json"); // Tell server we want JSON
                request.SetRequestHeader("Content-Type", "application/json");

                Debug.Log($"ApiClient: Sending GET request to: {url}");
                yield return request.SendWebRequest(); // Wait for the request to complete

                // --- Handle Response ---
                string responseJson = request.downloadHandler?.text; // Get response body text

                // Network or HTTP Error (e.g., 4xx, 5xx)
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    string errorMsg = $"API Error: {request.error} | Status Code: {request.responseCode}";
                    Debug.LogError(errorMsg);
                    Debug.LogError($"Response Body (Error): {responseJson}"); // Log error body if available

                    // Try to parse standard error response
                    ApiResponse<object> errorResponse = null;
                    if (!string.IsNullOrEmpty(responseJson))
                    {
                        try { errorResponse = JsonConvert.DeserializeObject<ApiResponse<object>>(responseJson); }
                        catch (Exception e) { Debug.LogError($"Failed to parse error JSON: {e.Message}"); }
                    }

                    // Ensure we always pass an ApiResponse<object> to onError
                    if (errorResponse == null)
                    {
                        // Create a default error response if parsing the error body also failed
                        errorResponse = new ApiResponse<object>
                        {
                            status = "FAIL",
                            code = request.responseCode.ToString(),
                            message = request.error,
                            // requestId = Try get from MDC via header? Or leave null
                            timestamp = DateTimeOffset.UtcNow.ToString("o")
                        };
                    }
                    onError?.Invoke(errorResponse); // Pass the guaranteed ApiResponse<object>
                }
                // Success (e.g., 200 OK)
                else
                {
                    Debug.Log($"API Success! Status Code: {request.responseCode}");
                    Debug.Log($"API Response Body: {responseJson}");

                    // Try to parse success response
                    try
                    {
                        // Expecting ApiResponse containing PlayerProfileDto in 'data' field
                        ApiResponse<PlayerProfileDto> apiResponse = JsonConvert.DeserializeObject<ApiResponse<PlayerProfileDto>>(responseJson);

                        if (apiResponse != null && apiResponse.status != null && apiResponse.status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) && apiResponse.data != null)
                        {
                            onSuccess?.Invoke(apiResponse.data); // Pass the extracted PlayerProfileDto
                        }
                        else
                        {
                            // Got a 200 OK but response wasn't the expected success format/content
                            string errorMsg = $"API Success (Code {request.responseCode}) but received unexpected data format or non-SUCCESS status.";
                            Debug.LogError(errorMsg);
                            // Log details if possible
                            if (apiResponse != null)
                            {
                                Debug.LogError($"Received Status: {apiResponse.status}, Data Null: {apiResponse.data == null}, Message: {apiResponse.message}, Code: {apiResponse.code}");
                            }
                            else
                            {
                                Debug.LogError("ApiResponse object was null after deserialization attempt.");
                            }

                            // --- FIX START ---
                            // Create a NEW ApiResponse<object> for the error callback, using info from the parsed response if possible
                            ApiResponse<object> errorData = new ApiResponse<object>
                            {
                                status = apiResponse?.status ?? "FAIL", // Use parsed status if available, else FAIL
                                code = apiResponse?.code ?? "UNEXPECTED_DATA", // Use parsed code or specific one
                                message = apiResponse?.message ?? errorMsg, // Use parsed message or generic one
                                requestId = apiResponse?.requestId, // Pass along if parsed
                                timestamp = apiResponse?.timestamp ?? DateTimeOffset.UtcNow.ToString("o"), // Pass along or generate
                                data = null // No data payload for error
                            };
                            onError?.Invoke(errorData); // Invoke callback with the correctly typed object
                        }
                    }
                    catch (Exception e)
                    {
                        // JSON Parsing failed entirely for a 200 OK response
                        string errorMsg = $"API Success (Code {request.responseCode}) but JSON parsing failed: {e.Message}";
                        Debug.LogError(errorMsg);
                        // Create a NEW specific error response object for the onError callback
                        // Remove the incorrect '??' operator line
                        ApiResponse<object> errorResponse = new ApiResponse<object>
                        {
                            status = "FAIL",
                            code = "JSON_PARSE_ERROR", // Specific code for this failure
                            message = errorMsg,
                            // requestId = Try to get from responseJson if possible/needed, else null
                            timestamp = DateTimeOffset.UtcNow.ToString("o") // Add timestamp
                        };
                        onError?.Invoke(errorResponse); // Invoke callback with the correctly typed object
                    }
                }
            }
        }
    }
}