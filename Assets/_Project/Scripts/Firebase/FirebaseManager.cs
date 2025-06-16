// Assets/_Project/Scripts/Firebase/FirebaseManager.cs
using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions; // For ContinueWithOnMainThread
using UnityEngine;

namespace DeepSimGames.UnchartedReach.Core
{
    public class FirebaseManager : MonoBehaviour
    {
        public static FirebaseManager Instance { get; private set; }

        public bool IsFirebaseInitialized { get; private set; } = false;
        public static event Action OnFirebaseReady; // Event to signal readiness

        public FirebaseAuth AuthInstance { get; private set; } // Provide access to the Auth instance
        public FirebaseUser CurrentUser => AuthInstance?.CurrentUser; // Convenience getter

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeFirebase();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeFirebase()
        {
            Debug.Log("FirebaseManager: Checking Firebase Dependencies...");
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    FirebaseApp app = FirebaseApp.DefaultInstance;
                    AuthInstance = FirebaseAuth.DefaultInstance; // Initialize Auth here
                    IsFirebaseInitialized = true;
                    Debug.Log("FirebaseManager: Firebase Core and Auth Initialized Successfully.");
                    OnFirebaseReady?.Invoke(); // Fire the readiness event
                }
                else
                {
                    IsFirebaseInitialized = false;
                    AuthInstance = null;
                    Debug.LogError($"FirebaseManager: Could not resolve all Firebase dependencies: {dependencyStatus}");
                    // Maybe fire a different event for failure?
                }
            });
        }
    }
}