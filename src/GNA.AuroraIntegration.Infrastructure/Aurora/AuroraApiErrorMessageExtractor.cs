using System.Net;
using System.Text.Json;

namespace GNA.AuroraIntegration.Infrastructure.Aurora;

internal static class AuroraApiErrorMessageExtractor
{
    private const string DisplayMessageProperty = "displayMessage";
    private const string DetailsProperty = "details";
    private const string ErrorMessageProperty = "errorMessage";

    public static string GetErrorMessageOrStatusCode(string? responseContent, HttpStatusCode statusCode)
    {
        string? auroraMessage = TryExtractErrorMessage(responseContent);
        return string.IsNullOrWhiteSpace(auroraMessage)
            ? statusCode.ToString()
            : auroraMessage;
    }

    private static string? TryExtractErrorMessage(string? responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseContent);
            JsonElement root = document.RootElement;

            if (TryGetNonEmptyStringProperty(root, DisplayMessageProperty, out string? topLevelMessage))
            {
                string? nestedMessage = TryExtractNestedAuroraMessage(topLevelMessage!);
                if (!string.IsNullOrWhiteSpace(nestedMessage))
                {
                    return nestedMessage;
                }

                return topLevelMessage;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? TryExtractNestedAuroraMessage(string topLevelMessage)
    {
        int nestedPayloadStart = topLevelMessage.LastIndexOf('[');
        if (nestedPayloadStart < 0)
        {
            return null;
        }

        string nestedPayload = topLevelMessage[nestedPayloadStart..];

        try
        {
            using JsonDocument nestedDocument = JsonDocument.Parse(nestedPayload);
            JsonElement nestedRoot = nestedDocument.RootElement;

            if (nestedRoot.ValueKind != JsonValueKind.Array || nestedRoot.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement firstError = nestedRoot[0];

            if (TryGetNonEmptyStringProperty(firstError, DisplayMessageProperty, out string? displayMessage))
            {
                return displayMessage;
            }

            if (firstError.TryGetProperty(DetailsProperty, out JsonElement details)
                && TryGetNonEmptyStringProperty(details, ErrorMessageProperty, out string? errorMessage))
            {
                return errorMessage;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool TryGetNonEmptyStringProperty(JsonElement element, string propertyName, out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? rawValue = property.GetString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        value = rawValue;
        return true;
    }
}
