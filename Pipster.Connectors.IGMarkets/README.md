# Pipster.Connectors.IGMarkets

IG Markets broker connector for automated trade execution from Telegram signals.

## Features

- ✅ Market and limit order placement
- ✅ Stop-loss and take-profit support
- ✅ Automatic session management (token refresh)
- ✅ Retry policies for transient failures
- ✅ Circuit breaker for API protection
- ✅ Support for forex and metals (XAUUSD, EURUSD, etc.)

## Setup

### 1. Get IG Demo Account

1. Sign up for IG demo account: https://www.ig.com/uk/demo-account
2. Receive credentials via email

### 2. Get API Key

1. Visit IG Labs: https://labs.ig.com/
2. Register and generate API key
3. Save your API key (format: xxx-xxx-xxx-xxx)

### 3. Configure Credentials

For **development**, use user secrets:
```bash
cd Pipster.Workers.Executor
dotnet user-secrets set "IGMarkets:ApiKey" "your-api-key"
dotnet user-secrets set "IGMarkets:Username" "your-demo-username"
dotnet user-secrets set "IGMarkets:Password" "your-demo-password"