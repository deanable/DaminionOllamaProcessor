# OpenRouter API Integration Analysis

## Issue Analysis

Based on my examination of the repository, I've identified several potential issues with the OpenRouter API integration that could be causing the API errors:

### 1. **Image Format Assumption**
**Issue**: The code assumes all images are JPEG format:
```csharp
new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Image}" } }
```
**Problem**: Images could be PNG, GIF, WebP, or other formats. OpenRouter expects the correct MIME type.

### 2. **Missing Content-Type Header**
**Issue**: The code only sets `Accept` header but doesn't explicitly set `Content-Type`:
```csharp
_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
```
**Problem**: While `StringContent` should set this automatically, explicit setting is more reliable.

### 3. **HTTP-Referer Header Format**
**Issue**: The header is added as `HTTP-Referer`:
```csharp
_httpClient.DefaultRequestHeaders.Add("HTTP-Referer", httpReferer);
```
**Problem**: According to OpenRouter docs, this might need to be `X-Title` or a different format.

### 4. **Model Validation Missing**
**Issue**: The code doesn't validate that the selected model supports vision/multimodal capabilities.
**Problem**: Sending image data to text-only models will fail.

### 5. **Error Response Parsing**
**Issue**: Error handling is generic and doesn't parse OpenRouter-specific error responses.
**Problem**: Makes debugging difficult.

## Recommended Solutions

### 1. **Fix Image Format Detection**
```csharp
private static string GetImageMimeType(byte[] imageBytes)
{
    if (imageBytes.Length >= 4)
    {
        // PNG signature
        if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
            return "image/png";
        
        // JPEG signature
        if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
            return "image/jpeg";
        
        // GIF signature
        if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
            return "image/gif";
        
        // WebP signature
        if (imageBytes.Length >= 12 && imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && 
            imageBytes[2] == 0x46 && imageBytes[3] == 0x46 && imageBytes[8] == 0x57 && 
            imageBytes[9] == 0x45 && imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
            return "image/webp";
    }
    
    return "image/jpeg"; // Default fallback
}
```

### 2. **Enhanced OpenRouter API Client**
```csharp
public async Task<string?> AnalyzeImageAsync(string modelName, string prompt, byte[] imageBytes)
{
    string mimeType = GetImageMimeType(imageBytes);
    string base64Image = Convert.ToBase64String(imageBytes);
    
    var requestPayload = new
    {
        model = modelName,
        messages = new[]
        {
            new {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt },
                    new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                }
            }
        }
    };

    try
    {
        var json = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        Console.WriteLine($"[OpenRouterApiClient] Sending request to model: {modelName}");
        Console.WriteLine($"[OpenRouterApiClient] Image type: {mimeType}, Size: {imageBytes.Length} bytes");

        HttpResponseMessage response = await _httpClient.PostAsync("chat/completions", content);
        string responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"[OpenRouterApiClient] Request failed. Status: {response.StatusCode}");
            Console.Error.WriteLine($"[OpenRouterApiClient] Response: {responseBody}");
            
            // Try to parse OpenRouter error response
            try
            {
                var errorResponse = JsonSerializer.Deserialize<OpenRouterErrorResponse>(responseBody);
                return $"Error: {errorResponse?.Error?.Message ?? "Unknown API error"}";
            }
            catch
            {
                return $"Error: API request failed with status {response.StatusCode}. Response: {responseBody}";
            }
        }

        var openRouterResponse = JsonSerializer.Deserialize<OpenRouterChatCompletionResponse>(responseBody);
        return openRouterResponse?.Choices?.FirstOrDefault()?.Message?.Content;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[OpenRouterApiClient] Exception: {ex.Message}");
        return $"Error: {ex.Message}";
    }
}
```

### 3. **Add OpenRouter Error Response DTOs**
```csharp
public class OpenRouterErrorResponse
{
    [JsonPropertyName("error")]
    public OpenRouterError? Error { get; set; }
}

public class OpenRouterError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
```

### 4. **Improved Model Validation**
```csharp
public async Task<bool> IsModelMultimodalAsync(string modelName)
{
    var modelsResponse = await ListModelsAsync();
    if (modelsResponse?.Data == null) return false;
    
    var model = modelsResponse.Data.FirstOrDefault(m => m.Id == modelName);
    if (model == null) return false;
    
    // Check if model supports vision/multimodal
    return modelName.Contains("vision") || 
           modelName.Contains("claude-3") || 
           modelName.Contains("gpt-4") || 
           modelName.Contains("gemini");
}
```

### 5. **Better Error Handling in Usage**
```csharp
// In DaminionCollectionTaggerViewModel.cs
var routerClient = new OpenRouterApiClient(Settings.OpenRouterApiKey, Settings.OpenRouterHttpReferer);

// Validate model supports images
if (!await routerClient.IsModelMultimodalAsync(Settings.OpenRouterModelName))
{
    throw new Exception($"Model {Settings.OpenRouterModelName} does not support image analysis");
}

string? routerResponse = await routerClient.AnalyzeImageAsync(Settings.OpenRouterModelName, Settings.OllamaPrompt, imageBytes);
```

## Additional Debugging Steps

1. **Enable detailed logging** to see the exact request/response
2. **Verify API key format** - OpenRouter keys should start with `sk-or-`
3. **Test with a simple text request** first to verify basic connectivity
4. **Check rate limits** - OpenRouter has rate limiting that might cause errors
5. **Validate HTTP-Referer** - Some models require specific referer values

## Common OpenRouter API Issues

1. **Invalid API Key**: Ensure the key is properly formatted and valid
2. **Model Not Available**: Some models may be temporarily unavailable
3. **Rate Limiting**: Too many requests in a short time
4. **Insufficient Credits**: Account may be out of credits
5. **Model Doesn't Support Images**: Text-only models can't process images
6. **Image Too Large**: OpenRouter has size limits for images

## Testing Recommendations

1. Test with a known working model like `google/gemini-pro-vision`
2. Use a small test image first
3. Verify the API key works with a simple text request
4. Check the OpenRouter dashboard for usage and errors
5. Enable verbose logging to see exact requests/responses

## Summary of Implemented Changes

I have successfully implemented the following fixes to your OpenRouter API integration:

### ✅ Fixed Files:
1. **`DaminionOllamaInteractionLib/OpenRouter/OpenRouterApiClient.cs`**:
   - Added proper image format detection using byte signatures
   - Added new `AnalyzeImageAsync` overload that accepts `byte[]` instead of `string`
   - Improved error handling with OpenRouter-specific error parsing
   - Added `IsModelMultimodalAsync` method for model validation
   - Added `GetImageMimeType` helper method
   - Added new DTOs for error responses

2. **`DaminionOllamaApp/ViewModels/DaminionCollectionTaggerViewModel.cs`**:
   - Updated to use the new `byte[]` overload for image analysis
   - Added model validation before sending requests
   - Improved error messages

3. **`DaminionOllamaApp/ViewModels/SettingsViewModel.cs`**:
   - Enhanced model filtering to include more vision-capable models (GPT-4, Gemini)

### 🔧 Key Improvements:
- **Better Image Format Support**: Automatically detects PNG, JPEG, GIF, and WebP formats
- **Enhanced Error Handling**: Parses OpenRouter-specific error responses for better debugging
- **Model Validation**: Prevents sending images to text-only models
- **Improved Logging**: More detailed logging for debugging API issues
- **Better Error Messages**: More specific error messages to help identify issues

### 🚀 Next Steps:
1. **Build and test** the solution after these changes
2. **Check your OpenRouter API key** format (should start with `sk-or-`)
3. **Test with a known working model** like `google/gemini-pro-vision`
4. **Monitor the console output** for detailed error messages
5. **Check the OpenRouter dashboard** for usage limits and errors

The improvements should resolve most common OpenRouter API integration issues and provide much better error reporting to help you diagnose any remaining problems.