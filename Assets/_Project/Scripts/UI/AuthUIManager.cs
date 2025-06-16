using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Firebase.Auth;
using DeepSimGames.UnchartedReach.Auth;
using DeepSimGames.UnchartedReach.Core;

namespace DeepSimGames.UnchartedReach.UI
{
    public class AuthUIManager : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button forgotPasswordButton;
        [SerializeField] private TMP_InputField changeEmailInput;
        [SerializeField] private Button changeEmailButton; // NEW
        [SerializeField] private TMP_Text feedbackText;

        [Header("Dependencies")]
        [SerializeField] private AuthManager authManager;

        private bool isFirebaseReady = false;

        void Start()
        {
            if (authManager == null || emailInput == null || passwordInput == null ||
                registerButton == null || loginButton == null || feedbackText == null || changeEmailInput == null || changeEmailButton == null)
            {
                Debug.LogError("AuthUIManager Dependencies not set correctly in Inspector!");
                UpdateFeedback("Error: UI Manager not setup correctly.");
                return;
            }

            registerButton.onClick.AddListener(HandleRegisterClicked);
            loginButton.onClick.AddListener(HandleLoginClicked);
            logoutButton?.onClick.AddListener(HandleLogoutClicked);
            forgotPasswordButton?.onClick.AddListener(HandleForgotPasswordClicked);
            changeEmailButton.onClick.AddListener(HandleChangeEmailClicked); // NEW

            SubscribeToAuthEvents();

            changeEmailInput.gameObject.SetActive(false);
            changeEmailButton.gameObject.SetActive(false);
            UpdateFeedback("Initializing Firebase...");
            SetButtonsInteractable(false);
        }

        void SubscribeToAuthEvents()
        {
            FirebaseManager.OnFirebaseReady += HandleFirebaseReady;
            authManager.OnSignInAttempt += HandleSignInAttempt;
            authManager.OnSignInSuccess += HandleSignInSuccess;
            authManager.OnSignInFailed += HandleSignInFailed;
            authManager.OnTokenReceived += HandleTokenReceived;
            authManager.OnSignOutComplete += HandleSignOutComplete;
            authManager.OnApiCalled += HandleApiCalled;
            authManager.OnApiSuccess += HandleApiSuccess;
            authManager.OnApiFailed += HandleApiFailed;
        }

        void HandleFirebaseReady()
        {
            isFirebaseReady = true;
            UpdateFeedback("Firebase Ready. Enter Email/Password.");
            SetButtonsInteractable(true);
        }

        public void HandleRegisterClicked()
        {
            string email = emailInput.text.Trim();
            string password = passwordInput.text;
            if (!ValidateInput(email, password)) return;

            authManager.RegisterWithEmailPassword(email, password);

            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user != null && !user.IsEmailVerified)
            {
                user.SendEmailVerificationAsync().ContinueWith(task =>
                {
                    if (!task.IsFaulted && !task.IsCanceled)
                        UpdateFeedback("Verification email sent. Please check your inbox.");
                    else
                        UpdateFeedback("Failed to send verification email.");
                });
            }
        }

        public void HandleLoginClicked()
        {
            string email = emailInput.text.Trim();
            string password = passwordInput.text;
            if (!ValidateInput(email, password)) return;

            authManager.LoginWithEmailPassword(email, password);
        }

        void HandleLogoutClicked()
        {
            if (!isFirebaseReady) return;
            authManager.SignOutUser();
        }

        void HandleForgotPasswordClicked()
        {
            string email = emailInput.text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                UpdateFeedback("Enter your email to reset password.");
                return;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                UpdateFeedback("Enter a valid email address.");
                return;
            }

            FirebaseAuth.DefaultInstance.SendPasswordResetEmailAsync(email).ContinueWith(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                    UpdateFeedback("Failed to send reset email.");
                else
                    UpdateFeedback("Password reset email sent.");
            });
        }

        void HandleChangeEmailClicked()
{
    FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
    if (user == null)
    {
        UpdateFeedback("No user logged in.");
        return;
    }

    string newEmail = changeEmailInput.text.Trim();
    string currentEmail = user.Email;
    string currentPassword = passwordInput.text;

    if (string.IsNullOrWhiteSpace(newEmail) || !newEmail.Contains("@") || !newEmail.Contains("."))
    {
        UpdateFeedback("Enter a valid new email.");
        return;
    }

    Credential credential = EmailAuthProvider.GetCredential(currentEmail, currentPassword);

    user.ReauthenticateAsync(credential).ContinueWith(reAuthTask =>
    {
        if (reAuthTask.IsFaulted || reAuthTask.IsCanceled)
        {
            Debug.LogError($"Reauth Error: {reAuthTask.Exception?.GetBaseException().Message}");
            UpdateFeedback("Reauthentication failed. Password may be incorrect.");
            return;
        }

        user.UpdateEmailAsync(newEmail).ContinueWith(updateTask =>
        {
            if (updateTask.IsFaulted || updateTask.IsCanceled)
            {
                Debug.LogError($"UpdateEmail Error: {updateTask.Exception?.GetBaseException().Message}");
                UpdateFeedback("Failed to update email. It may already be in use.");
            }
            else
            {
                user.SendEmailVerificationAsync();
                UpdateFeedback("Email updated successfully. Verification email sent.");
            }
        });
    });
}

        void HandleSignInAttempt()
        {
            UpdateFeedback("Processing...");
            SetButtonsInteractable(false);
        }

        void HandleSignInSuccess(FirebaseUser user)
        {
            if (user.IsEmailVerified)
            {
                UpdateFeedback($"Signed In: {user.Email}");
                changeEmailInput.gameObject.SetActive(false);
                changeEmailButton.gameObject.SetActive(false);
            }
            else
            {
                UpdateFeedback("Signed In (Unverified). Please verify your email.");
                changeEmailInput.gameObject.SetActive(true);
                changeEmailButton.gameObject.SetActive(true);
            }

            SetButtonsInteractable(true);
        }

        void HandleSignInFailed(string errorMessage)
        {
            UpdateFeedback($"Error: {errorMessage}");
            SetButtonsInteractable(true);
        }

        void HandleTokenReceived(string truncatedToken)
        {
            UpdateFeedback($"Token Received! ({truncatedToken}) Calling API...");
        }

        void HandleSignOutComplete()
        {
            UpdateFeedback("Signed Out. Enter Email/Password.");
            ClearInputFields();
            SetButtonsInteractable(true);
            changeEmailInput.gameObject.SetActive(false);
            changeEmailButton.gameObject.SetActive(false);
        }

        void HandleApiCalled() => UpdateFeedback("Calling Backend API...");

        void HandleApiSuccess(string response) => UpdateFeedback($"API Success:\n{response}");

        void HandleApiFailed(string error) => UpdateFeedback($"API Error: {error}");

        bool ValidateInput(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                UpdateFeedback("Please enter both email and password.");
                return false;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                UpdateFeedback("Please enter a valid email address.");
                return false;
            }

            if (password.Length < 6)
            {
                UpdateFeedback("Password must be at least 6 characters.");
                return false;
            }
            return true;
        }

        void SetButtonsInteractable(bool interactable)
        {
            bool allowLoginRegister = interactable && isFirebaseReady && (FirebaseManager.Instance?.CurrentUser == null);
            registerButton.interactable = allowLoginRegister;
            loginButton.interactable = allowLoginRegister;

            logoutButton.interactable = interactable && isFirebaseReady && (FirebaseManager.Instance?.CurrentUser != null);
            forgotPasswordButton.interactable = interactable && isFirebaseReady;
            changeEmailButton.interactable = interactable && isFirebaseReady;
        }

        void UpdateFeedback(string message)
        {
            if (feedbackText != null)
            {
                feedbackText.text = message;
                Debug.Log($"UI Feedback: {message}");
            }
        }

        void ClearInputFields()
        {
            emailInput.text = "";
            passwordInput.text = "";
        }

        void OnDestroy()
        {
            registerButton?.onClick.RemoveListener(HandleRegisterClicked);
            loginButton?.onClick.RemoveListener(HandleLoginClicked);
            logoutButton?.onClick.RemoveListener(HandleLogoutClicked);
            forgotPasswordButton?.onClick.RemoveListener(HandleForgotPasswordClicked);
            changeEmailButton?.onClick.RemoveListener(HandleChangeEmailClicked);

            FirebaseManager.OnFirebaseReady -= HandleFirebaseReady;
            if (authManager != null)
            {
                authManager.OnSignInAttempt -= HandleSignInAttempt;
                authManager.OnSignInSuccess -= HandleSignInSuccess;
                authManager.OnSignInFailed -= HandleSignInFailed;
                authManager.OnTokenReceived -= HandleTokenReceived;
                authManager.OnSignOutComplete -= HandleSignOutComplete;
                authManager.OnApiCalled -= HandleApiCalled;
                authManager.OnApiSuccess -= HandleApiSuccess;
                authManager.OnApiFailed -= HandleApiFailed;
            }
        }
    }
}