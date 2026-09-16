#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Convai.RestAPI.Transport;
using Newtonsoft.Json;

namespace Convai.RestAPI
{
    /// <summary>
    /// Internal base class for all service implementations.
    /// Provides common functionality for making API requests.
    /// </summary>
    public abstract class ConvaiServiceBase
    {
        private static readonly JsonSerializerSettings DefaultJsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        protected readonly ConvaiRestClientOptions Options;
        protected readonly IConvaiHttpTransport Transport;

        protected ConvaiServiceBase(ConvaiRestClientOptions options, IConvaiHttpTransport transport)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        /// <summary>
        /// Builds the full URL for an endpoint.
        /// </summary>
        protected Uri BuildUrl(string endpoint, bool useBeta = false)
        {
            string baseUrl = useBeta ? Options.BetaBaseUrl : Options.GetBaseUrl();
            // Ensure no double slashes
            if (baseUrl.EndsWith("/") && endpoint.StartsWith("/"))
                endpoint = endpoint.Substring(1);
            else if (!baseUrl.EndsWith("/") && !endpoint.StartsWith("/"))
                baseUrl += "/";

            return new Uri(baseUrl + endpoint);
        }

        /// <summary>
        /// Makes a POST request with JSON body and returns the deserialized response.
        /// </summary>
        protected async Task<T> PostAsync<T>(
            string endpoint,
            object? requestBody,
            bool useBeta = false,
            Dictionary<string, string>? additionalHeaders = null,
            CancellationToken cancellationToken = default)
        {
            Uri url = BuildUrl(endpoint, useBeta);
            ConvaiHttpRequest.Builder builder = ConvaiHttpRequest.CreateBuilder(url, HttpMethod.Post)
                .WithApiKey(Options.ApiKey);

            if (requestBody != null)
            {
                string json = JsonConvert.SerializeObject(requestBody, DefaultJsonSettings);
                builder.WithBody(json);
            }

            if (additionalHeaders != null)
            {
                builder.WithHeaders(additionalHeaders);
            }

            ConvaiHttpRequest request = builder.Build();
            ConvaiHttpResponse response = await Transport.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return ProcessResponse<T>(response);
        }

        /// <summary>
        /// Makes a POST request with JSON body to a custom URL.
        /// </summary>
        protected async Task<T> PostToUrlAsync<T>(
            string url,
            object? requestBody,
            bool useXApiKey = false,
            Dictionary<string, string>? additionalHeaders = null,
            CancellationToken cancellationToken = default)
        {
            ConvaiHttpRequest.Builder builder = ConvaiHttpRequest.CreateBuilder(url, HttpMethod.Post);

            if (useXApiKey)
                builder.WithXApiKey(Options.ApiKey);
            else
                builder.WithApiKey(Options.ApiKey);

            if (requestBody != null)
            {
                string json = JsonConvert.SerializeObject(requestBody, DefaultJsonSettings);
                builder.WithBody(json);
            }

            if (additionalHeaders != null)
            {
                builder.WithHeaders(additionalHeaders);
            }

            ConvaiHttpRequest request = builder.Build();
            ConvaiHttpResponse response = await Transport.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return ProcessResponse<T>(response);
        }

        /// <summary>
        /// Makes a POST request and returns just the success status (for void-returning operations).
        /// </summary>
        protected async Task PostVoidAsync(
            string endpoint,
            object? requestBody,
            bool useBeta = false,
            Dictionary<string, string>? additionalHeaders = null,
            CancellationToken cancellationToken = default)
        {
            Uri url = BuildUrl(endpoint, useBeta);
            ConvaiHttpRequest.Builder builder = ConvaiHttpRequest.CreateBuilder(url, HttpMethod.Post)
                .WithApiKey(Options.ApiKey);

            if (requestBody != null)
            {
                string json = JsonConvert.SerializeObject(requestBody, DefaultJsonSettings);
                builder.WithBody(json);
            }

            if (additionalHeaders != null)
            {
                builder.WithHeaders(additionalHeaders);
            }

            ConvaiHttpRequest request = builder.Build();
            ConvaiHttpResponse response = await Transport.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccess)
            {
                throw ConvaiRestException.FromResponse(response);
            }
        }

        /// <summary>
        /// Processes an HTTP response and deserializes the body.
        /// </summary>
        private static T ProcessResponse<T>(ConvaiHttpResponse response)
        {
            if (!response.IsSuccess)
            {
                throw ConvaiRestException.FromResponse(response);
            }

            try
            {
                T? result = JsonConvert.DeserializeObject<T>(response.Body, DefaultJsonSettings);
                if (result == null)
                {
                    throw ConvaiRestException.ParseError(
                        $"Failed to deserialize response to {typeof(T).Name}: result was null",
                        response.Url,
                        response.GetTruncatedBody());
                }
                return result;
            }
            catch (JsonException ex)
            {
                throw ConvaiRestException.ParseError(
                    $"Failed to deserialize response to {typeof(T).Name}: {ex.Message}",
                    response.Url,
                    response.GetTruncatedBody(),
                    ex);
            }
        }

        /// <summary>
        /// Tries to deserialize JSON, returning false on failure instead of throwing.
        /// </summary>
        protected static bool TryDeserialize<T>(string json, out T? result, out string? error)
        {
            try
            {
                result = JsonConvert.DeserializeObject<T>(json, DefaultJsonSettings);
                if (result == null)
                {
                    error = "Deserialization returned null";
                    return false;
                }
                error = null;
                return true;
            }
            catch (JsonException ex)
            {
                result = default;
                error = ex.Message;
                return false;
            }
        }
    }
}
