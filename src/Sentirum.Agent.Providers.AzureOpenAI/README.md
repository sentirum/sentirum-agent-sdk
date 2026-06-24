# Sentirum.Agent.Providers.AzureOpenAI

Azure OpenAI provider for Sentirum.Agent.

## Usage

### API key authentication

```csharp
services.AddSentirumAgent("support", b => b
    .UseAzureOpenAI(
        deployment: "gpt-4o",
        endpoint: new Uri("https://my-resource.openai.azure.com"),
        apiKey: "your-api-key"));
```

### Azure AD (Entra ID) authentication

```csharp
services.AddSentirumAgent("support", b => b
    .UseAzureOpenAI(
        deployment: "gpt-4o",
        endpoint: new Uri("https://my-resource.openai.azure.com"),
        credential: new DefaultAzureCredential()));
```

### Environment variable fallback

If `apiKey` is omitted, the builder falls back to the `AZURE_OPENAI_API_KEY` environment variable.

```csharp
services.AddSentirumAgent("support", b => b
    .UseAzureOpenAI(
        deployment: "gpt-4o",
        endpoint: new Uri("https://my-resource.openai.azure.com")));
```

## Custom API version

```csharp
services.AddSentirumAgent("support", b => b
    .UseAzureOpenAI(
        deployment: "gpt-4o",
        endpoint: new Uri("https://my-resource.openai.azure.com"),
        apiKey: key,
        apiVersion: "2024-10-21"));
```
