# 🚀 ThreadedMosaic Development Setup Guide

**Quick Start Guide for ThreadedMosaic Modernization Project**

## 📋 Prerequisites

### Required Software
- **.NET 9 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Git** - [Download here](https://git-scm.com/downloads)  
- **Visual Studio 2022** or **VS Code** (optional but recommended)

### Optional for Docker Setup
- **Docker Desktop** - [Download here](https://www.docker.com/products/docker-desktop)

## 🚀 Quick Start

### Option 1: PowerShell Script (Recommended for Windows)
```powershell
# Start both API and Blazor Server in local development mode
.\run-dev.ps1 -Mode local

# Clean build before starting
.\run-dev.ps1 -Mode local -Clean

# Docker mode (requires Docker Desktop)
.\run-dev.ps1 -Mode docker
```

### Option 2: Bash Script (Linux/macOS/WSL)
```bash
# Make script executable (first time only)
chmod +x run-dev.sh

# Start both services locally
./run-dev.sh local

# Clean build before starting
./run-dev.sh local --clean

# Docker mode
./run-dev.sh docker
```

### Option 3: Manual Setup
```bash
# Terminal 1 - Start API Server
cd ThreadedMosaic.Api
dotnet run --urls "https://localhost:7001;http://localhost:5001"

# Terminal 2 - Start Blazor Server
cd ThreadedMosaic.BlazorServer  
dotnet run --urls "https://localhost:7002;http://localhost:5002"
```

## 🌐 Application URLs

| Service | HTTPS | HTTP | Purpose |
|---------|-------|------|---------|
| **API Server** | https://localhost:7001 | http://localhost:5001 | REST API & File Upload |
| **Blazor Server** | https://localhost:7002 | http://localhost:5002 | Web UI |
| **API Documentation** | https://localhost:7001/swagger | http://localhost:5001/swagger | Swagger UI |

## 🧪 Testing & Development

### Run Tests
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test --filter "Category=Integration"
```

### Database Operations
```bash
# Apply migrations
dotnet ef database update --project ThreadedMosaic.Api

# Add new migration
dotnet ef migrations add MigrationName --project ThreadedMosaic.Core

# Reset database (development only)
dotnet ef database drop --project ThreadedMosaic.Api --force
dotnet ef database update --project ThreadedMosaic.Api
```

## 🐛 Troubleshooting

### Common Issues

**Port Already in Use**
```bash
# Windows: Kill processes using our ports
Get-Process -Id (Get-NetTCPConnection -LocalPort 5001,7001,5002,7002).OwningProcess | Stop-Process -Force

# Linux/macOS: Kill processes using our ports
sudo lsof -ti:5001,7001,5002,7002 | xargs kill -9
```

**CORS Issues**
- Ensure API server is running before Blazor server
- Check that `ApiConfiguration:BaseUrl` in `appsettings.json` matches API server URL

**File Upload Issues**
- Ensure both API and Blazor servers are running
- Check browser console for JavaScript errors
- Verify file size is under 50MB limit

**Database Issues**
```bash
# Reset SQLite database
rm ThreadedMosaic.Api/ThreadedMosaic.db
dotnet ef database update --project ThreadedMosaic.Api
```

### Log Locations
- **API Logs**: Console output from API server
- **Blazor Logs**: Console output from Blazor server  
- **Browser Logs**: F12 Developer Tools → Console

## 🔧 Development Configuration

### API Configuration (`ThreadedMosaic.Api/appsettings.Development.json`)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "ThreadedMosaic": "Debug"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=ThreadedMosaic.db"
  },
  "MosaicConfiguration": {
    "MaxConcurrentJobs": 2,
    "TempFileRetentionDays": 1
  }
}
```

### Blazor Configuration (`ThreadedMosaic.BlazorServer/appsettings.Development.json`)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "ThreadedMosaic": "Debug"
    }
  },
  "ApiConfiguration": {
    "BaseUrl": "https://localhost:7001",
    "TimeoutSeconds": 300
  }
}
```

## 🏗️ Project Structure

```
ThreadedMosaicModern/
├── ThreadedMosaic.Core/          # Core business logic & models
├── ThreadedMosaic.Api/           # REST API & file services  
├── ThreadedMosaic.BlazorServer/  # Web UI (Blazor Server)
├── ThreadedMosaic.Tests/         # Comprehensive test suite
├── run-dev.ps1                   # Windows development script
├── run-dev.sh                    # Linux/macOS development script
├── docker-compose.yml            # Docker development setup
└── ThreadedMosaic.sln            # Visual Studio solution
```

## 📖 Development Workflow

1. **Start Development Environment**
   ```bash
   .\run-dev.ps1 -Mode local
   ```

2. **Open Application**
   - Navigate to https://localhost:7002 for web UI
   - Navigate to https://localhost:7001/swagger for API docs

3. **Make Changes**
   - Hot reload is enabled for Blazor components
   - API changes require restart

4. **Run Tests**
   ```bash
   dotnet test --filter "Category!=Performance"
   ```

5. **Commit Changes**
   ```bash
   git add .
   git commit -m "Your commit message"
   git push
   ```

## 🚢 Production Deployment

### Docker Production
```bash
# Build production images
docker-compose -f docker-compose.prod.yml build

# Deploy to production
docker-compose -f docker-compose.prod.yml up -d
```

### Manual Deployment
```bash
# Build release versions
dotnet publish ThreadedMosaic.Api -c Release -o ./publish/api
dotnet publish ThreadedMosaic.BlazorServer -c Release -o ./publish/blazor

# Deploy to server
# Configure reverse proxy (nginx/IIS)
# Setup SSL certificates
# Configure production database
```

## 💡 Tips & Best Practices

- **Use HTTPS**: Always test with HTTPS URLs for production-like environment
- **Database Migrations**: Always create migrations for schema changes
- **Testing**: Run tests before committing changes
- **Logging**: Use structured logging for better debugging
- **Performance**: Monitor memory usage during mosaic generation

## 📞 Support

For issues and questions:
1. Check the troubleshooting section above
2. Review console logs for error messages  
3. Verify all prerequisites are installed
4. Ensure both API and Blazor servers are running

---

*Last Updated: August 29, 2025*
*ThreadedMosaic Modernization Project*