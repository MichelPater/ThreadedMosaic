#!/usr/bin/env pwsh
# ThreadedMosaic Development Environment Startup Script
# Usage: .\run-dev.ps1 -Mode local|docker [-Clean]

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("local", "docker")]
    [string]$Mode = "local",
    
    [Parameter(Mandatory=$false)]
    [switch]$Clean
)

function Write-ColorOutput($Color, $Message) {
    switch ($Color) {
        "Red"    { Write-Host $Message -ForegroundColor Red }
        "Green"  { Write-Host $Message -ForegroundColor Green }
        "Yellow" { Write-Host $Message -ForegroundColor Yellow }
        "Blue"   { Write-Host $Message -ForegroundColor Blue }
        default  { Write-Host $Message }
    }
}

function Test-DotNetInstalled {
    try {
        $version = dotnet --version
        Write-ColorOutput "Green" "* .NET $version installed"
        return $true
    }
    catch {
        Write-ColorOutput "Red" "X .NET SDK not found. Please install .NET 9 SDK"
        return $false
    }
}

function Test-DockerInstalled {
    try {
        $version = docker --version
        Write-ColorOutput "Green" "* $version installed"
        return $true
    }
    catch {
        Write-ColorOutput "Red" "X Docker not found. Please install Docker Desktop"
        return $false
    }
}

function Start-LocalDevelopment {
    Write-ColorOutput "Blue" "Starting ThreadedMosaic in LOCAL mode..."
    
    if (-not (Test-DotNetInstalled)) { exit 1 }
    
    if ($Clean) {
        Write-ColorOutput "Yellow" "Cleaning projects..."
        dotnet clean ThreadedMosaic.sln
        dotnet build ThreadedMosaic.sln --configuration Debug
    }
    
    # Kill any existing processes on our ports
    Write-ColorOutput "Yellow" "Stopping existing services on ports 5001, 7001, 5002, 7002..."
    
    # Stop processes using our ports (Windows)
    try {
        $processes = Get-NetTCPConnection -LocalPort @(5001, 7001, 5002, 7002) -ErrorAction SilentlyContinue
        foreach ($proc in $processes) {
            $processId = $proc.OwningProcess
            if ($processId) {
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                Write-ColorOutput "Yellow" "Stopped process $processId using port $($proc.LocalPort)"
            }
        }
    }
    catch {
        # Ignore errors - ports might not be in use
    }
    
    Write-ColorOutput "Green" "Starting API Server on https://localhost:7001 and http://localhost:5001..."
    $apiJob = Start-Job -ScriptBlock {
        Set-Location $using:PWD
        dotnet run --project "ThreadedMosaic.Api\ThreadedMosaic.Api.csproj" --urls "https://localhost:7001;http://localhost:5001"
    }
    
    # Wait a bit for API to start
    Start-Sleep -Seconds 3
    
    Write-ColorOutput "Green" "Starting Blazor Server on https://localhost:7002 and http://localhost:5002..."
    $blazorJob = Start-Job -ScriptBlock {
        Set-Location $using:PWD
        dotnet run --project "ThreadedMosaic.BlazorServer\ThreadedMosaic.BlazorServer.csproj" --urls "https://localhost:7002;http://localhost:5002"
    }
    
    Write-ColorOutput "Blue" "Waiting for services to start..."
    Start-Sleep -Seconds 5
    
    # Test API health
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5001/health" -TimeoutSec 5
        Write-ColorOutput "Green" "* API service is healthy"
    }
    catch {
        Write-ColorOutput "Yellow" "! API service may still be starting..."
    }
    
    # Test Blazor health  
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5002" -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-ColorOutput "Green" "* Blazor service is healthy"
        }
    }
    catch {
        Write-ColorOutput "Yellow" "! Blazor service may still be starting..."
    }
    
    Write-ColorOutput "Green" "
ThreadedMosaic Development Environment Started!

API Server:     https://localhost:7001 (primary) | http://localhost:5001
Blazor Server:  https://localhost:7002 (primary) | http://localhost:5002
API Docs:       https://localhost:7001/swagger

Press Ctrl+C to stop all services...
"

    try {
        # Wait for user interrupt
        while ($true) {
            Start-Sleep -Seconds 1
        }
    }
    finally {
        Write-ColorOutput "Yellow" "Stopping services..."
        Stop-Job $apiJob, $blazorJob -ErrorAction SilentlyContinue
        Remove-Job $apiJob, $blazorJob -ErrorAction SilentlyContinue
        Write-ColorOutput "Green" "* All services stopped"
    }
}

function Start-DockerDevelopment {
    Write-ColorOutput "Blue" "Starting ThreadedMosaic in DOCKER mode..."
    
    if (-not (Test-DockerInstalled)) { exit 1 }
    
    if ($Clean) {
        Write-ColorOutput "Yellow" "Cleaning Docker containers and images..."
        docker-compose down --rmi all --volumes --remove-orphans
    }
    
    Write-ColorOutput "Green" "Building and starting Docker containers..."
    docker-compose up --build -d
    
    Write-ColorOutput "Blue" "Waiting for services to start..."
    Start-Sleep -Seconds 10
    
    Write-ColorOutput "Green" "
ThreadedMosaic Docker Environment Started!

API Server:     https://localhost:7001 | http://localhost:5001  
Blazor Server:  https://localhost:7002 | http://localhost:5002
API Docs:       https://localhost:7001/swagger

View logs: docker-compose logs -f
Stop:      docker-compose down
"
}

# Main execution
Write-ColorOutput "Blue" "
======================================================================
                   ThreadedMosaic Development                        
                      Startup Script                              
======================================================================"

switch ($Mode) {
    "local"  { Start-LocalDevelopment }
    "docker" { Start-DockerDevelopment }
}