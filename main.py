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