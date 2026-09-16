using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Convai.Domain.Logging;
using Convai.RestAPI;
using Convai.Runtime;
using Convai.Runtime.Logging;
using UnityEditor;

namespace Convai.Editor.Settings.Services
{
    /// <summary>Outcome of an API key validation attempt.</summary>
    public readonly struct ApiKeyValidationResult
    {
        public ApiKeyValidationResult(bool isValid, string message, bool isDefinitive = true)
        {
            IsValid = isValid;
            Message = message ?? string.Empty;
            IsDefinitive = isDefinitive;
        }

        public bool IsValid { get; }
        public string Message { get; }

        /// <summary>True when the backend conclusively accepted or rejected the credentials.</summary>
        public bool IsDefinitive { get; }
    }

    /// <summary>
    ///     Validates API keys against the Convai backend for the settings UI.
    ///     Results are cached per key/environment hash in <see cref="SessionState" /> so the
    ///     badge survives domain reloads without ever persisting the key itself.
    /// </summary>
    public sealed class ApiKeyValidationService
    {
        private const string CachePrefix = "Convai.Settings.ApiKeyValidation.";
        private int _requestVersion;

        /// <summary>True while a validation request is in flight.</summary>
        public bool IsValidating { get; private set; }

        /// <summary>
        ///     Validates the given (possibly unsaved) credentials. The callback runs on the
        ///     editor main thread; stale responses from superseded requests are dropped.
        /// </summary>
        public void Validate(string apiKey, ConvaiApiEnvironment environment, string customRestBaseUrl,
            Action<ApiKeyValidationResult> onCompleted)
        {
            int version = ++_requestVersion;
            IsValidating = true;
            _ = ValidateAsync(apiKey, environment, customRestBaseUrl, version, onCompleted);
        }

        /// <summary>Drops any in-flight request without invoking its callback.</summary>
        public void CancelPending()
        {
            _requestVersion++;
            IsValidating = false;
        }

        /// <summary>Looks up a cached validation result for the given credentials.</summary>
        public static bool TryGetCachedResult(string apiKey, ConvaiApiEnvironment environment,
            string customRestBaseUrl, out ApiKeyValidationResult result)
        {
            result = default;
            if (string.IsNullOrEmpty(apiKey)) return false;

            string cached = SessionState.GetString(CacheKey(apiKey, environment, customRestBaseUrl), string.Empty);
            if (string.IsNullOrEmpty(cached)) return false;

            if (cached.StartsWith("valid:", StringComparison.Ordinal))
            {
                result = new ApiKeyValidationResult(true, cached.Substring("valid:".Length));
                return true;
            }

            if (cached.StartsWith("invalid:", StringComparison.Ordinal))
            {
                result = new ApiKeyValidationResult(false, cached.Substring("invalid:".Length));
                return true;
            }

            return false;
        }

        private static void StoreResult(string apiKey, ConvaiApiEnvironment environment, string customRestBaseUrl,
            ApiKeyValidationResult result)
        {
            if (string.IsNullOrEmpty(apiKey)) return;

            string value = (result.IsValid ? "valid:" : "invalid:") + result.Message;
            SessionState.SetString(CacheKey(apiKey, environment, customRestBaseUrl), value);
        }

        private static string CacheKey(string apiKey, ConvaiApiEnvironment environment, string customRestBaseUrl)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{apiKey}|{environment}|{customRestBaseUrl}"));
            return CachePrefix + Convert.ToBase64String(hash);
        }

        private async Task ValidateAsync(string apiKey, ConvaiApiEnvironment environment, string customRestBaseUrl,
            int version, Action<ApiKeyValidationResult> onCompleted)
        {
            ApiKeyValidationResult result;
            try
            {
                ConvaiRestClientOptions options =
                    ConvaiRestOptionsFactory.Create(apiKey, environment, customRestBaseUrl);
                using var client = new ConvaiRestClient(options);
                // ValidateApiKeyAsync throws ConvaiRestException for non-2xx responses (e.g., invalid API key).
                await client.Users.ValidateApiKeyAsync();
                result = new ApiKeyValidationResult(true, string.Empty);
            }
            catch (ConvaiRestException ex)
            {
                ConvaiLogger.Warning(
                    $"[ApiKeyValidation] API key validation failed ({ex.Category}, HTTP {ex.StatusCodeInt}): {ex.Message}",
                    LogCategory.Editor);
                result = new ApiKeyValidationResult(
                    false,
                    ex.GetUserFriendlyMessage(),
                    ex.Category == ConvaiRestErrorCategory.Authentication);
            }
            catch (Exception ex)
            {
                ConvaiLogger.Error($"[ApiKeyValidation] Unexpected error during validation: {ex}",
                    LogCategory.Editor);
                result = new ApiKeyValidationResult(false,
                    "Something went wrong. Please check your API key and network connection.", false);
            }

            EditorApplication.delayCall += () =>
            {
                if (version != _requestVersion) return;

                IsValidating = false;
                if (result.IsDefinitive)
                    StoreResult(apiKey, environment, customRestBaseUrl, result);
                try
                {
                    onCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    ConvaiLogger.Error($"[ApiKeyValidation] Completion callback threw: {ex}", LogCategory.Editor);
                }
            };
        }
    }
}
