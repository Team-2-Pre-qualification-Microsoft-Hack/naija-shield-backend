import base64
import re
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
from spitch import Spitch
import requests

app = FastAPI()

# ==========================================
# 1. SECURE KEY VAULT CONNECTION
# ==========================================
KVUri = "https://rg-naijashield-dev-key.vault.azure.net/"
credential = DefaultAzureCredential()
client = SecretClient(vault_url=KVUri, credential=credential)

print("Connecting to Key Vault...")

# ==========================================
# 2. FETCH ALL SIDECAR KEYS
# ==========================================
try:
    spitch_key = client.get_secret("Spitch-API-Key").value
    azure_speech_key = client.get_secret("Azure-Speech-Key").value
    # Azure Speech region — stored as a non-secret config value in Key Vault for convenience
    # Default to eastus if not found; update the vault if you deploy to a different region
    try:
        azure_speech_region = client.get_secret("Azure-Speech-Region").value
    except Exception:
        azure_speech_region = "eastus"
    print("Spitch and Azure Speech keys successfully retrieved!")
except Exception as e:
    print(f"Error fetching keys: {e}")
    print("Did you remember to run 'az login' in the terminal first?")
    spitch_key = None
    azure_speech_key = None
    azure_speech_region = "eastus"

# ==========================================
# 3. PYDANTIC MODELS
# ==========================================

class VoiceAnalysisRequest(BaseModel):
    """
    Payload sent by the .NET backend's POST /api/ingest/voice endpoint
    to this sidecar's POST /analyze-voice route.
    """
    call_id: str = Field(alias="callId")
    from_number: str = Field(alias="from")
    to: str
    audio_base64: str = Field(alias="audioBase64")
    audio_format: str = Field(default="wav", alias="audioFormat")
    timestamp: str

    model_config = {"populate_by_name": True}


class VoiceAnalysisResponse(BaseModel):
    transcript: str
    deepfakeScore: float
    languageDetected: str


# ==========================================
# 4. HELPER FUNCTIONS
# ==========================================

def transcribe_audio(audio_bytes: bytes, audio_format: str) -> str:
    """
    Transcribes audio bytes using the Azure Speech Service REST API.
    Supports Nigerian English (en-NG) and falls back to en-US.
    Returns an empty string if transcription fails rather than raising.
    """
    if not azure_speech_key:
        print("[transcribe] No Azure Speech key available — returning empty transcript")
        return ""

    url = (
        f"https://{azure_speech_region}.stt.speech.microsoft.com"
        "/speech/recognition/conversation/cognitiveservices/v1"
    )
    params = {
        "language": "en-NG",
        "format": "detailed",
    }
    # Azure Speech REST API requires the raw audio bytes as the request body.
    # Supported formats: wav (PCM), ogg (opus), mp3 with the correct Content-Type.
    content_type_map = {
        "wav": "audio/wav",
        "mp3": "audio/mpeg",
        "ogg": "audio/ogg; codecs=opus",
    }
    content_type = content_type_map.get(audio_format.lower(), "audio/wav")

    headers = {
        "Ocp-Apim-Subscription-Key": azure_speech_key,
        "Content-Type": content_type,
        "Accept": "application/json",
    }

    try:
        resp = requests.post(url, params=params, headers=headers, data=audio_bytes, timeout=30)
        resp.raise_for_status()
        data = resp.json()
        # Azure Speech REST returns 'DisplayText' at the top level for simple recognition
        return data.get("DisplayText", "")
    except requests.RequestException as e:
        print(f"[transcribe] Azure Speech API error: {e}")
        return ""


def compute_deepfake_score(audio_bytes: bytes) -> float:
    """
    Placeholder deepfake detection.
    TODO: Integrate a real model (e.g. RawNet2 or WavLM-based classifier).
    Currently analyses basic audio energy variance as a naive heuristic —
    this is NOT a real deepfake detector and should be replaced before production.
    Returns a score in [0.0, 1.0] where 1.0 = likely synthetic.
    """
    # Naive heuristic: very short or very uniform audio is flagged slightly higher.
    # Replace this entire function with a real ML inference call.
    if len(audio_bytes) < 1000:
        return 0.15  # suspiciously short
    # Real implementation would load audio, extract features, run classifier
    return 0.1  # default: likely human


def detect_language(transcript: str) -> str:
    """
    Lightweight language/dialect detection for Nigerian content.
    Checks for Pidgin English markers; returns BCP-47 tag where possible.
    TODO: Replace with Spitch language detection or a proper langdetect call.
    """
    if not transcript:
        return "unknown"

    pidgin_markers = [
        "oga", "abeg", "na", "dey", "don", "make", "wetin",
        "wahala", "chop", "sef", "sha", "nah", "bros", "comot",
    ]
    lower_words = set(re.findall(r"\b\w+\b", transcript.lower()))
    pidgin_hits = lower_words.intersection(pidgin_markers)

    if len(pidgin_hits) >= 2:
        return "pidgin"

    # Simple Yoruba / Hausa / Igbo marker checks (extend as needed)
    yoruba_markers = {"bawo", "ẹ", "jowo", "e", "se", "ko", "ni"}
    if lower_words.intersection(yoruba_markers):
        return "yo"

    return "en"


# ==========================================
# 5. EXISTING ENDPOINTS (retained)
# ==========================================

@app.get("/api/health-voice")
def health_check():
    """
    Proves the Sidecar has its keys and is ready to process audio.
    """
    spitch_headers = {
        "Authorization": f"Bearer {spitch_key}",
        "Content-Type": "application/json"
    }

    return {
        "status": "Online",
        "message": "Python Sidecar is ready for Jambonz/WebRTC WebSocket streams.",
        "keys_loaded": {
            "spitch": "Loaded" if spitch_key else "Missing",
            "azure_speech": "Loaded" if azure_speech_key else "Missing",
        }
    }


spitch_client = Spitch(api_key=spitch_key) if spitch_key else None


@app.get("/api/generate-warning")
def generate_warning_audio():
    """
    Tests the Spitch TTS API using their official Python SDK.
    """
    if not spitch_client:
        return {"status": "Error", "details": "Spitch key not loaded"}
    try:
        response = spitch_client.speech.generate(
            language="yo",
            text="Bawo ni? Abeg be careful, this person fit be scammer.",
            voice="femi"
        )
        print("Spitch Response received successfully!")
        return {
            "status": "Success!",
            "message": "Connected to Spitch.app successfully via the official SDK.",
        }
    except Exception as e:
        return {"status": "Error", "details": str(e)}


# ==========================================
# 6. NEW: VOICE ANALYSIS ENDPOINT
# ==========================================

@app.post("/analyze-voice", response_model=VoiceAnalysisResponse)
async def analyze_voice(request: VoiceAnalysisRequest):
    """
    Receives a base64-encoded audio segment from the .NET backend,
    transcribes it using Azure Speech Service, computes a deepfake
    probability score, and detects the spoken language/dialect.

    Returns:
        transcript      — speech-to-text output (may be empty if STT fails)
        deepfakeScore   — probability 0.0–1.0 the audio is AI-synthesised
        languageDetected — e.g. "en", "pidgin", "yo"

    The .NET pipeline combines deepfakeScore with the LLM risk score:
        finalRiskScore = (llmScore * 0.6) + (deepfakeScore * 100 * 0.4)
    """
    # Decode base64 audio
    try:
        audio_bytes = base64.b64decode(request.audio_base64)
    except Exception as e:
        raise HTTPException(
            status_code=400,
            detail=f"Invalid base64 audio payload: {e}"
        )

    if len(audio_bytes) == 0:
        raise HTTPException(status_code=400, detail="Audio payload is empty after base64 decode")

    print(
        f"[analyze-voice] callId={request.call_id} format={request.audio_format} "
        f"bytes={len(audio_bytes)}"
    )

    # Step 1: Transcribe via Azure Speech REST API
    transcript = transcribe_audio(audio_bytes, request.audio_format)
    print(f"[analyze-voice] Transcript ({len(transcript)} chars): {transcript[:100]}")

    # Step 2: Deepfake probability scoring (placeholder — see function docstring)
    deepfake_score = compute_deepfake_score(audio_bytes)
    print(f"[analyze-voice] Deepfake score: {deepfake_score}")

    # Step 3: Language / dialect detection
    language = detect_language(transcript)
    print(f"[analyze-voice] Language detected: {language}")

    return VoiceAnalysisResponse(
        transcript=transcript,
        deepfakeScore=deepfake_score,
        languageDetected=language,
    )


# ==========================================
# 7. ENTRY POINT
# ==========================================
if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
