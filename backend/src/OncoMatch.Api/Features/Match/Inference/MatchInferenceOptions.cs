namespace OncoMatch.Api.Features.Match.Inference;

public sealed class MatchInferenceOptions
{
    public string Route { get; set; } = "native";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string NativeModel { get; set; } = "gpt-oss:20b";
    public string OpenAiCompatibleModel { get; set; } = "oncomatch-gpt-oss-20b-openai-compatible:latest";
    public int RequestTimeoutSeconds { get; set; } = 600;
    internal string? BindingError { get; set; }
    public string Model => Route == "native" ? NativeModel : OpenAiCompatibleModel;
    public string? ValidationError => BindingError ?? (Route is not ("native" or "openai-compatible")
        ? "Match:Inference:Route must be native or openai-compatible."
        : !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            ? "Match:Inference:BaseUrl must be a local Ollama HTTP(S) origin."
            : string.IsNullOrWhiteSpace(Model) ? "Configure the selected route's model name."
            : RequestTimeoutSeconds <= 0 || RequestTimeoutSeconds > 4_294_967 ? "RequestTimeoutSeconds must be positive and within the platform timer limit (4294967 seconds)." : null);
}
