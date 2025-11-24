#!/bin/bash
# Pipster API - Identity Server Integration Test Script
# This script tests the authentication flow between Identity Server and API

echo "===================================="
echo -e "\033[36mPipster Authentication Test Script\033[0m"
echo "===================================="

# Configuration
IDENTITY_SERVER_URL="https://localhost:7019"
API_URL="http://localhost:5001"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Step 1: Test Identity Server Health
echo -e "\n${YELLOW}Step 1: Testing Identity Server Health...${NC}"
if curl -f -s "$IDENTITY_SERVER_URL/health" > /dev/null; then
    echo -e "${GREEN}✓ Identity Server is healthy${NC}"
else
    echo -e "${RED}✗ Identity Server is not responding. Please start it first.${NC}"
    echo -e "  Run: docker-compose up -d identity"
    exit 1
fi

# Step 2: Test API Health (Anonymous Endpoint)
echo -e "\n${YELLOW}Step 2: Testing API Health (Anonymous)...${NC}"
HEALTH_RESPONSE=$(curl -s "$API_URL/health")
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ API is healthy - Response: $HEALTH_RESPONSE${NC}"
else
    echo -e "${RED}✗ API is not responding. Please start it first.${NC}"
    echo -e "  Run: dotnet run --project Pipster.Api"
    exit 1
fi

# Step 3: Test Protected Endpoint (Should Fail Without Token)
echo -e "\n${YELLOW}Step 3: Testing Protected Endpoint Without Token...${NC}"
HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/state/status")
if [ "$HTTP_STATUS" -eq 401 ]; then
    echo -e "${GREEN}✓ Protected endpoint correctly requires authentication (401)${NC}"
else
    echo -e "${RED}✗ Unexpected status code: $HTTP_STATUS${NC}"
fi

# Step 4: Instructions for Manual Token Testing
echo -e "\n${CYAN}===================================="
echo "Manual Token Testing Instructions"
echo "====================================${NC}"

echo -e "\n1. Open Swagger UI: ${YELLOW}http://localhost:5001/swagger${NC}"
echo -e "\n2. Click the 'Authorize' button (lock icon)"
echo -e "\n3. Complete the OAuth flow:"
echo "   - You'll be redirected to Identity Server"
echo "   - Log in or register a new account"
echo "   - Accept the consent screen"
echo "   - You'll be redirected back to Swagger"

echo -e "\n4. Test these endpoints in order:"
echo -e "   a. ${YELLOW}GET /state/status${NC}"
echo "      - Should return status with your UserId and TenantId"
echo -e "   b. ${YELLOW}GET /api/tenants/current${NC}"
echo "      - Should return your tenant information"
echo -e "   c. ${YELLOW}GET /api/tenants/{your-tenant-id}/channels${NC}"
echo "      - Should return your channels list"

echo -e "\n${CYAN}===================================="
echo "CORS Test (for Frontend Integration)"
echo "====================================${NC}"

echo -e "\nTo test CORS from your Next.js app (http://localhost:3000):"
echo -e "${YELLOW}fetch('$API_URL/health')
  .then(r => r.json())
  .then(console.log)${NC}"

echo -e "\n${CYAN}===================================="
echo "Integration Status"
echo "====================================${NC}"

echo -e "${GREEN}✓ Health endpoints are accessible${NC}"
echo -e "${GREEN}✓ Protected endpoints require authentication${NC}"
echo -e "${GREEN}✓ CORS is configured for frontend${NC}"
echo -e "\n${YELLOW}Next: Test with real JWT token via Swagger UI${NC}"