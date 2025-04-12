using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using Translator.Protocol;
using Translator.Utils;
using Wox.Plugin.Logger;

namespace Translator.Service.Google
{
    public static class DictionaryExtensions
    {
        public static string ToQueryString(this Dictionary<string, string> dict)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (var kvp in dict)
            {
                query[kvp.Key] = kvp.Value;
            }
            return "?" + query.ToString();
        }
    }


// Represents the response from Google Translate API v2
public class GoogleTranslateResponse : ITranslateResult
{
    public class Data
    {
        public class Translation
        {
            public string? translatedText { get; set; }
            public string? detectedSourceLanguage { get; set; }
        }
        public Translation[]? translations { get; set; }
    }

    public Data? data { get; set; }

    // Placeholder for error handling if needed
    // public Error? error { get; set; }

    public override IEnumerable<ResultItem>? Transform()
    {
        if (data?.translations == null || data.translations.Length == 0)
        {
            // Log error or handle no translation case
            Log.Warn("Google Translate API returned no translations.", typeof(GoogleTranslateResponse));
            return null;
        }

        var results = new List<ResultItem>();
        foreach (var translation in data.translations)
        {
            if (translation.translatedText != null)
            {
                results.Add(new ResultItem
                {
                    Title = translation.translatedText,
                    SubTitle = $"Detected source language: {translation.detectedSourceLanguage ?? "unknown"}", // Provide subtitle if needed
                    transType = $"Google Translate ({(translation.detectedSourceLanguage ?? "auto")}->target)", // Indicate source language if detected
                    CopyTgt = translation.translatedText,
                    Description = $"Translated text: {translation.translatedText}", // Add more details if necessary
                    fromApiName = "Google Translate API"
                });
            }
        }
        return results.Count > 0 ? results : null;
    }
}


public class GoogleTranslator : ITranslator
{
    private HttpClient client;
    private readonly SettingHelper settingHelper;
    private string ApiKey => settingHelper.googleApiKey;
    private const string ApiEndpoint = "https://translation.googleapis.com/language/translate/v2";

    public GoogleTranslator(SettingHelper settingHelper)
    {
        this.settingHelper = settingHelper;
        client = new HttpClient(UtilsFun.httpClientDefaultHandler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public override ITranslateResult? Translate(string src, string toLan = "auto", string fromLan = "auto")
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            Log.Error("Google API Key is not configured in settings.", typeof(GoogleTranslator));
            return null;
        }

        try
        {
            // Construct the request URL
            var queryParams = new Dictionary<string, string>
            {
                { "key", ApiKey },
                { "q", src },
                { "target", toLan }
            };
            if (fromLan != "auto")
            {
                queryParams.Add("source", fromLan);
            }

            string requestUri = ApiEndpoint + queryParams.ToQueryString();

            // Send GET request asynchronously
            HttpResponseMessage response = client.GetAsync(requestUri).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode(); // Throw exception if not successful

            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            // Deserialize the response
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // Handle potential case differences in JSON response
            };
            GoogleTranslateResponse? result = JsonSerializer.Deserialize<GoogleTranslateResponse>(responseBody, options);

            // Add original query info to the result for context in Transform() if needed
            // result?.originalQuery = src;
            // result?.targetLanguage = toLan;

            return result;
        }
        catch (HttpRequestException e)
        {
            Log.Error($"Error calling Google Translate API: {e.Message}", typeof(GoogleTranslator));
            // Consider returning a specific error object or null
            return null;
        }
        catch (JsonException e)
        {
            Log.Error($"Error deserializing Google Translate API response: {e.Message}", typeof(GoogleTranslator));
            return null;
        }
         catch (Exception e) // Catch other potential errors
        {
            Log.Error($"An unexpected error occurred during Google translation: {e.Message}", typeof(GoogleTranslator));
            return null;
        }
    }

    public override void Reset()
    {
        // No need to explicitly reload the API key as it's accessed via property
        Log.Info("GoogleTranslator Reset called.", typeof(GoogleTranslator));
    }
}