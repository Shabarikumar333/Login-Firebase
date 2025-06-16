// Assets/_Project/Scripts/Auth/AuthManager.cs
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions; // For Task extensions if needed, but async/await preferred
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;
using DeepSimGames.UnchartedReach.API;
using DeepSimGames.UnchartedReach.Core;

namespace DeepSimGames.UnchartedReach.Auth
{
    public class AuthManager : MonoBehaviour
    {
        // Dependencies (Assign in Inspector)
        [SerializeField] private ApiClient apiClient; // Keep for API calls later

        // Public Events for UI/Other systems to subscribe to
        public event Action OnSignInAttempt;
        public event Action<FirebaseUser> OnSignInSuccess;
        public event Action<string> OnSignInFailed; // Pass error message string
        public event Action<string> OnTokenReceived; // Pass token string
        public event Action OnSignOutComplete;
        // Events related to API Calls (can be refined later)
        public event Action OnApiCalled;
        public event Action<string> OnApiSuccess;
        public event Action<string> OnApiFailed;

        // Private State
        private FirebaseAuth auth;
        private FirebaseUser user;
        private string currentIdToken;
        public bool IsSigningIn { get; private set; } = false;
        private bool isFirebaseReady = false;
        private bool hasFetchedProfileThisSession = false;

        void Awake()
        {
            // Subscribe to the FirebaseManager event
            FirebaseManager.OnFirebaseReady += HandleFirebaseReady;
        }

        void HandleFirebaseReady()
        {
            auth = FirebaseManager.Instance.AuthInstance;
            if (auth != null)
            {
                auth.StateChanged += AuthStateChanged; // Listen for login/logout events
                AuthStateChanged(this, null); // Check initial state immediately
                isFirebaseReady = true;
                Debug.Log("AuthManager: Firebase ready, listening for auth state changes.");
            }
            else
            {
                Debug.LogError("AuthManager: FirebaseAuth instance is null after FirebaseReady event!");
                isFirebaseReady = false;
            }
        }

        // Track auth state changes reported by Firebase SDK
        async void AuthStateChanged(object sender, System.EventArgs eventArgs)
        {
            if (!isFirebaseReady || auth == null) return; // Ensure auth is ready

            if (auth.CurrentUser != user) // Check if state actually changed
            {
                bool signedIn = auth.CurrentUser != null;
                user = auth.CurrentUser; // Update internal user state

                if (signedIn)
                {
                    Debug.Log($"AuthManager: AuthStateChanged -> User Signed In: {user.Email} ({user.UserId})");

                    // --- TRIGGER API CALL ON STARTUP IF LOGGED IN ---
                    if (!hasFetchedProfileThisSession) // Only fetch profile once per session/login
                    {
                        Debug.Log("AuthManager: User already logged in or just logged in, fetching profile...");
                        await GetIdTokenAndCallApi(); // Call the existing method
                    }
                    // --- END TRIGGER ---
                }
                else // User is signed OUT
                {
                    Debug.Log("AuthManager: AuthStateChanged -> User Signed Out");
                    currentIdToken = null;
                    hasFetchedProfileThisSession = false; // Reset flag on sign out
                    OnSignOutComplete?.Invoke();
                }
            }
        }

        // --- Register Method ---
        public async Task RegisterWithEmailPassword(string email, string password)
        {
            if (!isFirebaseReady || IsSigningIn)
            {
                OnSignInFailed?.Invoke("Not ready or already processing.");
                return; // Don't proceed if not ready or busy
            }
            IsSigningIn = true;
            OnSignInAttempt?.Invoke();
            Debug.Log($"AuthManager: Attempting Registration for email: [{email}]");

            try
            {
                AuthResult result = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
                // AuthStateChanged listener might fire here, updating 'user' variable
                // It's good practice to use the result directly too
                FirebaseUser newUser = result.User;
                Debug.LogFormat("AuthManager: Firebase User Registered successfully: {0} ({1})", newUser.Email, newUser.UserId);
                OnSignInSuccess?.Invoke(newUser);
                await GetIdTokenAndCallApi(); // Get token right after registration
            }
            catch (Exception e)
            {
                HandleAuthException(e, "Registration");
            }
            finally
            {
                IsSigningIn = false; // Ensure this always gets reset
            }
        }

        // --- Login Method ---
        public async Task LoginWithEmailPassword(string email, string password)
        {
            if (!isFirebaseReady || IsSigningIn)
            {
                OnSignInFailed?.Invoke("Not ready or already processing.");
                return;
            }
            IsSigningIn = true;
            OnSignInAttempt?.Invoke();
            Debug.Log($"AuthManager: Attempting Login for email: [{email}]");

            try
            {
                AuthResult result = await auth.SignInWithEmailAndPasswordAsync(email, password);
                // AuthStateChanged listener will update 'user'
                FirebaseUser loggedInUser = result.User;
                Debug.LogFormat("AuthManager: Firebase User Logged In successfully: {0} ({1})", loggedInUser.Email, loggedInUser.UserId);
                OnSignInSuccess?.Invoke(loggedInUser);
                await GetIdTokenAndCallApi(); // Get token after successful login
            }
            catch (Exception e)
            {
                HandleAuthException(e, "Login");
            }
            finally
            {
                IsSigningIn = false;
            }
        }

        // --- Sign Out Method ---
        public void SignOutUser()
        {
            if (auth != null && auth.CurrentUser != null)
            {
                Debug.Log("AuthManager: Signing out...");
                auth.SignOut(); // AuthStateChanged listener handles the rest
            }
            else
            {
                Debug.Log("AuthManager: Already signed out.");
                OnSignOutComplete?.Invoke(); // Ensure UI updates if already out
                hasFetchedProfileThisSession = false;
            }
        }

        // --- Get ID Token Method ---
        private async Task GetIdTokenAndCallApi()
        {
            if (user == null) // Use the class member 'user' updated by AuthStateChanged or login/reg success
            {
                Debug.LogError("AuthManager: Cannot get token, user is null.");
                OnSignInFailed?.Invoke("User session error.");
                return;
            }

            if (hasFetchedProfileThisSession) { // Add check to prevent multiple calls if triggered again
                Debug.Log("AuthManager: Profile already fetched this session, skipping redundant API call.");
                return;
            }

            Debug.Log("AuthManager: Getting Firebase ID Token...");
            try
            {
                currentIdToken = await user.TokenAsync(true); // true forces refresh
                string truncatedToken = currentIdToken.Length > 20 ? currentIdToken.Substring(0, 20) + "..." : currentIdToken;
                Debug.Log($"AuthManager: Firebase ID Token received: {truncatedToken}");
                OnTokenReceived?.Invoke(truncatedToken);

                // --- Trigger API Call ---
                CallBackendApi();
            }
            catch (Exception e)
            {
                Debug.LogError($"AuthManager: TokenAsync encountered an error: {e}");
                OnSignInFailed?.Invoke($"Failed to get token: {e.Message}");
            }
        }

        // --- Call Backend API Method (Kept separate) ---
        private void CallBackendApi()
        {
            if (apiClient != null && !string.IsNullOrEmpty(currentIdToken))
            {
                OnApiCalled?.Invoke();
                // Pass lambda expressions matching the new ApiClient signature
                StartCoroutine(apiClient.GetPlayerProfileRoutine(
                    currentIdToken,
                    // onSuccess callback (receives PlayerProfileDto)
                    (playerProfile) =>
                    {
                        string successMsg = $"Profile Fetched: {playerProfile.displayName} (ID: {playerProfile.playerId})";
                        Debug.Log($"AuthManager: {successMsg}");
                        OnApiSuccess?.Invoke(JsonConvert.SerializeObject(playerProfile, Formatting.Indented)); // Pass serialized DTO string to UI for now
                                                                                                               // TODO: Update local game state with received profile data
                    },
                    // onError callback (receives ApiResponse<object>)
                    (errorResponse) =>
                    {
                        string errorMsg = $"API Error: Code={errorResponse?.code}, Msg={errorResponse?.message}";
                        if (errorResponse != null && errorResponse.requestId != null)
                        {
                            errorMsg += $", RequestId={errorResponse.requestId}";
                        }
                        Debug.LogError($"AuthManager: {errorMsg}");
                        OnApiFailed?.Invoke(errorMsg);
                    }
                ));
            }
            else
            {
                // ... (existing error logging for missing client or token) ...
                string error = apiClient == null ? "ApiClient not configured." : "ID Token missing.";
                Debug.LogError($"AuthManager: Cannot call API. {error}");
                OnApiFailed?.Invoke($"Cannot call API: {error}");
            }
        }

        // --- Centralized Error Handling ---
        private void HandleAuthException(Exception e, string operation)
        {
            Debug.LogError($"AuthManager: {operation} failed with error: {e}");
            string detailedErrorMessage = $"{operation} Failed.";
            if (e.GetBaseException() is FirebaseException firebaseEx)
            {
                AuthError errorCode = (AuthError)firebaseEx.ErrorCode;
                detailedErrorMessage = $"{operation} Failed: {errorCode.ToString()} ({firebaseEx.Message})";
                // Log specific common errors
                if (errorCode == AuthError.WrongPassword || errorCode == AuthError.UserNotFound || errorCode == AuthError.InvalidEmail)
                {
                    Debug.LogWarning($"Common Auth Error Details: {errorCode}");
                }
                else if (errorCode == AuthError.EmailAlreadyInUse || errorCode == AuthError.WeakPassword)
                {
                    Debug.LogWarning($"Registration Error Details: {errorCode}");
                }
            }
            else
            {
                detailedErrorMessage = $"{operation} Failed: {e.Message}";
            }
            OnSignInFailed?.Invoke(detailedErrorMessage); // Notify UI
        }


        // Clean up listeners when this object is destroyed
        void OnDestroy()
        {
            FirebaseManager.OnFirebaseReady -= HandleFirebaseReady; // Unsubscribe from static event
            if (auth != null)
            {
                auth.StateChanged -= AuthStateChanged;
                auth = null;
            }
        }
    }
}