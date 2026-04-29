#!/usr/bin/env bash
# Deploy the NaijaShield reputation-seeder Logic App to Azure.
# Prerequisites: az CLI installed + logged in (az login), jq installed.
#
# Usage:
#   chmod +x deploy-reputation-seeder.sh
#   ./deploy-reputation-seeder.sh

set -euo pipefail

# ── Configuration ──────────────────────────────────────────────────────────────
RESOURCE_GROUP="rg-naijashield-dev"
LOCATION="westeurope"
API_BASE_URL="https://naija-shield-backend.azurewebsites.net"
SCAMMER_NUMBER="+2348099000000"
TEMPLATE="$(dirname "$0")/logicapp-reputation-seeder.json"

# ── 1. Ensure the resource group exists ────────────────────────────────────────
echo "→ Ensuring resource group '$RESOURCE_GROUP' exists..."
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --output none

# ── 2. Deploy the Logic App ARM template ───────────────────────────────────────
echo "→ Deploying Logic App ARM template..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$TEMPLATE" \
  --parameters \
      apiBaseUrl="$API_BASE_URL" \
      scammerNumber="$SCAMMER_NUMBER" \
  --output json)

LOGIC_APP_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.properties.outputs.logicAppUrl.value')
echo "✓ Logic App deployed."
echo "  Trigger URL: $LOGIC_APP_URL"

# ── 3. Trigger it immediately — seeds 8 incidents then queries reputation ──────
echo "→ Running Logic App now (this takes ~10-20 seconds)..."
HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$LOGIC_APP_URL")

if [ "$HTTP_STATUS" = "200" ] || [ "$HTTP_STATUS" = "202" ]; then
  echo "✓ Logic App triggered successfully (HTTP $HTTP_STATUS)."
  echo "  Wait ~15 seconds for the 8 seed calls to complete, then run:"
  echo ""
  echo "  curl \"$API_BASE_URL/api/numbers/%2B2348099000000/reputation\" | jq ."
  echo ""
else
  echo "✗ Trigger returned HTTP $HTTP_STATUS — check the Logic App run history in the Azure Portal."
fi
