import azure.cognitiveservices.speech as speechsdk
from fastapi import FastAPI
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
import os
from spitch import Spitch
import requests

app = FastAPI()

# ==========================================
# 1. SECURE KEY VAULT CONNECTION
# ==========================================
# IMPORTANT: Update this URL to match your exact Key Vault name!
KVUri = "https://rg-naijashield-dev-key.vault.azure.net/"
credential = DefaultAzureCredential()
client = SecretClient(vault_url=KVUri, credential=credential)

print("Connecting to Key Vault...")

# ==========================================
# 2. FETCH THE VOICE INFRASTRUCTURE KEYS
# ==========================================
try:
    spitch_key = client.get_secret("Spitch-API-Key").value
    azure_speech_key = client.get_secret("Azure-Speech-Key").value
    print("Spitch and Azure Speech keys successfully retrieved!")
except Exception as e:
    print(f"Error fetching keys: {e}")
    print("Did you remember to run 'az login' in the terminal first?")

# ==========================================
# 3. PLUG & PLAY TEST ENDPOINT
# ==========================================
@app.get("/api/health-voice")
def health_check():
    """
    Proves the Sidecar has its keys and is ready to process audio.
    """
    # A fake payload to prove we can build the Spitch request format
    spitch_headers = {
        "Authorization": f"Bearer {spitch_key}",
        "Content-Type": "application/json"
    }
    
    return {
        "status": "Online", 
        "message": "Python Sidecar is ready for Jambonz/WebRTC WebSocket streams.",
        "keys_loaded": {
            "spitch": "Loaded (Ready for Text-To-Speech)",
            "azure_speech": "Loaded (Ready for Speech-To-Text)"
        }
    }


@app.get("/api/generate-warning")
def generate_warning_audio():
    """
    PIVOT: Using Azure native Text-to-Speech since Spitch is throwing 503 errors.
    Generates a localized audio warning using Azure's Nigerian English neural voices.
    """
    try:
        # 1. Set up the Azure Speech Config using the key from the vault
        # NOTE: Update "swedencentral" to match whatever region you used when creating the Speech resource!
        speech_config = speechsdk.SpeechConfig(subscription=azure_speech_key, region="swedencentral")
        
        # 2. Select the native Nigerian English Voice (Ezinne is the female neural voice)
        speech_config.speech_synthesis_voice_name = "en-NG-EzinneNeural"
        
        # 3. Tell it to save the output to a local file
        file_name = "azure_scam_warning.wav"
        audio_config = speechsdk.audio.AudioOutputConfig(filename=file_name)
        
        # 4. Create the synthesizer and speak the text
        synthesizer = speechsdk.SpeechSynthesizer(speech_config=speech_config, audio_config=audio_config)
        
        warning_text = "Attention. We suspect the person you are speaking to may be a scammer. Do not share your OTP or bank details."
        
        print("Generating audio via Azure...")
        result = synthesizer.speak_text_async(warning_text).get()
        
        # 5. Check if it worked
        if result.reason == speechsdk.ResultReason.SynthesizingAudioCompleted:
            return {
                "status": "Success!",
                "message": f"Pivot successful! Audio generated via Azure. Listen to '{file_name}' in your project folder."
            }
        elif result.reason == speechsdk.ResultReason.Canceled:
            cancellation_details = result.cancellation_details
            return {"status": "Failed", "error": f"Speech synthesis canceled: {cancellation_details.reason}"}
            
    except Exception as e:
        return {"status": "Error", "details": str(e)}
# Run the server
if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)