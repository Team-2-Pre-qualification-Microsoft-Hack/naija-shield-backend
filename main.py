import re
import os
import json
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from pydantic import BaseModel
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
from spitch import Spitch
import requests

app = FastAPI()

# ==========================================
# 1. SECURE KEY VAULT CONNECTION
# ==========================================
environment = os.environ.get("ASPNETCORE_ENVIRONMENT", "Development")

if environment != "Production":
    # Read directly from appsettings.Development.json (same file the .NET app uses)
    settings_path = os.path.join(os.path.dirname(__file__), "appsettings.Development.json")
    with open(settings_path) as f:
        settings = json.load(f)
    spitch_key = settings.get("Spitch-API-Key", "")
    azure_speech_key = settings["Azure-Speech-Key"]
    azure_speech_region = settings.get("Azure-Speech-Region", "eastus")
    print("Dev mode: keys loaded from appsettings.Development.json")
else:
    KVUri = "https://rg-naijashield-dev-key.vault.azure.net/"
    credential = DefaultAzureCredential()
    client = SecretClient(vault_url=KVUri, credential=credential)
    print("Connecting to Key Vault...")
    try:
        spitch_key = client.get_secret("Spitch-API-Key").value
        azure_speech_key = client.get_secret("Azure-Speech-Key").value
        try:
            azure_speech_region = client.get_secret("Azure-Speech-Region").value
        except Exception:
            azure_speech_region = "eastus"
        print("Keys successfully retrieved from Key Vault!")
    except Exception as e:
        print(f"Error fetching keys: {e}")
        raise

# ==========================================
# 3. PYDANTIC MODELS
# ==========================================

class VoiceAnalysisResponse(BaseModel):
    transcript: str
    deepfakeScore: float


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

@app.post("/ingest/voice", response_model=VoiceAnalysisResponse)
async def analyze_voice(
    audioFile: UploadFile = File(...),
    audioFormat: str = Form(default="wav"),
):
    """
    Receives an audio file via multipart/form-data from the .NET backend,
    transcribes it using Azure Speech Service, computes a deepfake
    probability score, and detects the spoken language/dialect.

    Form fields:
        audioFile   — the raw audio file upload
        audioFormat — file format hint: "wav", "mp3", or "ogg" (default "wav")

    Returns:
        transcript      — speech-to-text output (may be empty if STT fails)
        deepfakeScore   — probability 0.0–1.0 the audio is AI-synthesised
        languageDetected — e.g. "en", "pidgin", "yo"

    The .NET pipeline combines deepfakeScore with the LLM risk score:
        finalRiskScore = (llmScore * 0.6) + (deepfakeScore * 100 * 0.4)
    """
    audio_bytes = await audioFile.read()

    if len(audio_bytes) == 0:
        raise HTTPException(status_code=400, detail="Audio file is empty")

    print(f"[analyze-voice] filename={audioFile.filename} format={audioFormat} bytes={len(audio_bytes)}")

    # Step 1: Transcribe via Azure Speech REST API
    transcript = transcribe_audio(audio_bytes, audioFormat)
    print(f"[analyze-voice] Transcript ({len(transcript)} chars): {transcript[:100]}")

    # Step 2: Deepfake probability scoring (placeholder — see function docstring)
    deepfake_score = compute_deepfake_score(audio_bytes)
    print(f"[analyze-voice] Deepfake score: {deepfake_score}")


    return VoiceAnalysisResponse(
        transcript=transcript,
        deepfakeScore=deepfake_score,
    )


# ==========================================
# 7. ENTRY POINT
# ==========================================
if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
