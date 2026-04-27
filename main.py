from fastapi import FastAPI
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
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


spitch_client = Spitch(api_key=spitch_key)

@app.get("/api/generate-warning")
def generate_warning_audio():
    """
    Tests the Spitch TTS API using their official Python SDK.
    """
    try:
        # Call the API cleanly without needing to know the URL endpoints
        response = spitch_client.speech.generate(
            language="yo", # Using Yoruba as a test (or "en" for English)
            text="Bawo ni? Abeg be careful, this person fit be scammer.",
            voice="femi" # 'femi' is one of their documented African voices
        )
        
        # The API will return the speech object
        print("Spitch Response received successfully!")
        
        return {
            "status": "Success!",
            "message": "Connected to Spitch.app successfully via the official SDK without any DNS errors!",
        }
            
    except Exception as e:
        return {"status": "Error", "details": str(e)}
    
# Run the server
if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)