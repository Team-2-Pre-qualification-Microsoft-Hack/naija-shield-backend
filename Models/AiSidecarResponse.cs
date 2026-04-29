namespace naija_shield_backend.Models;

/// <summary>
/// Response returned by the Python FastAPI sidecar's POST /analyze-voice endpoint.
/// The sidecar performs speech-to-text transcription and deepfake probability scoring.
/// </summary>
public class AiSidecarResponse
{
    /// <summary>Speech-to-text transcript of the audio segment.</summary>
    public string Transcript { get; set; } = string.Empty;

    /// <summary>
    /// Probability that the audio was AI-synthesised (deepfake), range 0.0–1.0.
    /// 0.0 = definitely human; 1.0 = definitely synthetic.
    /// </summary>
    public double DeepfakeScore { get; set; }

    /// <summary>BCP-47 language tag or descriptive label, e.g. "en", "pidgin", "yo".</summary>
    public string LanguageDetected { get; set; } = "en";
}
